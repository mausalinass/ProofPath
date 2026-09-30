using ProofPath.Domain.Entities;

namespace ProofPath.Application.Analysis;

public sealed record AnalysisLease(Guid Id, Guid CandidateProfileId, AnalysisKind Kind, Guid ResourceId,
    string InputVersion, Guid Token, DateTime ExpiresAt, int Attempt);
public sealed record AnalysisOutput(string ResultJson, bool Partial = false);
public sealed record AnalysisStatus(Guid Id, AnalysisKind Kind, Guid ResourceId, AnalysisState State,
    int Attempts, string? ErrorCode, bool Retryable, DateTime CreatedAt, DateTime UpdatedAt);

public interface IAnalysisQueue
{
    Task<Guid> EnqueueAsync(string userId, AnalysisKind kind, Guid resourceId, string inputVersion, CancellationToken ct);
    Task<AnalysisLease?> ClaimAsync(CancellationToken ct);
    Task<bool> IsCurrentAsync(AnalysisLease lease, CancellationToken ct);
    Task<bool> CompleteAsync(AnalysisLease lease, AnalysisOutput output, CancellationToken ct);
    Task<bool> FailAsync(AnalysisLease lease, string errorCode, bool retryable, CancellationToken ct);
    Task<AnalysisStatus?> GetAsync(string userId, Guid id, CancellationToken ct);
    Task<bool> CancelAsync(string userId, Guid id, CancellationToken ct);
    Task<bool> RetryAsync(string userId, Guid id, CancellationToken ct);
}

// Handlers only read input and return a draft. Queue completion is fenced by its lease;
// authoritative candidate facts are materialized separately upon user confirmation.
public interface IAnalysisCompletion
{
    Task SaveAsync(AnalysisLease lease, AnalysisOutput output, CancellationToken ct);
}

public interface IAnalysisHandler
{
    AnalysisKind Kind { get; }
    Task<AnalysisOutput> ProcessAsync(AnalysisLease lease, CancellationToken ct);
}

public sealed class AnalysisFailure(string code, bool retryable) : Exception(code)
{
    public string Code { get; } = code;
    public bool Retryable { get; } = retryable;
}
