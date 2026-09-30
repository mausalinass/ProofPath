namespace ProofPath.Domain.Entities;

public enum JobStatus { Processing, ReadyForReview, Confirmed, Failed }
public enum RequirementCategory { TechnicalSkill, Experience, EducationCredential, Behavioral, Contextual }
public enum RequirementLevel { Required, Preferred, Unspecified }
public enum RequirementImportance { Critical, High, Medium, Low }
public enum RequirementState { Extracted, Confirmed, Excluded }
public enum RequirementGroupType { None, AnyOf, AllOf }
public enum RequirementNormalizationStatus { NotApplicable, Exact, Alias, Equivalent, Suggested, UserConfirmed, Unresolved }

public sealed class Job
{
    public Guid Id { get; set; }
    public Guid CandidateProfileId { get; set; }
    public string? Company { get; set; }
    public string? Title { get; set; }
    public string Description { get; set; } = "";
    public string? SourceUrl { get; set; }
    public int DescriptionVersion { get; set; } = 1;
    public JobStatus Status { get; set; } = JobStatus.Processing;
    public Guid? AnalysisJobId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class JobRequirementExtraction
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public int DescriptionVersion { get; set; }
    public int Revision { get; set; } = 1;
    public string MachineJson { get; set; } = "{}";
    public string DraftJson { get; set; } = "{}";
    public string PromptVersion { get; set; } = "job-prompt-v1";
    public string SchemaVersion { get; set; } = "job-schema-v1";
    public string ExtractionVersion { get; set; } = "job-extraction-v1";
    public string NormalizationPolicyVersion { get; set; } = "job-normalization-v1";
    public string Model { get; set; } = "";
    public bool Outdated { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}

public sealed class RequirementSet
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid JobRequirementExtractionId { get; set; }
    public int Version { get; set; }
    public bool Active { get; set; }
    public DateTime ConfirmedAt { get; set; }
}

public sealed class JobRequirement
{
    public Guid Id { get; set; }
    public Guid RequirementSetId { get; set; }
    public string Key { get; set; } = "";
    public RequirementCategory Category { get; set; }
    public RequirementLevel Level { get; set; }
    public RequirementImportance Importance { get; set; }
    public RequirementState State { get; set; }
    public string OriginalWording { get; set; } = "";
    public string? SkillTerm { get; set; }
    public string? SkillId { get; set; }
    public RequirementNormalizationStatus NormalizationStatus { get; set; }
    public string? BehavioralThemeKey { get; set; }
    public string QualifiersJson { get; set; } = "[]";
    public string? GroupKey { get; set; }
    public RequirementGroupType GroupType { get; set; }
    public string SourceBlockId { get; set; } = "";
    public string Quote { get; set; } = "";
    public bool IsEvaluable { get; set; }
    public bool IsScoreEligible { get; set; }
    public bool UserCorrected { get; set; }
}
