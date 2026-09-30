using ProofPath.Domain.Entities;

namespace ProofPath.Domain.Matching;

public enum MatchEvaluationStatus { Evaluated, Uncertain, NotEvaluated }
public enum MatchClassification { Strong, Moderate, Weak, Missing }
public enum MatchComponentStatus { Applicable, NotApplicable, InsufficientInformation }
public enum OverallMatchClassification { StrongMatch, GoodMatch, PartialMatch, WeakMatch, LowMatch }
public enum OverallMatchStatus { Complete, CoverageWarning, Limited }
public enum MatchConfidenceBand { High, Medium, Low }
public enum MatchSemanticRelation { ExactCanonical, Equivalent, RelatedOnly, None }
public enum MatchGapType { SkillGap, EvidenceGap, ExperienceGap, EducationCredentialGap, VerificationGap }
public enum MatchPriorityTier { Critical, High, Medium, Low }

public sealed record CandidateEvidenceItemSnapshot(Guid Id, string? SkillId, string Strength,
    double ExtractionConfidence, string Lifecycle, string SourceEntityId, string EvidenceType,
    string Quote, string SourceReference, string[] RelatedToSkillIds);
public sealed record CandidateFactSnapshot(Guid Id, string Kind, string Name, string? Organization,
    string? Detail, string? StartDateText, string? EndDateText, string? Status, string Quote);
public sealed record BehavioralEvidenceSnapshot(Guid Id, string ThemeKey, string Statement,
    string Basis, string Strength, string Quote);
public sealed record CandidateEvidenceSnapshot(Guid CandidateProfileId, DateTime SnapshotAt,
    double SourceCoverage, CandidateEvidenceItemSnapshot[] Evidence, CandidateFactSnapshot[] Experiences,
    CandidateFactSnapshot[] Educations, CandidateFactSnapshot[] Credentials,
    BehavioralEvidenceSnapshot[] BehavioralEvidence, string Revision);
public sealed record JobRequirementItemSnapshot(Guid Id, string Key, RequirementCategory Category,
    RequirementLevel Level, RequirementImportance Importance, string OriginalWording, string? SkillId,
    string? BehavioralThemeKey, string[] Qualifiers, string? GroupKey, RequirementGroupType GroupType,
    bool IsEvaluable, bool IsScoreEligible, RequirementNormalizationStatus NormalizationStatus = RequirementNormalizationStatus.NotApplicable);
public sealed record JobRequirementSnapshot(Guid JobId, Guid RequirementSetId, int Version,
    JobRequirementItemSnapshot[] Requirements, string Revision);

public sealed record MatchingConfiguration(
    string Version, double TechnicalWeight, double EvidenceWeight, double ExperienceWeight,
    double EducationWeight, double WeakStrength, double ModerateStrength, double StrongStrength,
    double StaleFactor, double InactiveFactor, double RelatedScore, double WeakRelatedCap,
    double RequiredLevelFactor, double PreferredLevelFactor, double UnspecifiedLevelFactor,
    double CriticalImportance, double HighImportance, double MediumImportance, double LowImportance,
    double RequiredTechnicalWeight, double PreferredTechnicalWeight, double RelatedEvidenceFactor,
    double ExplicitExperienceWeight, double SeniorityWeight, double ResponsibilitiesWeight,
    double DegreeInProgressFactor, double MinimumComponentCoverage, double CoverageWarningThreshold,
    double CompleteCoverageThreshold, bool BehavioralComponentEnabled)
{
    public static MatchingConfiguration V1 { get; } = new("matching-v1", .45, .25, .25, .05,
        .30, .65, 1, .60, 0, .35, .70, 1, .50, .50, 1.50, 1.20, 1, .70, .80, .20, .25, .60, .25, .15, .65, .50, .60, .80, false);
}

public sealed record MatchEvidenceContribution(Guid EvidenceId, string SourceEntityId, string EvidenceType,
    string Strength, string Lifecycle, string Quote, string SourceReference, double Contribution);
