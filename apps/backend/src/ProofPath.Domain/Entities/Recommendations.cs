namespace ProofPath.Domain.Entities;

public enum ApplicationStage { Saved, Preparing, Applied, Interviewing, Offer, Rejected, Withdrawn }
public enum RecommendationStatus { Open, InProgress, Completed, Dismissed }

public sealed class JobTracking
{
    public Guid JobId { get; set; }
    public ApplicationStage Stage { get; set; } = ApplicationStage.Saved;
    public string? Notes { get; set; }
    public DateTime? NextActionAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class MatchRecommendation
{
    public Guid Id { get; set; }
    public Guid MatchResultId { get; set; }
    public Guid? RequirementId { get; set; }
    public int Rank { get; set; }
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Rationale { get; set; } = "";
    public string Action { get; set; } = "";
    public RecommendationStatus Status { get; set; } = RecommendationStatus.Open;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
