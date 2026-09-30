using ProofPath.Domain.Entities;
using ProofPath.Domain.Matching;

namespace ProofPath.Api.Tests;

public sealed class MatchingEngineTests
{
    private static readonly DateTime SnapshotAt = new(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SameSnapshotProducesSameResultRegardlessOfEvidenceOrder()
    {
        var a = Evidence("csharp", "Strong", "resume:1", "Projects");
        var b = Evidence("csharp", "Moderate", "repo:1", "Implementation");
        var first = MatchingEngine.Calculate(Candidate([a, b]), Job(Technical("csharp")));
        var second = MatchingEngine.Calculate(Candidate([b, a]), Job(Technical("csharp")));
        Assert.Equal(first.OverallScore, second.OverallScore);
        Assert.Equal(first.Requirements.Single().Score, second.Requirements.Single().Score);
        Assert.Equal("matching-v1", first.ScoringVersion);
    }

    [Fact]
    public void DuplicateEvidenceFromOneSourceDoesNotInflateLikeIndependentCorroboration()
    {
        var duplicate = MatchingEngine.Calculate(Candidate([
            Evidence("csharp", "Moderate", "resume:1", "Presence"),
            Evidence("csharp", "Moderate", "resume:1", "Presence")]), Job(Technical("csharp")));
        var independent = MatchingEngine.Calculate(Candidate([
            Evidence("csharp", "Moderate", "resume:1", "Presence"),
            Evidence("csharp", "Moderate", "repo:1", "Implementation")]), Job(Technical("csharp")));
        Assert.True(independent.Requirements.Single().Score > duplicate.Requirements.Single().Score);
    }

    [Fact]
    public void RelatedOnlyEvidenceIsWeakAndNeverEquivalentToExact()
    {
        var related = Evidence("dotnet", "Strong", "repo:1", "Implementation") with { RelatedToSkillIds = ["csharp"] };
        var relatedResult = MatchingEngine.Calculate(Candidate([related]), Job(Technical("csharp"))).Requirements.Single();
        var exactResult = MatchingEngine.Calculate(Candidate([Evidence("csharp", "Strong", "repo:1", "Implementation")]), Job(Technical("csharp"))).Requirements.Single();
        Assert.Equal(MatchSemanticRelation.RelatedOnly, relatedResult.Relation);
        Assert.Equal(MatchClassification.Weak, relatedResult.Classification);
        Assert.True(exactResult.Score > relatedResult.Score);
    }

    [Fact]
    public void NoEvidenceIsUncertainWithPartialCoverageAndMissingWithFullCoverage()
    {
        var uncertain = MatchingEngine.Calculate(Candidate([], .4), Job(Technical("csharp"))).Requirements.Single();
        var missing = MatchingEngine.Calculate(Candidate([], 1), Job(Technical("csharp"))).Requirements.Single();
        Assert.Equal(MatchEvaluationStatus.Uncertain, uncertain.EvaluationStatus);
        Assert.Null(uncertain.Classification);
        Assert.Equal(MatchClassification.Missing, missing.Classification);
    }

    [Fact]
    public void AnyOfKeepsOnlyBestAlternativeAndDoesNotCreateFalseGaps()
    {
        var requirements = new[] { Technical("react", "frontend", RequirementGroupType.AnyOf), Technical("vue", "frontend", RequirementGroupType.AnyOf) };
        var result = MatchingEngine.Calculate(Candidate([Evidence("react", "Strong", "repo:1", "Implementation")]), Job(requirements));
        Assert.Equal(MatchClassification.Strong, result.Requirements.Single(item => item.Key == "react").Classification);
        Assert.Equal(MatchEvaluationStatus.NotEvaluated, result.Requirements.Single(item => item.Key == "vue").EvaluationStatus);
        Assert.Empty(result.Gaps);
    }

    [Fact]
    public void CriticalRequiredMissingCapsClassificationAtPartial()
    {
        var critical = Technical("kubernetes") with { Importance = RequirementImportance.Critical };
        var preferred = Technical("csharp") with { Level = RequirementLevel.Preferred };
        var result = MatchingEngine.Calculate(Candidate([Evidence("csharp", "Strong", "resume:1", "Projects")], 1), Job([critical, preferred]));
        Assert.Contains("CRITICAL_REQUIRED_MISSING", result.Safeguards);
        Assert.NotNull(result.Classification);
        Assert.True(result.Classification >= OverallMatchClassification.PartialMatch);
    }

    [Fact]
    public void BehavioralEvidenceIsReportedSeparatelyAndNeverChangesScore()
    {
        var technical = Technical("csharp");
        var behavioral = new JobRequirementItemSnapshot(Guid.NewGuid(), "communication", RequirementCategory.Behavioral,
            RequirementLevel.Required, RequirementImportance.Critical, "Communicates well", null,
            "COMMUNICATION", [], null, RequirementGroupType.None, true, false);
        var without = MatchingEngine.Calculate(Candidate([Evidence("csharp", "Strong", "resume:1", "Projects")]), Job([technical, behavioral]));
        var withBehavior = MatchingEngine.Calculate(Candidate([Evidence("csharp", "Strong", "resume:1", "Projects")], behavior: [
            new(Guid.NewGuid(), "COMMUNICATION", "Led stakeholder reviews", "Explicit", "Strong", "Led reviews")]), Job([technical, behavioral]));
        Assert.Equal(without.OverallScore, withBehavior.OverallScore);
        Assert.True(withBehavior.BehavioralAssessment.Single().Observed);
        Assert.DoesNotContain(withBehavior.Components, component => component.Name.Contains("Behavior", StringComparison.Ordinal));
    }

    [Fact]
    public void ExperienceUsesConfirmedProfessionalFactsAndFrozenSnapshotDate()
    {
        var experience = new JobRequirementItemSnapshot(Guid.NewGuid(), "experience", RequirementCategory.Experience,
            RequirementLevel.Required, RequirementImportance.High, "Three years experience", null, null,
            ["3 years"], null, RequirementGroupType.None, true, true);
        var candidate = Candidate([], experiences: [new(Guid.NewGuid(), "Experience", "Engineer", "Acme", null,
            "2022-01-01", "2025-01-01", null, "Engineer")]);
        var result = MatchingEngine.Calculate(candidate, Job(experience)).Requirements.Single();
        Assert.Equal(MatchClassification.Strong, result.Classification);
        Assert.Equal(100, result.Score);
    }

    [Fact]
    public void StaleStrongEvidenceScoresBelowActiveStrongEvidence()
    {
        var active = Evidence("csharp", "Strong", "repo:1", "Implementation");
        var stale = active with { Id = Guid.NewGuid(), Lifecycle = "Stale" };
        var activeResult = MatchingEngine.Calculate(Candidate([active]), Job(Technical("csharp"))).Requirements.Single();
        var staleResult = MatchingEngine.Calculate(Candidate([stale]), Job(Technical("csharp"))).Requirements.Single();
        Assert.True(activeResult.Score > staleResult.Score);
        Assert.Equal(MatchClassification.Moderate, staleResult.Classification);
    }

    [Fact]
    public void MissingEducationRequirementMakesEducationComponentNotApplicable()
    {
        var result = MatchingEngine.Calculate(Candidate([Evidence("csharp", "Strong", "resume:1", "Projects")]), Job(Technical("csharp")));
        Assert.Equal(MatchComponentStatus.NotApplicable, result.Components.Single(item => item.Name == "Education").Status);
    }

    [Fact]
    public void DegreeInProgressReceivesPartialCredit()
    {
        var requirement = new JobRequirementItemSnapshot(Guid.NewGuid(), "degree", RequirementCategory.EducationCredential,
            RequirementLevel.Required, RequirementImportance.High, "Bachelor degree", null, null, [], null,
            RequirementGroupType.None, true, true);
        var education = new CandidateFactSnapshot(Guid.NewGuid(), "Education", "Bachelor of Science", "University",
            "Computer Science degree", "2024", null, "InProgress", "Bachelor of Science");
        var candidate = new CandidateEvidenceSnapshot(Guid.NewGuid(), SnapshotAt, 1, [], [], [education], [], [], "candidate-r1");
        var match = MatchingEngine.Calculate(candidate, Job(requirement)).Requirements.Single();
        Assert.Equal(65, match.Score);
        Assert.Equal(MatchClassification.Moderate, match.Classification);
        Assert.Equal("EDUCATION_IN_PROGRESS", match.ReasonCode);
    }

    [Fact]
    public void LowCoverageSuppressesOverallClassification()
    {
        var result = MatchingEngine.Calculate(Candidate([], .25), Job(Technical("csharp")));
        Assert.Equal(OverallMatchStatus.Limited, result.Status);
        Assert.Null(result.OverallScore);
        Assert.Null(result.Classification);
    }

    [Fact]
    public void UnresolvedSkillIsNotEvaluatedRatherThanMissing()
    {
        var unresolved = Technical("unknown") with { SkillId = null };
        var match = MatchingEngine.Calculate(Candidate([], 1), Job(unresolved)).Requirements.Single();
        Assert.Equal(MatchEvaluationStatus.NotEvaluated, match.EvaluationStatus);
        Assert.Null(match.Classification);
        Assert.Equal("UNRESOLVED_REQUIREMENT", match.ReasonCode);
    }
    [Fact]
    public void TechnicalComponentUsesRequiredEightyPreferredTwentyAndTracksRequiredCoverage()
    {
        var required = Technical("kubernetes");
        var preferred = Technical("csharp") with { Level = RequirementLevel.Preferred };
        var result = MatchingEngine.Calculate(Candidate([Evidence("csharp", "Strong", "resume:1", "Implementation")], 1), Job([required, preferred]));
        Assert.Equal(20, result.Components.Single(item => item.Name == "Technical").Score);
        Assert.Equal(0, result.RequiredCoverage);
    }

    [Fact]
    public void ExperienceComponentRenormalizesSixtyTwentyFiveFifteenDimensions()
    {
        JobRequirementItemSnapshot Experience(string key, string wording, string[] qualifiers) => new(Guid.NewGuid(), key,
            RequirementCategory.Experience, RequirementLevel.Required, RequirementImportance.Medium, wording, null, null,
            qualifiers, null, RequirementGroupType.None, true, true);
        var requirements = new[]
        {
            Experience("years", "Four years professional experience", ["4 years"]),
            Experience("seniority", "Senior engineer", []),
            Experience("responsibilities", "Design distributed systems", [])
        };
        var fact = new CandidateFactSnapshot(Guid.NewGuid(), "Experience", "Senior Engineer", "Acme",
            "Designed distributed systems", "2024-09-29", "2026-09-29", null, "Senior Engineer");
        var result = MatchingEngine.Calculate(Candidate([], 1, [fact]), Job(requirements));
        var score = result.Components.Single(item => item.Name == "Experience").Score;
        Assert.InRange(score!.Value, 69, 71);
    }

    [Fact]
    public void KeywordOnlyJuniorScoresBelowImplementationEvidenceJunior()
    {
        var requirement = Technical("csharp");
        var keywordOnly = MatchingEngine.Calculate(
            Candidate([Evidence("csharp", "Weak", "resume:skills", "Presence")]), Job(requirement));
        var implementation = MatchingEngine.Calculate(
            Candidate([Evidence("csharp", "Strong", "repo:api", "Implementation")]), Job(requirement));

        Assert.True(keywordOnly.OverallScore < implementation.OverallScore);
        Assert.True(keywordOnly.Requirements.Single().Score < implementation.Requirements.Single().Score);
    }

    [Fact]
    public void StrongGitHubEvidenceDoesNotManufactureProfessionalExperience()
    {
        var experience = new JobRequirementItemSnapshot(Guid.NewGuid(), "experience", RequirementCategory.Experience,
            RequirementLevel.Required, RequirementImportance.High, "Three years professional experience", null, null,
            ["3 years"], null, RequirementGroupType.None, true, true);
        var result = MatchingEngine.Calculate(
            Candidate([Evidence("csharp", "Strong", "repo:api", "Implementation")]),
            Job([Technical("csharp"), experience]));

        Assert.Equal(100, result.Components.Single(item => item.Name == "Technical").Score);
        Assert.Equal(0, result.Components.Single(item => item.Name == "Experience").Score);
        Assert.Equal(MatchClassification.Missing,
            result.Requirements.Single(item => item.Category == RequirementCategory.Experience).Classification);
    }

    [Fact]
    public void ProfessionalExperienceCanBeStrongWithoutGitHubEvidence()
    {
        var experience = new JobRequirementItemSnapshot(Guid.NewGuid(), "experience", RequirementCategory.Experience,
            RequirementLevel.Required, RequirementImportance.High, "One year professional experience", null, null,
            ["1 year"], null, RequirementGroupType.None, true, true);
        var fact = new CandidateFactSnapshot(Guid.NewGuid(), "Experience", "Software Engineer", "Acme",
            "Maintained production services", "2024-01-01", "2026-01-01", null, "Software Engineer at Acme");
        var result = MatchingEngine.Calculate(Candidate([], 1, [fact]), Job(experience));

        Assert.Equal(100, result.Components.Single(item => item.Name == "Experience").Score);
        Assert.Equal(MatchComponentStatus.NotApplicable,
            result.Components.Single(item => item.Name == "Evidence").Status);
    }

    [Fact]
    public void BalancedJuniorProducesEvaluatedTechnicalAndExperienceComponents()
    {
        var experience = new JobRequirementItemSnapshot(Guid.NewGuid(), "experience", RequirementCategory.Experience,
            RequirementLevel.Required, RequirementImportance.Medium, "One year professional experience", null, null,
            ["1 year"], null, RequirementGroupType.None, true, true);
        var fact = new CandidateFactSnapshot(Guid.NewGuid(), "Experience", "Junior Engineer", "Acme",
            "Built APIs", "2025-01-01", "2026-09-29", null, "Junior Engineer at Acme");
        var result = MatchingEngine.Calculate(
            Candidate([Evidence("csharp", "Moderate", "repo:api", "Implementation")], 1, [fact]),
            Job([Technical("csharp"), experience]));

        Assert.Equal(OverallMatchStatus.Complete, result.Status);
        Assert.NotNull(result.Components.Single(item => item.Name == "Technical").Score);
        Assert.NotNull(result.Components.Single(item => item.Name == "Experience").Score);
        Assert.NotNull(result.OverallScore);
    }

    [Fact]
    public void SameJuniorExperienceScoresLowerAgainstSeniorRequirement()
    {
        JobRequirementItemSnapshot Experience(string key, string wording, string qualifier) =>
            new(Guid.NewGuid(), key, RequirementCategory.Experience, RequirementLevel.Required,
                RequirementImportance.High, wording, null, null, [qualifier], null,
                RequirementGroupType.None, true, true);
        var fact = new CandidateFactSnapshot(Guid.NewGuid(), "Experience", "Junior Engineer", "Acme",
            "Built production APIs", "2025-01-01", "2026-09-29", null, "Junior Engineer at Acme");
        var candidate = Candidate([], 1, [fact]);
        var junior = MatchingEngine.Calculate(candidate, Job(Experience("junior-years", "One year experience", "1 year")));
        var senior = MatchingEngine.Calculate(candidate, Job(Experience("senior-years", "Five years experience", "5 years")));

        Assert.True(junior.OverallScore > senior.OverallScore);
    }

    [Fact]
    public void CriticalRequiredUncertainCapsClassificationAndConfidence()
    {
        var critical = Technical("kubernetes") with { Importance = RequirementImportance.Critical };
        var result = MatchingEngine.Calculate(Candidate([], .75), Job(critical));

        Assert.Contains("CRITICAL_REQUIRED_UNCERTAIN", result.Safeguards);
        Assert.NotEqual(OverallMatchClassification.StrongMatch, result.Classification);
        Assert.True(result.OverallConfidence <= .69);
    }
    private static CandidateEvidenceSnapshot Candidate(CandidateEvidenceItemSnapshot[] evidence, double coverage = 1,
        CandidateFactSnapshot[]? experiences = null, BehavioralEvidenceSnapshot[]? behavior = null) =>
        new(Guid.NewGuid(), SnapshotAt, coverage, evidence, experiences ?? [], [], [], behavior ?? [], "candidate-r1");
    private static CandidateEvidenceItemSnapshot Evidence(string skill, string strength, string source, string type) =>
        new(Guid.NewGuid(), skill, strength, 1, "Active", source, type, $"Used {skill}", source, []);
    private static JobRequirementSnapshot Job(params JobRequirementItemSnapshot[] requirements) =>
        new(Guid.NewGuid(), Guid.NewGuid(), 1, requirements, "job-r1");
    private static JobRequirementItemSnapshot Technical(string skill, string? group = null,
        RequirementGroupType groupType = RequirementGroupType.None) => new(Guid.NewGuid(), skill,
            RequirementCategory.TechnicalSkill, RequirementLevel.Required, RequirementImportance.High,
            skill, skill, null, [], group, groupType, true, true);
}