public sealed record RequirementMatchDraft(Guid RequirementId, string Key, string OriginalWording,
    RequirementCategory Category, RequirementLevel Level, RequirementImportance Importance,
    MatchEvaluationStatus EvaluationStatus, MatchClassification? Classification, double? Score,
    double Confidence, MatchConfidenceBand ConfidenceBand, MatchSemanticRelation Relation,
    string ReasonCode, string Details, bool IsStrength, MatchGapType? GapType,
    MatchPriorityTier? Priority, MatchEvidenceContribution[] Evidence);
public sealed record MatchComponentDraft(string Name, MatchComponentStatus Status, double? Score,
    double Coverage, double Confidence, double AppliedWeight);
public sealed record MatchGapDraft(Guid RequirementId, string Requirement, MatchGapType Type,
    MatchPriorityTier Priority, string Reason);
public sealed record BehavioralAssessmentDraft(string ThemeKey, bool Observed, string Summary,
    Guid[] EvidenceIds);
public sealed record MatchResultDraft(string ScoringVersion, double? OverallScore,
    OverallMatchClassification? Classification, OverallMatchStatus Status, double OverallConfidence,
    MatchConfidenceBand ConfidenceBand, double EvaluationCoverage, double? RequiredCoverage,
    MatchComponentDraft[] Components, RequirementMatchDraft[] Requirements, MatchGapDraft[] Gaps,
    BehavioralAssessmentDraft[] BehavioralAssessment, string[] Safeguards);

public static class MatchingEngine
{
    public static MatchResultDraft Calculate(CandidateEvidenceSnapshot candidate,
        JobRequirementSnapshot job, MatchingConfiguration? configuration = null)
    {
        var config = configuration ?? MatchingConfiguration.V1;
        var matches = job.Requirements.OrderBy(item => item.Key, StringComparer.Ordinal)
            .ThenBy(item => item.Id).Select(item => Match(item, candidate, config)).ToArray();
        matches = ApplyAnyOf(matches, job.Requirements);

        var components = new[]
        {
            Component("Technical", RequirementCategory.TechnicalSkill, matches, config.TechnicalWeight, config),
            Component("Evidence", RequirementCategory.TechnicalSkill, matches, config.EvidenceWeight, config, evidence: true),
            Component("Experience", RequirementCategory.Experience, matches, config.ExperienceWeight, config),
            Component("Education", RequirementCategory.EducationCredential, matches, config.EducationWeight, config)
        };
        var applicable = components.Where(item => item.Status == MatchComponentStatus.Applicable).ToArray();
        var relevantComponents = components.Where(item => item.Status != MatchComponentStatus.NotApplicable).ToArray();
        var relevantComponentWeight = relevantComponents.Sum(item => item.AppliedWeight);
        var componentCoverage = relevantComponentWeight == 0 ? 0 : relevantComponents.Sum(item => item.Coverage * item.AppliedWeight) / relevantComponentWeight;
        var evaluationCoverage = Math.Round(.7 * componentCoverage + .3 * Clamp(candidate.SourceCoverage), 4);
        var status = evaluationCoverage < config.CoverageWarningThreshold ? OverallMatchStatus.Limited
            : evaluationCoverage < config.CompleteCoverageThreshold ? OverallMatchStatus.CoverageWarning
            : OverallMatchStatus.Complete;
        double? overall = null;
        if (status != OverallMatchStatus.Limited && applicable.Length > 0)
        {
            var weight = applicable.Sum(item => item.AppliedWeight);
            overall = Math.Round(applicable.Sum(item => item.Score!.Value * item.AppliedWeight) / weight, 2);
        }
        OverallMatchClassification? classification = overall is null ? null : ClassifyOverall(overall.Value);
        var safeguards = new List<string>();
        if (matches.Any(item => item.Level == RequirementLevel.Required && item.Importance == RequirementImportance.Critical && item.Classification == MatchClassification.Missing))
        {
            classification = Cap(classification, OverallMatchClassification.PartialMatch);
            safeguards.Add("CRITICAL_REQUIRED_MISSING");
        }
        if (matches.Any(item => item.Level == RequirementLevel.Required && item.Importance == RequirementImportance.Critical && item.EvaluationStatus == MatchEvaluationStatus.Uncertain))
        {
            classification = Cap(classification, OverallMatchClassification.GoodMatch);
            safeguards.Add("CRITICAL_REQUIRED_UNCERTAIN");
        }
        if (status == OverallMatchStatus.Limited) safeguards.Add("LIMITED_EVALUATION_COVERAGE");
        var applicableWeight = applicable.Sum(item => item.AppliedWeight);
        var componentConfidence = applicableWeight == 0 ? 0 : applicable.Sum(item => item.Confidence * item.AppliedWeight) / applicableWeight;
        var confidence = Math.Round(.7 * componentConfidence + .3 * evaluationCoverage, 4);
        if (safeguards.Contains("CRITICAL_REQUIRED_UNCERTAIN")) confidence = Math.Min(confidence, .84);
        var gaps = matches.Where(item => item.GapType is not null).Select(item => new MatchGapDraft(
            item.RequirementId, item.OriginalWording, item.GapType!.Value, item.Priority!.Value, item.Details)).ToArray();
        var behavioral = job.Requirements.Where(item => item.Category == RequirementCategory.Behavioral)
            .Select(item =>
            {
                var evidence = candidate.BehavioralEvidence.Where(candidateItem =>
                    string.Equals(candidateItem.ThemeKey, item.BehavioralThemeKey, StringComparison.OrdinalIgnoreCase)).ToArray();
                return new BehavioralAssessmentDraft(item.BehavioralThemeKey ?? item.Key, evidence.Length > 0,
                    evidence.Length > 0 ? "Explicit evidence was observed." : "No explicit evidence was observed; this does not affect the score.",
                    evidence.Select(value => value.Id).ToArray());
            }).ToArray();
        return new(config.Version, overall, classification, status, confidence, Band(confidence),
            evaluationCoverage, RequiredCoverage(matches, config), components, matches, gaps, behavioral, safeguards.ToArray());
    }

