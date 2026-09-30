using ProofPath.Domain.Entities;

namespace ProofPath.Application.Resumes;

public interface IResumeTransaction : IAsyncDisposable
{
    Guid CandidateProfileId { get; }
    Task CommitAsync(CancellationToken ct);
}
public interface IResumePersistence : IResumeInputReader
{
    Task<IResumeTransaction> BeginAsync(string userId, CancellationToken ct);
    Task ReserveAsync(string userId, Resume resume, CancellationToken ct);
    Task<Resume?> FindAsync(string userId, Guid id, CancellationToken ct);
    Task<ResumeSummary[]> ListAsync(string userId, CancellationToken ct);
    Task<ResumeExtraction?> ExtractionAsync(string userId, Guid id, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task MarkUploadFailedAsync(Guid id, CancellationToken ct);
    Task DeactivateAsync(string userId, CancellationToken ct);
    Task<Dictionary<string, string>> AliasesAsync(CancellationToken ct);
    void AddFact(CandidateFact fact);
    void AddEvidence(EvidenceItem evidence);
    void AddBehavior(BehavioralEvidenceItem evidence);
}
