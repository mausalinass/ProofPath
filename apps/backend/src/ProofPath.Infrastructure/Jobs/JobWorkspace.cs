using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProofPath.Application.Analysis;
using ProofPath.Application.Jobs;
using ProofPath.Application.Resumes;
using ProofPath.Domain.Entities;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Infrastructure.Jobs;

public sealed class JobRequirementNormalizer(ProofPathDbContext database) : IJobRequirementNormalizer
{
    private static readonly Dictionary<string, string> Equivalents = new(StringComparer.OrdinalIgnoreCase)
    { [".NET WEB API"] = "aspnet-core", ["ASP.NET WEB API"] = "aspnet-core" };

    public Task<SkillOption[]> CatalogAsync(CancellationToken ct) => database.Skills.AsNoTracking()
        .OrderBy(item => item.DisplayName).Select(item => new SkillOption(item.Id, item.DisplayName)).ToArrayAsync(ct);

    public async Task<JobRequirementDraftSet> NormalizeAsync(JobRequirementDraftSet draft, CancellationToken ct)
    {
        var skills = await database.Skills.AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.DisplayName, ct);
        var canonical = skills.ToDictionary(item => item.Value.Trim().ToUpperInvariant(), item => item.Key, StringComparer.Ordinal);
        foreach (var id in skills.Keys) canonical.TryAdd(id.ToUpperInvariant(), id);
        var aliases = await database.SkillAliases.AsNoTracking().ToDictionaryAsync(item => item.Alias, item => item.SkillId, StringComparer.OrdinalIgnoreCase, ct);
        var normalized = draft.Requirements.Select(item => Normalize(item, skills, canonical, aliases)).ToArray();
        var consolidated = new List<JobRequirementDraft>();
        foreach (var item in normalized)
        {
            var identity = $"{item.Category}|{item.SkillId ?? item.SkillTerm?.Trim().ToUpperInvariant() ?? item.BehavioralThemeKey ?? item.OriginalWording.Trim().ToUpperInvariant()}|{item.GroupKey}|{item.GroupType}";
            var index = consolidated.FindIndex(existing => existing.Key.StartsWith(identity + "|", StringComparison.Ordinal));
            var keyed = item with { Key = $"{identity}|{item.Key}" };
            if (index < 0) { consolidated.Add(keyed); continue; }
            var prior = consolidated[index];
            consolidated[index] = prior with { Level = Stronger(prior.Level, item.Level), Importance = Stronger(prior.Importance, item.Importance) };
        }
        return new(consolidated.Select((item, index) => item with { Key = $"req-{index + 1}" }).ToArray());
    }

    private static JobRequirementDraft Normalize(JobRequirementDraft item, IReadOnlyDictionary<string, string> skills,
        IReadOnlyDictionary<string, string> canonical, IReadOnlyDictionary<string, string> aliases)
    {
        if (item.Category != RequirementCategory.TechnicalSkill)
            return item with { SkillId = null, NormalizationStatus = RequirementNormalizationStatus.NotApplicable };
        var term = item.SkillTerm!.Trim(); var upper = term.ToUpperInvariant();
        if (canonical.TryGetValue(upper, out var exact)) return item with { SkillTerm = term, SkillId = exact, NormalizationStatus = RequirementNormalizationStatus.Exact };
        if (aliases.TryGetValue(term, out var alias)) return item with { SkillTerm = term, SkillId = alias, NormalizationStatus = RequirementNormalizationStatus.Alias };
        if (Equivalents.TryGetValue(term, out var equivalent)) return item with { SkillTerm = term, SkillId = equivalent, NormalizationStatus = RequirementNormalizationStatus.Equivalent };
        if (item.SkillId is not null && skills.ContainsKey(item.SkillId)) return item with { SkillTerm = term, NormalizationStatus = RequirementNormalizationStatus.Suggested };
        return item with { SkillTerm = term, SkillId = null, NormalizationStatus = RequirementNormalizationStatus.Unresolved };
    }
    private static RequirementLevel Stronger(RequirementLevel left, RequirementLevel right) =>
        (left, right) switch { (RequirementLevel.Required, _) or (_, RequirementLevel.Required) => RequirementLevel.Required,
            (RequirementLevel.Preferred, _) or (_, RequirementLevel.Preferred) => RequirementLevel.Preferred, _ => RequirementLevel.Unspecified };
    private static RequirementImportance Stronger(RequirementImportance left, RequirementImportance right) =>
        (left, right) switch { (RequirementImportance.Critical, _) or (_, RequirementImportance.Critical) => RequirementImportance.Critical,
            (RequirementImportance.High, _) or (_, RequirementImportance.High) => RequirementImportance.High,
            (RequirementImportance.Medium, _) or (_, RequirementImportance.Medium) => RequirementImportance.Medium, _ => RequirementImportance.Low };
}

