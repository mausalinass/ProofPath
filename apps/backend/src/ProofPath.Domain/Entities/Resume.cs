namespace ProofPath.Domain.Entities;

public sealed class Resume
{
    public Guid Id { get; set; }
    public Guid CandidateProfileId { get; set; }
    public int Version { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Length { get; set; }
    public string Status { get; set; } = "Uploading";
    public Guid? AnalysisJobId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ResumeExtraction
{
    public Guid Id { get; set; }
    public Guid ResumeId { get; set; }
    public string MachineJson { get; set; } = "{}";
    public string DraftJson { get; set; } = "{}";
    public int Revision { get; set; } = 1;
    public string PromptVersion { get; set; } = "resume-prompt-v1";
    public string SchemaVersion { get; set; } = "resume-schema-v1";
    public string ExtractionVersion { get; set; } = "resume-extraction-v1";
    public string Model { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public bool Active { get; set; }
}

// Facts remain relational and independent from private file contents. Provenance points to
// an immutable confirmed extraction; newer versions never mutate historical facts.
public abstract class CandidateFact
{
    public Guid Id { get; set; }
    public Guid CandidateProfileId { get; set; }
    public Guid? ResumeExtractionId { get; set; }
    public string Name { get; set; } = "";
    public string? Organization { get; set; }
    public string? Detail { get; set; }
    public string? StartDateText { get; set; }
    public string? EndDateText { get; set; }
    public string? Status { get; set; }
    public string SourceBlockId { get; set; } = "";
    public string Quote { get; set; } = "";
    public bool UserCorrected { get; set; }
}
public sealed class Experience : CandidateFact { }
public sealed class Education : CandidateFact { }
public sealed class Credential : CandidateFact { }
public sealed class Project : CandidateFact
{
    public Guid? RepositoryId { get; set; }
    public string? SourceUrl { get; set; }
}

public sealed class Skill
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
}
public sealed class SkillAlias
{
    public string Alias { get; set; } = "";
    public string SkillId { get; set; } = "";
}
public sealed class EvidenceItem
{
    public Guid Id { get; set; }
    public Guid CandidateProfileId { get; set; }
    public Guid? ResumeExtractionId { get; set; }
    public Guid? RepositoryAnalysisId { get; set; }
    public Guid? ProjectId { get; set; }
    public string? SkillId { get; set; }
    public string OriginalTerm { get; set; } = "";
    public string Context { get; set; } = "";
    public string Strength { get; set; } = "Weak";
    public decimal ExtractionConfidence { get; set; } = 1m;
    public string EvidenceType { get; set; } = "Presence";
    public string Lifecycle { get; set; } = "Active";
    public string SourceBlockId { get; set; } = "";
    public string Quote { get; set; } = "";
    public bool UserCorrected { get; set; }
    public DateTime ObservedAt { get; set; }
    public string? RevisionSha { get; set; }
    public string? SourcePath { get; set; }
    public int? StartLine { get; set; }
    public int? EndLine { get; set; }
    public string? Detector { get; set; }
    public string? DetectorVersion { get; set; }
}
public sealed class BehavioralEvidenceItem
{
    public Guid Id { get; set; }
    public Guid CandidateProfileId { get; set; }
    public Guid ResumeExtractionId { get; set; }
    public string ThemeKey { get; set; } = "";
    public string Statement { get; set; } = "";
    public string Basis { get; set; } = "Explicit";
    public string Strength { get; set; } = "Weak";
    public string SourceBlockId { get; set; } = "";
    public string Quote { get; set; } = "";
    public bool UserCorrected { get; set; }
}
// No user identifier or document text: survives account deletion until storage cleanup succeeds.
public sealed class PrivateFileDeletion
{
    public Guid Id { get; set; }
    public string StorageKey { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public int Attempts { get; set; }
}
