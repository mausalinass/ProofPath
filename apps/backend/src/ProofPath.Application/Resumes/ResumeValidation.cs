using ProofPath.Application.Analysis;
using ProofPath.Domain.Entities;

namespace ProofPath.Application.Resumes;

public static class ResumeValidation
{
    public static readonly string[] Themes = ["COLLABORATION", "CROSS_FUNCTIONAL_COLLABORATION", "COMMUNICATION",
        "STAKEHOLDER_COMMUNICATION", "OWNERSHIP", "LEADERSHIP", "INITIATIVE", "ADAPTABILITY", "PROBLEM_SOLVING", "MENTORING"];
    private static readonly string[] Contexts = ["SkillsSection", "ExperienceStatement", "ProjectStatement", "Education", "Certification", "Other"];
    public static void Validate(ResumeDraft draft, DocumentText source, bool machine)
    {
        if (draft is null || draft.Facts is null || draft.Skills is null || draft.Behaviors is null ||
            draft.Facts.Length > 200 || draft.Skills.Length > 300 || draft.Behaviors.Length > 100) Fail();
        var blocks = source.Blocks.ToDictionary(block => block.Id, block => block.Text);
        void Ground(string id, string quote)
        {
            if (string.IsNullOrWhiteSpace(quote) || !blocks.TryGetValue(id, out var text) || !text.Contains(quote, StringComparison.Ordinal)) Fail();
        }
        void Text(string? value, bool required = false)
        {
            if (required && string.IsNullOrWhiteSpace(value) || value?.Length > 10000) Fail();
        }
        foreach (var fact in draft.Facts!)
        {
            if (fact is null || fact.Kind is not ("Experience" or "Education" or "Project" or "Credential")) Fail();
            Text(fact!.Name, true); Text(fact.Organization); Text(fact.Detail); Text(fact.StartDateText); Text(fact.EndDateText); Text(fact.Status);
            Ground(fact.SourceBlockId, fact.Quote);
            // Machine values must be verbatim, grounded spans. User edits remain separately identified.
            if (machine)
                foreach (var field in new[] { fact.Name, fact.Organization, fact.Detail, fact.StartDateText, fact.EndDateText, fact.Status })
                    if (field is not null && !blocks[fact.SourceBlockId].Contains(field, StringComparison.Ordinal)) Fail();
        }
        foreach (var skill in draft.Skills!)
        {
            if (skill is null || !Contexts.Contains(skill.Context)) Fail();
            Text(skill!.Term, true); Ground(skill.SourceBlockId, skill.Quote);
            if (machine && !skill.Quote.Contains(skill.Term, StringComparison.OrdinalIgnoreCase)) Fail();
        }
        foreach (var behavior in draft.Behaviors!)
        {
            if (behavior is null || !Themes.Contains(behavior.ThemeKey)) Fail();
            Text(behavior!.Statement, true); Ground(behavior.SourceBlockId, behavior.Quote);
            if (BehaviorEvidenceRules.Strength(behavior.ThemeKey, behavior.Statement) is null) Fail();
            if (machine && !behavior.Quote.Contains(behavior.Statement, StringComparison.Ordinal)) Fail();
        }
    }
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail() => throw new AnalysisFailure("INVALID_EXTRACTION", false);
}