    private static RequirementMatchDraft Match(JobRequirementItemSnapshot requirement,
        CandidateEvidenceSnapshot candidate, MatchingConfiguration config)
    {
        if (!requirement.IsEvaluable || !requirement.IsScoreEligible || requirement.Category is RequirementCategory.Behavioral or RequirementCategory.Contextual)
            return Result(requirement, MatchEvaluationStatus.NotEvaluated, null, null, 1,
                MatchSemanticRelation.None, "NOT_SCORE_ELIGIBLE", "Retained for context and audit only.", []);
        if (requirement.Category == RequirementCategory.TechnicalSkill)
        {
            if (string.IsNullOrWhiteSpace(requirement.SkillId))
                return Result(requirement, MatchEvaluationStatus.NotEvaluated, null, null, 0,
                    MatchSemanticRelation.None, "UNRESOLVED_REQUIREMENT", "The skill is unresolved and cannot be scored.", []);
            var exact = candidate.Evidence.Where(item => string.Equals(item.SkillId, requirement.SkillId, StringComparison.OrdinalIgnoreCase)).ToArray();
            var related = candidate.Evidence.Where(item => item.RelatedToSkillIds.Contains(requirement.SkillId, StringComparer.OrdinalIgnoreCase)).ToArray();
            var relation = exact.Length > 0 ? requirement.NormalizationStatus == RequirementNormalizationStatus.Equivalent ? MatchSemanticRelation.Equivalent : MatchSemanticRelation.ExactCanonical : related.Length > 0 ? MatchSemanticRelation.RelatedOnly : MatchSemanticRelation.None;
            var source = exact.Length > 0 ? exact : related;
            if (source.Length == 0)
            {
                if (candidate.SourceCoverage < config.CompleteCoverageThreshold)
                    return Result(requirement, MatchEvaluationStatus.Uncertain, null, null, candidate.SourceCoverage,
                        relation, "INSUFFICIENT_SOURCE_COVERAGE", "Available sources cannot establish absence.", []);
                return Result(requirement, MatchEvaluationStatus.Evaluated, MatchClassification.Missing, 0,
                    candidate.SourceCoverage, relation, "NO_SUPPORTING_EVIDENCE", "No supporting evidence was found in sufficiently covered sources.", []);
            }
            var (score, evidence) = Aggregate(source, config);
            if (relation == MatchSemanticRelation.RelatedOnly) score = Math.Min(config.WeakRelatedCap, score * config.RelatedScore);
            var confidenceCap = relation == MatchSemanticRelation.RelatedOnly ? .80 : relation == MatchSemanticRelation.Equivalent ? .95 : 1;
            var confidence = Math.Min(confidenceCap, Math.Round(.3 + .4 * evidence.Average(item => item.Contribution) +
                .2 * Clamp(candidate.SourceCoverage) + .1 * Freshness(source, config), 4));
            return Result(requirement, MatchEvaluationStatus.Evaluated, Classify(score), Math.Round(score * 100, 2),
                confidence, relation, relation == MatchSemanticRelation.RelatedOnly ? "RELATED_ONLY" : "SUPPORTED",
                relation == MatchSemanticRelation.RelatedOnly ? "Related evidence provides partial support." : "Canonical evidence supports this requirement.", evidence);
        }
        if (requirement.Category == RequirementCategory.Experience)
        {
            if (candidate.Experiences.Length == 0)
                return candidate.SourceCoverage < config.CompleteCoverageThreshold
                    ? Result(requirement, MatchEvaluationStatus.Uncertain, null, null, candidate.SourceCoverage,
                        MatchSemanticRelation.None, "EXPERIENCE_COVERAGE_INSUFFICIENT", "Professional experience could not be established.", [])
                    : Result(requirement, MatchEvaluationStatus.Evaluated, MatchClassification.Missing, 0, candidate.SourceCoverage,
                        MatchSemanticRelation.None, "NO_CONFIRMED_PROFESSIONAL_EXPERIENCE", "No confirmed professional experience supports this requirement.", []);
            var requiredYears = ParseRequiredYears(requirement.Qualifiers);
            if (requiredYears is not null)
            {
                var observedYears = ExperienceYears(candidate.Experiences, candidate.SnapshotAt);
                var value = Math.Min(1, observedYears / requiredYears.Value);
                return Result(requirement, MatchEvaluationStatus.Evaluated, Classify(value), Math.Round(value * 100, 2),
                    .85, MatchSemanticRelation.None, "CONFIRMED_PROFESSIONAL_YEARS", $"{observedYears:0.0} confirmed professional years against {requiredYears:0.#} required.", []);
            }
            var requiredTier = Seniority(requirement.OriginalWording);
            if (requiredTier > 0)
            {
                var observedTier = candidate.Experiences.Max(item => Seniority($"{item.Name} {item.Detail}"));
                var value = Math.Min(1, (double)observedTier / requiredTier);
                return Result(requirement, MatchEvaluationStatus.Evaluated, Classify(value), Math.Round(value * 100, 2),
                    .75, MatchSemanticRelation.None, "SENIORITY_ALIGNMENT", $"Observed professional seniority tier {observedTier} against tier {requiredTier}.", []);
            }
            var requiredTerms = SignificantTerms(requirement.OriginalWording);
            if (requiredTerms.Length == 0)
                return Result(requirement, MatchEvaluationStatus.Uncertain, null, null, .5,
                    MatchSemanticRelation.None, "EXPERIENCE_NOT_MACHINE_EVALUABLE", "The experience wording has no deterministic duration, seniority, or responsibility terms.", []);
            var candidateText = string.Join(' ', candidate.Experiences.Select(item => $"{item.Name} {item.Detail}"));
            var matched = requiredTerms.Count(term => candidateText.Contains(term, StringComparison.OrdinalIgnoreCase));
            var responsibilityScore = (double)matched / requiredTerms.Length;
            return Result(requirement, MatchEvaluationStatus.Evaluated, Classify(responsibilityScore), Math.Round(responsibilityScore * 100, 2),
                .70, MatchSemanticRelation.None, "RESPONSIBILITY_ALIGNMENT", $"Matched {matched} of {requiredTerms.Length} responsibility terms in confirmed experience.", []);
        }
        if (requirement.Category == RequirementCategory.EducationCredential)
        {
            var facts = candidate.Educations.Concat(candidate.Credentials).ToArray();
            var words = requirement.OriginalWording.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(word => word.Length >= 4).ToArray();
            var found = facts.FirstOrDefault(fact => words.Any(word => $"{fact.Name} {fact.Detail}".Contains(word, StringComparison.OrdinalIgnoreCase)));
            if (found is null && candidate.SourceCoverage < config.CompleteCoverageThreshold)
                return Result(requirement, MatchEvaluationStatus.Uncertain, null, null, candidate.SourceCoverage,
                    MatchSemanticRelation.None, "EDUCATION_COVERAGE_INSUFFICIENT", "Education or credentials could not be established.", []);
            var inProgress = found is not null && (string.Equals(found.Status, "InProgress", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(found.Status, "In progress", StringComparison.OrdinalIgnoreCase));
            var educationScore = found is null ? 0 : inProgress ? config.DegreeInProgressFactor : 1;
            return Result(requirement, MatchEvaluationStatus.Evaluated, Classify(educationScore), Math.Round(educationScore * 100, 2),
                found is null ? candidate.SourceCoverage : .9, MatchSemanticRelation.None,
                found is null ? "NO_EDUCATION_MATCH" : inProgress ? "EDUCATION_IN_PROGRESS" : "EXPLICIT_EDUCATION_MATCH",
                found is null ? "No matching confirmed education or credential fact was found." : inProgress ? "A matching education fact is explicitly in progress." : "A confirmed education or credential fact matches.", []);
        }
        return Result(requirement, MatchEvaluationStatus.NotEvaluated, null, null, 0,
            MatchSemanticRelation.None, "NOT_EVALUATED", "This requirement is outside matching-v1.", []);
    }

    private static (double Score, MatchEvidenceContribution[] Evidence) Aggregate(
        CandidateEvidenceItemSnapshot[] items, MatchingConfiguration config)
    {
        var perSource = items.GroupBy(item => item.SourceEntityId).Select(group =>
        {
            var ranked = group.Select(item => (Item: item, Value: Strength(item.Strength, config) *
                Lifecycle(item.Lifecycle, config) * Clamp(item.ExtractionConfidence))).OrderByDescending(item => item.Value).ToArray();
            var bonus = Math.Min(.10, Math.Max(0, ranked.Select(item => item.Item.EvidenceType).Distinct(StringComparer.OrdinalIgnoreCase).Count() - 1) * .05);
            return (Score: Math.Min(1, ranked[0].Value + bonus), Items: ranked);
        }).OrderByDescending(item => item.Score).ToArray();
        var score = perSource[0].Score;
        var factors = new[] { .25, .10, .05 };
        for (var index = 1; index < perSource.Length && index <= factors.Length; index++)
            score += (1 - score) * perSource[index].Score * factors[index - 1];
        var evidence = perSource.SelectMany(group => group.Items.Select(item => new MatchEvidenceContribution(item.Item.Id,
            item.Item.SourceEntityId, item.Item.EvidenceType, item.Item.Strength, item.Item.Lifecycle,
            item.Item.Quote, item.Item.SourceReference, Math.Round(item.Value, 4)))).OrderByDescending(item => item.Contribution)
            .ThenBy(item => item.EvidenceId).ToArray();
        return (Clamp(score), evidence);
    }

    private static RequirementMatchDraft[] ApplyAnyOf(RequirementMatchDraft[] matches, JobRequirementItemSnapshot[] requirements)
    {
        var output = matches.ToDictionary(item => item.RequirementId);
        foreach (var group in requirements.Where(item => item.GroupType == RequirementGroupType.AnyOf && item.GroupKey is not null)
                     .GroupBy(item => item.GroupKey!, StringComparer.Ordinal))
        {
            var winner = group.Select(item => output[item.Id]).OrderByDescending(item => item.Score ?? -1)
                .ThenByDescending(item => item.Confidence).ThenBy(item => item.Key, StringComparer.Ordinal).First();
            foreach (var item in group.Where(item => item.Id != winner.RequirementId))
                output[item.Id] = Result(item, MatchEvaluationStatus.NotEvaluated, null, null, 1,
                    MatchSemanticRelation.None, "ANY_OF_ALTERNATIVE_NOT_SELECTED", "Another alternative in this group was the best supported option.", []);
        }
        return matches.Select(item => output[item.RequirementId]).ToArray();
    }

    private static MatchComponentDraft Component(string name, RequirementCategory category,
        RequirementMatchDraft[] matches, double weight, MatchingConfiguration config, bool evidence = false)
    {
        var relevant = matches.Where(item => item.Category == category && item.ReasonCode is not
            ("NOT_SCORE_ELIGIBLE" or "ANY_OF_ALTERNATIVE_NOT_SELECTED")).ToArray();
        if (relevant.Length == 0) return new(name, MatchComponentStatus.NotApplicable, null, 0, 0, weight);
        var evaluated = relevant.Where(item => item.EvaluationStatus == MatchEvaluationStatus.Evaluated && item.Score is not null).ToArray();
        var relevantWeight = relevant.Sum(item => Importance(item.Importance, config) * Level(item.Level, config));
        var evaluatedWeight = evaluated.Sum(item => Importance(item.Importance, config) * Level(item.Level, config));
        var coverage = relevantWeight == 0 ? 0 : evaluatedWeight / relevantWeight;
        var confidence = evaluatedWeight == 0 ? 0 : evaluated.Sum(item => item.Confidence * Importance(item.Importance, config) * Level(item.Level, config)) / evaluatedWeight;
        if (coverage < config.MinimumComponentCoverage)
            return new(name, MatchComponentStatus.InsufficientInformation, null, Math.Round(coverage, 4), Math.Round(confidence, 4), weight);

        double Presence(RequirementMatchDraft item) => item.Classification switch
        {
            MatchClassification.Strong => 1, MatchClassification.Moderate => .90,
            MatchClassification.Weak => .70, _ => 0
        };
        double TechnicalValue(RequirementMatchDraft item) => 100 * Presence(item) *
            (item.Relation == MatchSemanticRelation.RelatedOnly ? config.RelatedScore : 1);
        double EvidenceValue(RequirementMatchDraft item) => item.Relation == MatchSemanticRelation.RelatedOnly
            ? item.Score!.Value / config.RelatedScore * config.RelatedEvidenceFactor : item.Score!.Value;
        double WeightedAverage(RequirementMatchDraft[] values, Func<RequirementMatchDraft, double> selector)
        {
            var denominator = values.Sum(item => Importance(item.Importance, config));
            return denominator == 0 ? 0 : values.Sum(item => selector(item) * Importance(item.Importance, config)) / denominator;
        }
        double score;
        if (name == "Technical")
        {
            var required = evaluated.Where(item => item.Level == RequirementLevel.Required).ToArray();
            var preferred = evaluated.Where(item => item.Level != RequirementLevel.Required).ToArray();
            score = required.Length > 0 && preferred.Length > 0
                ? config.RequiredTechnicalWeight * WeightedAverage(required, TechnicalValue) +
                  config.PreferredTechnicalWeight * WeightedAverage(preferred, TechnicalValue)
                : required.Length > 0 ? WeightedAverage(required, TechnicalValue) : WeightedAverage(preferred, TechnicalValue);
        }
        else if (name == "Experience")
        {
            var dimensions = new[]
            {
                (Items: evaluated.Where(item => item.ReasonCode == "CONFIRMED_PROFESSIONAL_YEARS").ToArray(), Weight: config.ExplicitExperienceWeight),
                (Items: evaluated.Where(item => item.ReasonCode == "SENIORITY_ALIGNMENT").ToArray(), Weight: config.SeniorityWeight),
                (Items: evaluated.Where(item => item.ReasonCode == "RESPONSIBILITY_ALIGNMENT").ToArray(), Weight: config.ResponsibilitiesWeight)
            }.Where(item => item.Items.Length > 0).ToArray();
            var dimensionWeight = dimensions.Sum(item => item.Weight);
            score = dimensionWeight == 0 ? WeightedAverage(evaluated, item => item.Score!.Value)
                : dimensions.Sum(dimension => dimension.Weight * WeightedAverage(dimension.Items, item => item.Score!.Value)) / dimensionWeight;
        }
        else
        {
            var denominator = evaluated.Sum(item => Importance(item.Importance, config) * Level(item.Level, config));
            Func<RequirementMatchDraft, double> selector = evidence ? EvidenceValue : item => item.Score!.Value;
            score = denominator == 0 ? 0 : evaluated.Sum(item => selector(item) * Importance(item.Importance, config) * Level(item.Level, config)) / denominator;
        }
        return new(name, MatchComponentStatus.Applicable, Math.Round(score, 2), Math.Round(coverage, 4), Math.Round(confidence, 4), weight);
    }

    private static double? RequiredCoverage(RequirementMatchDraft[] matches, MatchingConfiguration config)
    {
        var required = matches.Where(item => item.Level == RequirementLevel.Required && item.EvaluationStatus == MatchEvaluationStatus.Evaluated && item.Score is not null && item.Category is not (RequirementCategory.Behavioral or RequirementCategory.Contextual)).ToArray();
        if (required.Length == 0) return null;
        var denominator = required.Sum(item => Importance(item.Importance, config));
        return Math.Round(required.Sum(item => item.Score!.Value * Importance(item.Importance, config)) / denominator / 100, 4);
    }

    private static RequirementMatchDraft Result(JobRequirementItemSnapshot item, MatchEvaluationStatus evaluation,
        MatchClassification? classification, double? score, double confidence, MatchSemanticRelation relation,
        string reason, string details, MatchEvidenceContribution[] evidence)
    {
        MatchGapType? gap = evaluation == MatchEvaluationStatus.Uncertain ? MatchGapType.VerificationGap : classification switch
        {
            MatchClassification.Missing when item.Category == RequirementCategory.TechnicalSkill => MatchGapType.SkillGap,
            MatchClassification.Missing when item.Category == RequirementCategory.Experience => MatchGapType.ExperienceGap,
            MatchClassification.Missing when item.Category == RequirementCategory.EducationCredential => MatchGapType.EducationCredentialGap,
            MatchClassification.Weak => MatchGapType.EvidenceGap,
            _ => null
        };
        return new(item.Id, item.Key, item.OriginalWording, item.Category, item.Level, item.Importance,
            evaluation, classification, score, Math.Round(Clamp(confidence), 4), Band(confidence), relation,
            reason, details, classification == MatchClassification.Strong, gap, gap is null ? null : Priority(item.Importance), evidence);
    }

    private static double Strength(string value, MatchingConfiguration c) => value.ToLowerInvariant() switch { "strong" => c.StrongStrength, "moderate" => c.ModerateStrength, _ => c.WeakStrength };
    private static double Lifecycle(string value, MatchingConfiguration c) => value.ToLowerInvariant() switch { "inactive" or "removed" => c.InactiveFactor, "stale" => c.StaleFactor, _ => 1 };
    private static double Freshness(CandidateEvidenceItemSnapshot[] items, MatchingConfiguration c) => items.Length == 0 ? 0 : items.Average(item => Lifecycle(item.Lifecycle, c));
    private static double Importance(RequirementImportance value, MatchingConfiguration c) => value switch { RequirementImportance.Critical => c.CriticalImportance, RequirementImportance.High => c.HighImportance, RequirementImportance.Low => c.LowImportance, _ => c.MediumImportance };
    private static double Level(RequirementLevel value, MatchingConfiguration c) => value switch { RequirementLevel.Required => c.RequiredLevelFactor, RequirementLevel.Preferred => c.PreferredLevelFactor, _ => c.UnspecifiedLevelFactor };
    private static MatchPriorityTier Priority(RequirementImportance value) => value switch { RequirementImportance.Critical => MatchPriorityTier.Critical, RequirementImportance.High => MatchPriorityTier.High, RequirementImportance.Low => MatchPriorityTier.Low, _ => MatchPriorityTier.Medium };
    private static MatchClassification Classify(double value) => value >= .80 ? MatchClassification.Strong : value >= .55 ? MatchClassification.Moderate : value >= .20 ? MatchClassification.Weak : MatchClassification.Missing;
    private static OverallMatchClassification ClassifyOverall(double value) => value >= 85 ? OverallMatchClassification.StrongMatch : value >= 70 ? OverallMatchClassification.GoodMatch : value >= 55 ? OverallMatchClassification.PartialMatch : value >= 40 ? OverallMatchClassification.WeakMatch : OverallMatchClassification.LowMatch;
    private static MatchConfidenceBand Band(double value) => value >= .85 ? MatchConfidenceBand.High : value >= .65 ? MatchConfidenceBand.Medium : MatchConfidenceBand.Low;
    private static OverallMatchClassification? Cap(OverallMatchClassification? value, OverallMatchClassification cap) => value is null || value.Value < cap ? cap : value;
    private static double Clamp(double value) => Math.Clamp(value, 0, 1);
    private static int Seniority(string text)
    {
        var value = text.ToLowerInvariant();
        if (value.Contains("principal") || value.Contains("staff")) return 5;
        if (value.Contains("lead") || value.Contains("manager")) return 4;
        if (value.Contains("senior") || value.Contains(" sr")) return 3;
        if (value.Contains("mid-level") || value.Contains("intermediate")) return 2;
        if (value.Contains("junior") || value.Contains("entry-level") || value.Contains("intern")) return 1;
        return 0;
    }
    private static string[] SignificantTerms(string text)
    {
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "experience", "required", "preferred", "years", "year", "with", "working", "knowledge", "strong", "ability", "role", "candidate", "must", "have", "will", "using", "professional" };
        return new string(text.Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(term => term.Length >= 4 && !ignored.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(term => term, StringComparer.Ordinal).ToArray();
    }
    private static double? ParseRequiredYears(IEnumerable<string> qualifiers)
    {
        foreach (var qualifier in qualifiers)
        {
            var number = new string(qualifier.SkipWhile(character => !char.IsDigit(character)).TakeWhile(character => char.IsDigit(character) || character == '.').ToArray());
            if (double.TryParse(number, System.Globalization.CultureInfo.InvariantCulture, out var value)) return value;
        }
        return null;
    }
    private static double ExperienceYears(CandidateFactSnapshot[] facts, DateTime snapshotAt)
    {
        var ranges = facts.Select(item => (Start: ParseDate(item.StartDateText, snapshotAt), End: ParseDate(item.EndDateText, snapshotAt) ?? snapshotAt.Date))
            .Where(item => item.Start is not null && item.End >= item.Start).Select(item => (item.Start!.Value, item.End)).OrderBy(item => item.Value).ToArray();
        if (ranges.Length == 0) return 0;
        var total = TimeSpan.Zero; var start = ranges[0].Item1; var end = ranges[0].Item2;
        foreach (var range in ranges.Skip(1)) { if (range.Item1 <= end) end = range.Item2 > end ? range.Item2 : end; else { total += end - start; start = range.Item1; end = range.Item2; } }
        total += end - start; return total.TotalDays / 365.2425;
    }
    private static DateTime? ParseDate(string? text, DateTime snapshotAt)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (text.Equals("present", StringComparison.OrdinalIgnoreCase) || text.Equals("current", StringComparison.OrdinalIgnoreCase)) return snapshotAt.Date;
        return DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var value) ? value.Date : null;
    }
}
