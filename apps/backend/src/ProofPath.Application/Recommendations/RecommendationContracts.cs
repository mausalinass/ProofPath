using ProofPath.Domain.Entities;

namespace ProofPath.Application.Recommendations;

public sealed record JobTrackingView(Guid JobId, ApplicationStage Stage, string? Notes,
    DateTime? NextActionAt, DateTime UpdatedAt);
public sealed record JobTrackingUpdate(ApplicationStage Stage, string? Notes, DateTime? NextActionAt);
public sealed record RecommendationView(Guid Id, Guid MatchResultId, Guid? RequirementId, int Rank,
    string Kind, string Title, string Rationale, string Action, RecommendationStatus Status,
    DateTime CreatedAt, DateTime UpdatedAt);
public sealed record RecommendationUpdate(RecommendationStatus Status);
public sealed record ScoreHistoryPoint(Guid MatchResultId, DateTime CreatedAt, double? Score,
    double? Delta, string? Classification, string Status, double Coverage);

public sealed class RecommendationProblem(string code, int status = 400) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public interface IRecommendationWorkspace
{
    Task<JobTrackingView> TrackingAsync(string userId, Guid jobId, CancellationToken ct);
    Task<JobTrackingView> UpdateTrackingAsync(string userId, Guid jobId, JobTrackingUpdate update, CancellationToken ct);
    Task<RecommendationView[]> RecommendationsAsync(string userId, Guid jobId, CancellationToken ct);
    Task<RecommendationView> UpdateRecommendationAsync(string userId, Guid jobId, Guid recommendationId,
        RecommendationUpdate update, CancellationToken ct);
    Task<ScoreHistoryPoint[]> ScoreHistoryAsync(string userId, Guid jobId, CancellationToken ct);
}
