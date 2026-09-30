namespace ProofPath.Domain.Entities;

public enum AnalysisState { Pending, Processing, Completed, PartiallyCompleted, Failed, Cancelled }
public enum AnalysisKind { Resume, Repository, JobDescription }

public sealed class AnalysisJob
{
    public Guid Id { get; set; }
    public Guid CandidateProfileId { get; set; }
    public AnalysisKind Kind { get; set; }
    public Guid ResourceId { get; set; }
    public string InputVersion { get; set; } = string.Empty;
    public AnalysisState State { get; set; }
    public int Attempts { get; set; }
    public DateTime AvailableAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public string? ResultJson { get; set; }
    public string? ErrorCode { get; set; }
    public bool Retryable { get; set; }
}