public sealed class JobWorkspace(ProofPathDbContext database, IAnalysisQueue queue, IJobRequirementNormalizer normalizer) : IJobWorkspace, IJobInputReader
{
    public async Task<JobCreateResult> CreateAsync(string userId, JobWrite input, CancellationToken ct)
    {
        var profileId = await ProfileId(userId, ct); var clean = Validate(input);
        var now = DateTime.UtcNow; var job = new Job { Id = Guid.NewGuid(), CandidateProfileId = profileId, Company = clean.Company,
            Title = clean.Title, Description = clean.Description, SourceUrl = clean.SourceUrl, CreatedAt = now, UpdatedAt = now };
        database.Jobs.Add(job); await database.SaveChangesAsync(ct);
        job.AnalysisJobId = await queue.EnqueueAsync(userId, AnalysisKind.JobDescription, job.Id, "job-description-v1", ct);
        await database.SaveChangesAsync(ct); return new(View(job, null), job.AnalysisJobId.Value);
    }
    public async Task<JobCreateResult> UpdateAsync(string userId, Guid id, JobWrite input, CancellationToken ct)
    {
        var job = await Owned(userId).SingleOrDefaultAsync(item => item.Id == id, ct) ?? throw new JobProblem("JOB_NOT_FOUND", 404);
        if (input.ExpectedDescriptionVersion != job.DescriptionVersion) throw new JobProblem("JOB_VERSION_CONFLICT", 409);
        var clean = Validate(input); var descriptionChanged = !string.Equals(job.Description, clean.Description, StringComparison.Ordinal);
        job.Company = clean.Company; job.Title = clean.Title; job.SourceUrl = clean.SourceUrl; job.UpdatedAt = DateTime.UtcNow;
        if (descriptionChanged)
        {
            job.Description = clean.Description; job.DescriptionVersion++; job.Status = JobStatus.Processing;
            await database.JobRequirementExtractions.Where(item => item.JobId == job.Id && !item.Outdated)
                .ExecuteUpdateAsync(set => set.SetProperty(item => item.Outdated, true), ct);
            await database.RequirementSets.Where(item => item.JobId == job.Id && item.Active)
                .ExecuteUpdateAsync(set => set.SetProperty(item => item.Active, false), ct);
        }
        await database.SaveChangesAsync(ct);
        if (descriptionChanged)
        {
            job.AnalysisJobId = await queue.EnqueueAsync(userId, AnalysisKind.JobDescription, job.Id, $"job-description-v{job.DescriptionVersion}", ct);
            await database.SaveChangesAsync(ct);
        }
        return new(View(job, await ActiveVersion(job.Id, ct)), job.AnalysisJobId ?? Guid.Empty);
    }
    public Task<JobSummary[]> ListAsync(string userId, CancellationToken ct) => Owned(userId).AsNoTracking().OrderByDescending(item => item.UpdatedAt)
        .Select(item => new JobSummary(item.Id, item.Company, item.Title, item.Description, item.SourceUrl, item.DescriptionVersion,
            item.Status.ToString(), item.AnalysisJobId, item.CreatedAt, item.UpdatedAt,
            database.RequirementSets.Where(set => set.JobId == item.Id && set.Active).Select(set => (int?)set.Version).SingleOrDefault())).ToArrayAsync(ct);
    public async Task<JobSummary?> GetAsync(string userId, Guid id, CancellationToken ct)
    {
        var job = await Owned(userId).AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct);
        return job is null ? null : View(job, await ActiveVersion(id, ct));
    }
    public async Task<(string Description, int Version)?> GetInputAsync(Guid candidateProfileId, Guid jobId, CancellationToken ct) =>
        await database.Jobs.AsNoTracking().Where(item => item.Id == jobId && item.CandidateProfileId == candidateProfileId)
            .Select(item => new ValueTuple<string, int>(item.Description, item.DescriptionVersion)).SingleOrDefaultAsync(ct) is var value && value.Item1 is not null ? value : null;
    public async Task<JobReview?> ReviewAsync(string userId, Guid id, CancellationToken ct)
    {
        var extraction = await Extraction(userId, id, ct); return extraction is null ? null : Review(extraction);
    }
    public async Task<JobReview> SaveReviewAsync(string userId, Guid id, int revision, JobRequirementDraftSet draft, CancellationToken ct)
    {
        var extraction = await Extraction(userId, id, ct) ?? throw new JobProblem("JOB_EXTRACTION_NOT_READY", 404);
        if (extraction.Outdated) throw new JobProblem("JOB_EXTRACTION_OUTDATED", 409);
        if (extraction.ConfirmedAt is not null) throw new JobProblem("JOB_REQUIREMENTS_CONFIRMED", 409);
        if (extraction.Revision != revision) throw new JobProblem("JOB_REVIEW_CONFLICT", 409);
        var machine = ResumeJson.Read<JobAnalysisResult>(extraction.MachineJson);
        try { JobRequirementValidation.Validate(draft, machine.Source, machine: false); }
        catch (AnalysisFailure) { throw new JobProblem("INVALID_JOB_REVIEW"); }
        var skillIds = await database.Skills.AsNoTracking().Select(item => item.Id).ToHashSetAsync(ct);
        if (draft.Requirements.Any(item => item.SkillId is not null && !skillIds.Contains(item.SkillId))) throw new JobProblem("INVALID_JOB_REVIEW");
        var machineByKey = machine.Draft.Requirements.ToDictionary(item => item.Key);
        draft = new(draft.Requirements.Select(item => item with
        {
            NormalizationStatus = item.SkillId is not null && (!machineByKey.TryGetValue(item.Key, out var original) || original.SkillId != item.SkillId)
                ? RequirementNormalizationStatus.UserConfirmed : item.NormalizationStatus
        }).ToArray());
        extraction.DraftJson = ResumeJson.Serialize(draft); extraction.Revision++; await database.SaveChangesAsync(ct); return Review(extraction);
    }
    public async Task ConfirmAsync(string userId, Guid id, int revision, CancellationToken ct)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(ct);
        var extraction = await Extraction(userId, id, ct) ?? throw new JobProblem("JOB_EXTRACTION_NOT_READY", 404);
        if (extraction.Outdated) throw new JobProblem("JOB_EXTRACTION_OUTDATED", 409);
        if (extraction.Revision != revision) throw new JobProblem("JOB_REVIEW_CONFLICT", 409);
        if (extraction.ConfirmedAt is not null) return;
        var draft = ResumeJson.Read<JobRequirementDraftSet>(extraction.DraftJson); var machine = ResumeJson.Read<JobAnalysisResult>(extraction.MachineJson);
        try { JobRequirementValidation.Validate(draft, machine.Source, machine: false); }
        catch (AnalysisFailure) { throw new JobProblem("INVALID_JOB_REVIEW"); }
        await database.RequirementSets.Where(item => item.JobId == id && item.Active).ExecuteUpdateAsync(set => set.SetProperty(item => item.Active, false), ct);
        var version = (await database.RequirementSets.Where(item => item.JobId == id).MaxAsync(item => (int?)item.Version, ct) ?? 0) + 1;
        var set = new RequirementSet { Id = Guid.NewGuid(), JobId = id, JobRequirementExtractionId = extraction.Id, Version = version, Active = true, ConfirmedAt = DateTime.UtcNow };
        database.RequirementSets.Add(set);
        var machineByKey = machine.Draft.Requirements.ToDictionary(item => item.Key);
        foreach (var item in draft.Requirements)
        {
            var excluded = item.State == RequirementState.Excluded; var evaluable = !excluded && (item.Category != RequirementCategory.TechnicalSkill || item.SkillId is not null);
            database.JobRequirements.Add(new JobRequirement { Id = Guid.NewGuid(), RequirementSetId = set.Id, Key = item.Key,
                Category = item.Category, Level = item.Level, Importance = item.Importance, State = excluded ? RequirementState.Excluded : RequirementState.Confirmed,
                OriginalWording = item.OriginalWording, SkillTerm = item.SkillTerm, SkillId = item.SkillId, NormalizationStatus = item.NormalizationStatus,
                BehavioralThemeKey = item.BehavioralThemeKey, QualifiersJson = JsonSerializer.Serialize(item.Qualifiers), GroupKey = item.GroupKey,
                GroupType = item.GroupType, SourceBlockId = item.SourceBlockId, Quote = item.Quote, IsEvaluable = evaluable,
                IsScoreEligible = evaluable && item.Category is RequirementCategory.TechnicalSkill or RequirementCategory.Experience or RequirementCategory.EducationCredential,
                UserCorrected = !machineByKey.TryGetValue(item.Key, out var original) || ResumeJson.Serialize(original) != ResumeJson.Serialize(item) });
        }
        extraction.ConfirmedAt = DateTime.UtcNow; var job = await Owned(userId).SingleAsync(item => item.Id == id, ct); job.Status = JobStatus.Confirmed; job.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
    public Task<SkillOption[]> SkillsAsync(CancellationToken ct) => normalizer.CatalogAsync(ct);

    private IQueryable<Job> Owned(string userId) => database.Jobs.Where(item => database.CandidateProfiles.Any(profile => profile.Id == item.CandidateProfileId && profile.UserId == userId));
    private async Task<Guid> ProfileId(string userId, CancellationToken ct) => await database.CandidateProfiles.Where(item => item.UserId == userId).Select(item => (Guid?)item.Id).SingleOrDefaultAsync(ct) ?? throw new JobProblem("PROFILE_REQUIRED", 409);
    private async Task<JobRequirementExtraction?> Extraction(string userId, Guid id, CancellationToken ct) => await database.JobRequirementExtractions
        .Where(item => item.JobId == id && database.Jobs.Any(job => job.Id == item.JobId && database.CandidateProfiles.Any(profile => profile.Id == job.CandidateProfileId && profile.UserId == userId)))
        .OrderByDescending(item => item.DescriptionVersion).SingleOrDefaultAsync(item => item.DescriptionVersion == database.Jobs.Where(job => job.Id == id).Select(job => job.DescriptionVersion).Single(), ct);
    private Task<int?> ActiveVersion(Guid id, CancellationToken ct) => database.RequirementSets.Where(item => item.JobId == id && item.Active).Select(item => (int?)item.Version).SingleOrDefaultAsync(ct);
    private static JobReview Review(JobRequirementExtraction extraction) => new(extraction.JobId, extraction.DescriptionVersion, extraction.Revision,
        ResumeJson.Read<JobAnalysisResult>(extraction.MachineJson), ResumeJson.Read<JobRequirementDraftSet>(extraction.DraftJson), extraction.Outdated, extraction.ConfirmedAt);
    private static JobSummary View(Job job, int? version) => new(job.Id, job.Company, job.Title, job.Description, job.SourceUrl,
        job.DescriptionVersion, job.Status.ToString(), job.AnalysisJobId, job.CreatedAt, job.UpdatedAt, version);
    private static JobWrite Validate(JobWrite input)
    {
        var description = input.Description?.Trim() ?? "";
        if (description.Length < JobLimits.MinDescriptionCharacters || description.Length > JobLimits.MaxDescriptionCharacters) throw new JobProblem("JOB_DESCRIPTION_LENGTH");
        string? Clean(string? value, int max) { value = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); if (value?.Length > max) throw new JobProblem("JOB_INPUT_INVALID"); return value; }
        var source = Clean(input.SourceUrl, 2048);
        if (source is not null && (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))) throw new JobProblem("JOB_SOURCE_URL_INVALID");
        return input with { Company = Clean(input.Company, 255), Title = Clean(input.Title, 255), Description = description, SourceUrl = source };
    }
}

