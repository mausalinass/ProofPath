using System.Text.Json.Serialization;
using ProofPath.Domain.Entities;

namespace ProofPath.Application.Jobs;

public static class JobLimits
{
    public const int MinDescriptionCharacters = 100;
    public const int MaxDescriptionCharacters = 50_000;
}

public sealed record JobSourceBlock(string Id, string Text);
public sealed record JobDescriptionSource(JobSourceBlock[] Blocks, string[] Warnings);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record JobRequirementDraft(string Key, RequirementCategory Category, RequirementLevel Level,
    RequirementImportance Importance, RequirementState State, string OriginalWording, string? SkillTerm,
    string? SkillId, RequirementNormalizationStatus NormalizationStatus, string? BehavioralThemeKey,
    string[] Qualifiers, string? GroupKey, RequirementGroupType GroupType, string SourceBlockId, string Quote);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record JobRequirementDraftSet(JobRequirementDraft[] Requirements);

public sealed record LlmJobResult(JobRequirementDraftSet Draft, string Model, int InputTokens, int OutputTokens, long DurationMs);
public sealed record JobAnalysisResult(JobDescriptionSource Source, JobRequirementDraftSet Draft, string Model,
    int InputTokens, int OutputTokens, long DurationMs, string PromptVersion = "job-prompt-v1",
    string SchemaVersion = "job-schema-v1", string ExtractionVersion = "job-extraction-v1",
    string NormalizationPolicyVersion = "job-normalization-v1");
public sealed record JobSummary(Guid Id, string? Company, string? Title, string Description, string? SourceUrl,
    int DescriptionVersion, string Status, Guid? AnalysisJobId, DateTime CreatedAt, DateTime UpdatedAt,
    int? ConfirmedRequirementSetVersion);
public sealed record JobReview(Guid JobId, int DescriptionVersion, int Revision, JobAnalysisResult Machine,
    JobRequirementDraftSet Draft, bool Outdated, DateTime? ConfirmedAt);
public sealed record JobWrite(string? Company, string? Title, string Description, string? SourceUrl, int? ExpectedDescriptionVersion = null);
public sealed record JobCreateResult(JobSummary Job, Guid AnalysisJobId);
public sealed record SkillOption(string Id, string DisplayName);

public sealed class JobProblem(string code, int status = 400) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public interface IJobRequirementProvider
{
    Task<LlmJobResult> ExtractAsync(JobDescriptionSource source, IReadOnlyList<SkillOption> catalog, CancellationToken ct);
}
public interface IJobRequirementNormalizer
{
    Task<SkillOption[]> CatalogAsync(CancellationToken ct);
    Task<JobRequirementDraftSet> NormalizeAsync(JobRequirementDraftSet draft, CancellationToken ct);
}
public interface IJobInputReader
{
    Task<(string Description, int Version)?> GetInputAsync(Guid candidateProfileId, Guid jobId, CancellationToken ct);
}
public interface IJobWorkspace
{
    Task<JobCreateResult> CreateAsync(string userId, JobWrite input, CancellationToken ct);
    Task<JobSummary[]> ListAsync(string userId, CancellationToken ct);
    Task<JobSummary?> GetAsync(string userId, Guid id, CancellationToken ct);
    Task<JobCreateResult> UpdateAsync(string userId, Guid id, JobWrite input, CancellationToken ct);
    Task<JobReview?> ReviewAsync(string userId, Guid id, CancellationToken ct);
    Task<JobReview> SaveReviewAsync(string userId, Guid id, int revision, JobRequirementDraftSet draft, CancellationToken ct);
    Task ConfirmAsync(string userId, Guid id, int revision, CancellationToken ct);
    Task<SkillOption[]> SkillsAsync(CancellationToken ct);
}
