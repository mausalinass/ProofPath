using ProofPath.Application.Analysis;
using ProofPath.Application.Files;
using ProofPath.Domain.Entities;

namespace ProofPath.Application.Resumes;

public sealed class ResumeAnalysisHandler(IResumeInputReader inputs, IPrivateFileStore files,
    IDocumentTextExtractor extractor, ILlmProvider provider) : IAnalysisHandler
{
    public AnalysisKind Kind => AnalysisKind.Resume;
    public async Task<AnalysisOutput> ProcessAsync(AnalysisLease lease, CancellationToken ct)
    {
        var input = await inputs.GetInputAsync(lease.CandidateProfileId, lease.ResourceId, ct)
            ?? throw new AnalysisFailure("RESOURCE_REMOVED", false);
        await using var stream = await files.OpenReadAsync(input.StorageKey, ct)
            ?? throw new AnalysisFailure("FILE_UNAVAILABLE", true);
        using var buffer = new MemoryStream(); var chunk = new byte[32768]; int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > ResumeLimits.MaxBytes) throw new AnalysisFailure("FILE_TOO_LARGE", false);
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        var source = extractor.Extract(buffer.ToArray(), input.ContentType, ct);
        var result = await provider.ExtractResumeAsync(source, ct);
        ResumeValidation.Validate(result.Draft with { Behaviors = [] }, source, machine: true);
        var behaviors = new List<BehaviorDraft>();
        foreach (var behavior in result.Draft.Behaviors ?? [])
        {
            try { ResumeValidation.Validate(new ResumeDraft([], [], [behavior]), source, machine: true); behaviors.Add(behavior); }
            catch (AnalysisFailure) { source = source with { Warnings = [.. source.Warnings, "An unsupported behavioral statement was omitted. Technical facts remain available for review."] }; }
        }
        result = result with { Draft = result.Draft with { Behaviors = behaviors.Distinct().ToArray() } };
        return new AnalysisOutput(ResumeJson.Serialize(new ResumeAnalysisResult(source, result.Draft, result.Model,
            result.InputTokens, result.OutputTokens, result.DurationMs)), source.Warnings.Length > 0);
    }
}
