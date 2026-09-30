using ProofPath.Application.Analysis;
using ProofPath.Application.Resumes;
using ProofPath.Domain.Entities;

namespace ProofPath.Application.Jobs;

public static class JobDescriptionPreprocessor
{
    public static JobDescriptionSource Extract(string description)
    {
        var all = description.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            .Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
        var lines = all.Take(500).Select((line, index) => new JobSourceBlock($"jd-{index + 1}", line)).ToArray();
        if (lines.Length == 0) throw new AnalysisFailure("JOB_DESCRIPTION_INVALID", false);
        var warnings = all.Length > 500 ? new[] { "Only the first 500 non-empty lines were analyzed." } : [];
        return new(lines, warnings);
    }
}

public static class JobRequirementValidation
{
    private static readonly HashSet<string> Themes = new(ResumeValidation.Themes, StringComparer.Ordinal);
    public static void Validate(JobRequirementDraftSet draft, JobDescriptionSource source, bool machine)
    {
        if (draft?.Requirements is null || draft.Requirements.Length is 0 or > 300) Fail();
        var blocks = source.Blocks.ToDictionary(block => block.Id, block => block.Text);
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in draft.Requirements)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Key) || !keys.Add(item.Key) ||
                string.IsNullOrWhiteSpace(item.OriginalWording) || item.OriginalWording.Length > 4000 ||
                string.IsNullOrWhiteSpace(item.Quote) || !blocks.TryGetValue(item.SourceBlockId, out var block) ||
                !block.Contains(item.Quote, StringComparison.Ordinal) || item.Qualifiers is null || item.Qualifiers.Length > 20 ||
                item.Qualifiers.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 500) ||
                item.State == RequirementState.Confirmed) Fail();
            if (machine && !item.Quote.Contains(item.OriginalWording, StringComparison.OrdinalIgnoreCase)) Fail();
            if (item.Category == RequirementCategory.TechnicalSkill && string.IsNullOrWhiteSpace(item.SkillTerm)) Fail();
            if (item.Category != RequirementCategory.TechnicalSkill && (item.SkillTerm is not null || item.SkillId is not null ||
                item.NormalizationStatus != RequirementNormalizationStatus.NotApplicable)) Fail();
            if (item.Category == RequirementCategory.Behavioral && (item.BehavioralThemeKey is null || !Themes.Contains(item.BehavioralThemeKey))) Fail();
            if (item.Category != RequirementCategory.Behavioral && item.BehavioralThemeKey is not null) Fail();
            if (item.GroupType == RequirementGroupType.None && item.GroupKey is not null ||
                item.GroupType != RequirementGroupType.None && string.IsNullOrWhiteSpace(item.GroupKey)) Fail();
        }
    }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail() => throw new AnalysisFailure("INVALID_JOB_EXTRACTION", false);
}

public sealed class JobAnalysisHandler(IJobInputReader inputs, IJobRequirementProvider provider,
    IJobRequirementNormalizer normalizer) : IAnalysisHandler
{
    public AnalysisKind Kind => AnalysisKind.JobDescription;
    public async Task<AnalysisOutput> ProcessAsync(AnalysisLease lease, CancellationToken ct)
    {
        var input = await inputs.GetInputAsync(lease.CandidateProfileId, lease.ResourceId, ct)
            ?? throw new AnalysisFailure("RESOURCE_REMOVED", false);
        if (lease.InputVersion != $"job-description-v{input.Version}") throw new AnalysisFailure("RESOURCE_CHANGED", false);
        var source = JobDescriptionPreprocessor.Extract(input.Description);
        var result = await provider.ExtractAsync(source, await normalizer.CatalogAsync(ct), ct);
        JobRequirementValidation.Validate(result.Draft, source, machine: true);
        var normalized = await normalizer.NormalizeAsync(result.Draft, ct);
        JobRequirementValidation.Validate(normalized, source, machine: true);
        var analysis = new JobAnalysisResult(source, normalized, result.Model, result.InputTokens, result.OutputTokens, result.DurationMs);
        return new(ResumeJson.Serialize(analysis), source.Warnings.Length > 0);
    }
}
