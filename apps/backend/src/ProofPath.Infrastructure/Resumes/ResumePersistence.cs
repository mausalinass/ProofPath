using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ProofPath.Application.Resumes;
using ProofPath.Domain.Entities;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Infrastructure.Resumes;

public sealed class ResumePersistence(ProofPathDbContext database) : IResumePersistence
{
    private IQueryable<Resume> Owned(string userId) => database.Resumes.Where(resume =>
        database.CandidateProfiles.Any(profile => profile.Id == resume.CandidateProfileId && profile.UserId == userId));
    private sealed class Transaction(IDbContextTransaction transaction, Guid profileId) : IResumeTransaction
    {
        public Guid CandidateProfileId => profileId;
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
    public async Task<IResumeTransaction> BeginAsync(string userId, CancellationToken ct)
    {
        var transaction = await database.Database.BeginTransactionAsync(ct);
        try
        {
            var profiles = await database.CandidateProfiles.FromSqlInterpolated($"""SELECT * FROM "CandidateProfiles" WHERE "UserId" = {userId} FOR UPDATE""")
                .AsNoTracking().ToListAsync(ct);
            var profile = profiles.SingleOrDefault() ?? throw new ResumeProblem("PROFILE_REQUIRED", 409);
            return new Transaction(transaction, profile.Id);
        }
        catch { await transaction.DisposeAsync(); throw; }
    }
    public async Task ReserveAsync(string userId, Resume resume, CancellationToken ct)
    {
        await using var transaction = await BeginAsync(userId, ct); resume.CandidateProfileId = transaction.CandidateProfileId;
        resume.Version = (await database.Resumes.Where(item => item.CandidateProfileId == resume.CandidateProfileId).MaxAsync(item => (int?)item.Version, ct) ?? 0) + 1;
        database.Resumes.Add(resume); await database.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
    public Task<Resume?> FindAsync(string userId, Guid id, CancellationToken ct) => Owned(userId).SingleOrDefaultAsync(item => item.Id == id, ct);
    public Task<ResumeSummary[]> ListAsync(string userId, CancellationToken ct) => Owned(userId).AsNoTracking()
        .OrderByDescending(item => item.Version).Select(item => new ResumeSummary(item.Id, item.Version, item.FileName,
            item.Length, item.Status, item.AnalysisJobId, item.CreatedAt,
            database.ResumeExtractions.Where(extraction => extraction.ResumeId == item.Id).Select(extraction => extraction.ConfirmedAt).FirstOrDefault(),
            database.ResumeExtractions.Any(extraction => extraction.ResumeId == item.Id && extraction.Active))).ToArrayAsync(ct);
    public Task<ResumeExtraction?> ExtractionAsync(string userId, Guid id, CancellationToken ct) =>
        database.ResumeExtractions.SingleOrDefaultAsync(item => item.ResumeId == id && Owned(userId).Any(resume => resume.Id == id), ct);
    public Task<ResumeInput?> GetInputAsync(Guid candidateProfileId, Guid resumeId, CancellationToken ct) => database.Resumes.AsNoTracking()
        .Where(item => item.CandidateProfileId == candidateProfileId && item.Id == resumeId && item.Status == "Processing")
        .Select(item => new ResumeInput(item.Id, item.StorageKey, item.ContentType)).SingleOrDefaultAsync(ct);
    public Task SaveAsync(CancellationToken ct) => database.SaveChangesAsync(ct);
    public async Task MarkUploadFailedAsync(Guid id, CancellationToken ct)
    {
        database.ChangeTracker.Clear();
        await database.Resumes.Where(item => item.Id == id).ExecuteUpdateAsync(set => set.SetProperty(item => item.Status, "UploadFailed"), ct);
    }
    public Task DeactivateAsync(string userId, CancellationToken ct) => database.ResumeExtractions
        .Where(item => Owned(userId).Any(resume => resume.Id == item.ResumeId) && item.Active)
        .ExecuteUpdateAsync(set => set.SetProperty(item => item.Active, false), ct);
    public Task<Dictionary<string, string>> AliasesAsync(CancellationToken ct) => database.SkillAliases.AsNoTracking()
        .ToDictionaryAsync(item => item.Alias, item => item.SkillId, ct);
    public void AddFact(CandidateFact fact) => database.Add(fact);
    public void AddEvidence(EvidenceItem evidence) => database.EvidenceItems.Add(evidence);
    public void AddBehavior(BehavioralEvidenceItem evidence) => database.BehavioralEvidenceItems.Add(evidence);
}