public sealed class JobAnalysisCompletion(ProofPathDbContext database) : IAnalysisCompletion
{
    public async Task SaveAsync(AnalysisLease lease, AnalysisOutput output, CancellationToken ct)
    {
        if (lease.Kind != AnalysisKind.JobDescription) return;
        var job = await database.Jobs.SingleOrDefaultAsync(item => item.Id == lease.ResourceId && item.CandidateProfileId == lease.CandidateProfileId, ct);
        if (job is null || lease.InputVersion != $"job-description-v{job.DescriptionVersion}") return;
        var result = ResumeJson.Read<JobAnalysisResult>(output.ResultJson); JobRequirementValidation.Validate(result.Draft, result.Source, machine: true);
        if (await database.JobRequirementExtractions.AnyAsync(item => item.JobId == job.Id && item.DescriptionVersion == job.DescriptionVersion, ct)) return;
        database.JobRequirementExtractions.Add(new JobRequirementExtraction { Id = Guid.NewGuid(), JobId = job.Id, DescriptionVersion = job.DescriptionVersion,
            MachineJson = output.ResultJson, DraftJson = ResumeJson.Serialize(result.Draft), Model = result.Model, CreatedAt = DateTime.UtcNow,
            PromptVersion = result.PromptVersion, SchemaVersion = result.SchemaVersion, ExtractionVersion = result.ExtractionVersion,
            NormalizationPolicyVersion = result.NormalizationPolicyVersion });
        job.Status = JobStatus.ReadyForReview; job.UpdatedAt = DateTime.UtcNow; await database.SaveChangesAsync(ct);
    }
}
