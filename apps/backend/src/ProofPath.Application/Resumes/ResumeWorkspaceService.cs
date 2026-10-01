using ProofPath.Application.Analysis;
using ProofPath.Application.Files;
using ProofPath.Domain.Entities;

namespace ProofPath.Application.Resumes;

public sealed class ResumeWorkspaceService(IResumePersistence database, IPrivateFileStore files, IDocumentTextExtractor extractor,
    IAnalysisQueue queue) : IResumeWorkspace, IResumeInputReader
{
    public async Task<ResumeUploadResult> UploadAsync(string userId, string fileName, string contentType, byte[] content, CancellationToken ct)
    {
        extractor.Validate(content, fileName, contentType);
        var resume = new Resume
        {
            Id = Guid.NewGuid(),
            FileName = Path.GetFileName(fileName.Replace('\\', '/')),
            StorageKey = Guid.NewGuid().ToString("N"),
            ContentType = contentType,
            CreatedAt = DateTime.UtcNow
        };
        // Persist the key before touching object storage. Interrupted uploads remain discoverable for cleanup.
        await database.ReserveAsync(userId, resume, ct);
        try
        {
            await using var transaction = await database.BeginAsync(userId, ct); // same ownership lock as account deletion
            if (await database.FindAsync(userId, resume.Id, ct) is null) throw new ResumeProblem("RESOURCE_REMOVED", 404);
            var stored = await files.PutAsync(resume.StorageKey, new MemoryStream(content, writable: false), ct);
            resume.Length = stored.Length; resume.Sha256 = stored.Sha256; resume.Status = "Processing";
            resume.AnalysisJobId = await queue.EnqueueAsync(userId, AnalysisKind.Resume, resume.Id, "resume-extraction-v1", ct);
            await database.SaveAsync(ct); await transaction.CommitAsync(ct);
            return new ResumeUploadResult(resume.Id, resume.AnalysisJobId.Value);
        }
        catch
        {
            // A cancellation must not suppress compensation. If DB is unavailable, the Uploading reservation
            // remains durable and the cleanup worker recovers it after ten minutes.
            try
            {
                await database.MarkUploadFailedAsync(resume.Id, CancellationToken.None);
                await files.DeleteAsync(resume.StorageKey, CancellationToken.None);
            }
            catch { /* durable reservation retains the key for recovery */ }
            throw;
        }
    }
    public Task<ResumeSummary[]> ListAsync(string userId, CancellationToken ct) => database.ListAsync(userId, ct);
    public async Task<ResumeDownload?> DownloadAsync(string userId, Guid id, CancellationToken ct)
    {
        var resume = await database.FindAsync(userId, id, ct);
        if (resume is null || resume.Status is not ("Processing" or "ReadyForReview" or "Confirmed")) return null;
        var stream = await files.OpenReadAsync(resume.StorageKey, ct);
        return stream is null ? null : new ResumeDownload(stream, resume.ContentType, resume.FileName);
    }
    public Task<ResumeInput?> GetInputAsync(Guid candidateProfileId, Guid resumeId, CancellationToken ct) => database.GetInputAsync(candidateProfileId, resumeId, ct);
    public async Task<ResumeReview?> ReviewAsync(string userId, Guid id, CancellationToken ct)
    {
        var extraction = await database.ExtractionAsync(userId, id, ct);
        return extraction is null ? null : View(extraction);
    }
    private static ResumeReview View(ResumeExtraction extraction) => new(extraction.ResumeId, extraction.Revision,
        ResumeJson.Read<ResumeAnalysisResult>(extraction.MachineJson), ResumeJson.Read<ResumeDraft>(extraction.DraftJson), extraction.ConfirmedAt, extraction.Active);
    public async Task<ResumeReview> SaveAsync(string userId, Guid id, int revision, ResumeDraft draft, CancellationToken ct)
    {
        await using var transaction = await database.BeginAsync(userId, ct);
        var extraction = await database.ExtractionAsync(userId, id, ct)
            ?? throw new ResumeProblem("EXTRACTION_NOT_READY", 404);
        if (extraction.ConfirmedAt is not null) throw new ResumeProblem("EXTRACTION_ALREADY_CONFIRMED", 409);
        if (extraction.Revision != revision) throw new ResumeProblem("REVIEW_CONFLICT", 409);
        try { ResumeValidation.Validate(draft, ResumeJson.Read<ResumeAnalysisResult>(extraction.MachineJson).Source, machine: false); }
        catch (AnalysisFailure) { throw new ResumeProblem("INVALID_REVIEW"); }
        extraction.DraftJson = ResumeJson.Serialize(draft); extraction.Revision++;
        await database.SaveAsync(ct); await transaction.CommitAsync(ct); return View(extraction);
    }
    public async Task ConfirmAsync(string userId, Guid id, int revision, CancellationToken ct)
    {
        await using var transaction = await database.BeginAsync(userId, ct);
        var extraction = await database.ExtractionAsync(userId, id, ct)
            ?? throw new ResumeProblem("EXTRACTION_NOT_READY", 404);
        if (extraction.Revision != revision) throw new ResumeProblem("REVIEW_CONFLICT", 409);
        if (extraction.ConfirmedAt is not null) return; // replay cannot reactivate an obsolete version
        var draft = ResumeJson.Read<ResumeDraft>(extraction.DraftJson); var machine = ResumeJson.Read<ResumeAnalysisResult>(extraction.MachineJson);
        ResumeValidation.Validate(draft, machine.Source, machine: false);
        await database.DeactivateAsync(userId, ct);
        var now = DateTime.UtcNow;
        foreach (var fact in draft.Facts.Distinct())
        {
            CandidateFact row = fact.Kind switch { "Experience" => new Experience(), "Education" => new Education(), "Project" => new Project(), _ => new Credential() };
            row.Id = Guid.NewGuid(); row.CandidateProfileId = transaction.CandidateProfileId; row.ResumeExtractionId = extraction.Id;
            row.Name = fact.Name; row.Organization = fact.Organization; row.Detail = fact.Detail; row.StartDateText = fact.StartDateText;
            row.EndDateText = fact.EndDateText; row.Status = fact.Status; row.SourceBlockId = fact.SourceBlockId; row.Quote = fact.Quote;
            row.UserCorrected = !machine.Draft.Facts.Contains(fact); database.AddFact(row);
        }
        var aliases = await database.AliasesAsync(ct);
        foreach (var skill in draft.Skills.Distinct())
        {
            aliases.TryGetValue(skill.Term.Trim().ToUpperInvariant(), out var skillId);
            database.AddEvidence(new EvidenceItem
            {
                Id = Guid.NewGuid(),
                CandidateProfileId = transaction.CandidateProfileId,
                ResumeExtractionId = extraction.Id,
                SkillId = skillId,
                OriginalTerm = skill.Term,
                Context = skill.Context,
                SourceBlockId = skill.SourceBlockId,
                Quote = skill.Quote,
                // Resume statements remain self-reported; provider labels never assign implementation strength.
                Strength = "Weak",
                UserCorrected = !machine.Draft.Skills.Contains(skill),
                ObservedAt = now
            });
        }
        foreach (var behavior in draft.Behaviors.Distinct())
            database.AddBehavior(new BehavioralEvidenceItem
            {
                Id = Guid.NewGuid(),
                CandidateProfileId = transaction.CandidateProfileId,
                ResumeExtractionId = extraction.Id,
                ThemeKey = behavior.ThemeKey,
                Statement = behavior.Statement,
                SourceBlockId = behavior.SourceBlockId,
                Quote = behavior.Quote,
                Basis = "Explicit",
                Strength = BehaviorEvidenceRules.Strength(behavior.ThemeKey, behavior.Statement)!,
                UserCorrected = !machine.Draft.Behaviors.Contains(behavior)
            });
        extraction.ConfirmedAt = now; extraction.Active = true;
        var resume = await database.FindAsync(userId, id, ct) ?? throw new ResumeProblem("RESOURCE_REMOVED", 404);
        resume.Status = "Confirmed";
        await database.SaveAsync(ct); await transaction.CommitAsync(ct);
    }
}
