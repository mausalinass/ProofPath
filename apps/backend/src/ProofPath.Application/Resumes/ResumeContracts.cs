using System.Text.Json;
using System.Text.Json.Serialization;
using ProofPath.Application.Analysis;

namespace ProofPath.Application.Resumes;

public sealed record SourceBlock(string Id, int? Page, string Text);
public sealed record DocumentText(SourceBlock[] Blocks, string[] Warnings);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FactDraft(string Kind, string Name, string? Organization, string? Detail,
    string? StartDateText, string? EndDateText, string? Status, string SourceBlockId, string Quote);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SkillDraft(string Term, string Context, string SourceBlockId, string Quote);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BehaviorDraft(string ThemeKey, string Statement, string SourceBlockId, string Quote);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ResumeDraft(FactDraft[] Facts, SkillDraft[] Skills, BehaviorDraft[] Behaviors);
public sealed record LlmResumeResult(ResumeDraft Draft, string Model, int InputTokens, int OutputTokens, long DurationMs);
public sealed record ResumeAnalysisResult(DocumentText Source, ResumeDraft Draft, string Model,
    int InputTokens, int OutputTokens, long DurationMs, string PromptVersion = "resume-prompt-v1",
    string SchemaVersion = "resume-schema-v1", string ExtractionVersion = "resume-extraction-v1");
public sealed record ResumeInput(Guid Id, string StorageKey, string ContentType);
public sealed record ResumeSummary(Guid Id, int Version, string FileName, long Length, string Status,
    Guid? AnalysisJobId, DateTime CreatedAt, DateTime? ConfirmedAt, bool Active);
public sealed record ResumeReview(Guid ResumeId, int Revision, ResumeAnalysisResult Machine,
    ResumeDraft Draft, DateTime? ConfirmedAt, bool Active);
public sealed record ResumeUploadResult(Guid Id, Guid AnalysisJobId);
public sealed record ResumeDownload(Stream Content, string ContentType, string FileName);
public sealed class ResumeProblem(string code, int status = 400) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
public interface IDocumentTextExtractor
{
    void Validate(byte[] content, string fileName, string contentType);
    DocumentText Extract(byte[] content, string contentType, CancellationToken ct);
}
public interface ILlmProvider
{
    Task<LlmResumeResult> ExtractResumeAsync(DocumentText source, CancellationToken ct);
}
public interface IResumeInputReader
{
    Task<ResumeInput?> GetInputAsync(Guid candidateProfileId, Guid resumeId, CancellationToken ct);
}
public interface IResumeWorkspace
{
    Task<ResumeUploadResult> UploadAsync(string userId, string fileName, string contentType, byte[] content, CancellationToken ct);
    Task<ResumeSummary[]> ListAsync(string userId, CancellationToken ct);
    Task<ResumeDownload?> DownloadAsync(string userId, Guid id, CancellationToken ct);
    Task<ResumeReview?> ReviewAsync(string userId, Guid id, CancellationToken ct);
    Task<ResumeReview> SaveAsync(string userId, Guid id, int revision, ResumeDraft draft, CancellationToken ct);
    Task ConfirmAsync(string userId, Guid id, int revision, CancellationToken ct);
}
public interface IPrivateFileCleanup { Task RunAsync(CancellationToken ct); }

public static class ResumeJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter() } };
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options) ?? throw new AnalysisFailure("INVALID_EXTRACTION", false);
}
