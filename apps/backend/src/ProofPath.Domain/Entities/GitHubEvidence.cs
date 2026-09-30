namespace ProofPath.Domain.Entities;

public enum RepositoryScanStatus
{
    NeverScanned, Pending, Processing, Completed, CompletedLimited,
    FailedRetryable, FailedPermanent, AccessLost, Cancelled
}

public sealed class Repository
{
    public Guid Id { get; set; }
    public Guid CandidateProfileId { get; set; }
    public Guid ConnectedAccountId { get; set; }
    public long GitHubId { get; set; }
    public string Owner { get; set; } = "";
    public string Name { get; set; } = "";
    public string FullName { get; set; } = "";
    public bool Private { get; set; }
    public string DefaultBranch { get; set; } = "";
    public string HtmlUrl { get; set; } = "";
    public bool IncludedForAnalysis { get; set; }
    public DateTime LastSyncedAt { get; set; }
    public DateTime? LastScanAt { get; set; }
    public string? LastRevisionSha { get; set; }
    public RepositoryScanStatus ScanStatus { get; set; }
    public string CoverageJson { get; set; } = "{}";
}

public sealed class RepositoryAnalysis
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid AnalysisJobId { get; set; }
    public string RevisionSha { get; set; } = "";
    public string ExtractionVersion { get; set; } = "github-extraction-v1";
    public string AnalysisPolicyVersion { get; set; } = "github-policy-v1";
    public string ScanIdentity { get; set; } = "";
    public RepositoryScanStatus Status { get; set; }
    public string CoverageJson { get; set; } = "{}";
    public string WarningsJson { get; set; } = "[]";
    public string ResultJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
