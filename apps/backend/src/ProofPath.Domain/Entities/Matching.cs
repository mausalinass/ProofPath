namespace ProofPath.Domain.Entities;

public sealed class MatchingScoringVersion
{
    public string Id { get; set; } = "";
    public string ConfigurationJson { get; set; } = "{}";
    public bool Frozen { get; set; }
    public DateTime CreatedAt { get; set; }
}
public sealed class MatchResult
{
    public Guid Id { get; set; }
    public Guid CandidateProfileId { get; set; }
    public Guid JobId { get; set; }
    public Guid RequirementSetId { get; set; }
    public string ScoringVersionId { get; set; } = "matching-v1";
    public string CandidateSnapshotJson { get; set; } = "{}";
    public string RequirementSnapshotJson { get; set; } = "{}";
    public string ResultJson { get; set; } = "{}";
    public decimal? OverallScore { get; set; }
    public string? OverallClassification { get; set; }
    public string OverallStatus { get; set; } = "Limited";
    public decimal OverallConfidence { get; set; }
    public decimal EvaluationCoverage { get; set; }
    public DateTime CreatedAt { get; set; }
}
public sealed class RequirementMatchRecord
{
    public Guid Id { get; set; }
    public Guid MatchResultId { get; set; }
    public Guid RequirementId { get; set; }
    public string RequirementSnapshotJson { get; set; } = "{}";
    public string ResultSnapshotJson { get; set; } = "{}";
}
public sealed class RequirementMatchEvidenceRecord
{
    public Guid Id { get; set; }
    public Guid RequirementMatchRecordId { get; set; }
    public Guid EvidenceItemId { get; set; }
    public string EvidenceSnapshotJson { get; set; } = "{}";
    public decimal Contribution { get; set; }
}
