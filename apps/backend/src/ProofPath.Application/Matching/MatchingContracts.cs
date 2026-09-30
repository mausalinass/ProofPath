using ProofPath.Domain.Matching;

namespace ProofPath.Application.Matching;

public sealed record MatchView(Guid Id, Guid JobId, Guid RequirementSetId, string ScoringVersion,
    DateTime CreatedAt, MatchResultDraft Result);
public sealed record MatchSummary(Guid Id, DateTime CreatedAt, string ScoringVersion,
    double? OverallScore, OverallMatchClassification? Classification, OverallMatchStatus Status,
    double EvaluationCoverage);

public sealed class MatchingProblem(string code, int status = 400) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public interface IMatchingWorkspace
{
    Task<MatchView> CalculateAsync(string userId, Guid jobId, CancellationToken ct);
    Task<MatchView?> LatestAsync(string userId, Guid jobId, CancellationToken ct);
    Task<MatchView?> GetAsync(string userId, Guid jobId, Guid matchId, CancellationToken ct);
    Task<MatchSummary[]> ListAsync(string userId, Guid jobId, CancellationToken ct);
}
