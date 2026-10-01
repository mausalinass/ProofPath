using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProofPath.Application.Matching;
using ProofPath.Domain.Entities;
using ProofPath.Domain.Matching;
using ProofPath.Infrastructure.Identity;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Infrastructure.Matching;

public sealed class MatchingWorkspace(ProofPathDbContext database) : IMatchingWorkspace
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public async Task<MatchView> CalculateAsync(string userId, Guid jobId, CancellationToken ct)
    {
        var job = await OwnedJob(userId, jobId, ct) ?? throw new MatchingProblem("JOB_NOT_FOUND", 404);
        var requirementSet = await database.RequirementSets.AsNoTracking()
            .SingleOrDefaultAsync(item => item.JobId == jobId && item.Active, ct)
            ?? throw new MatchingProblem("REQUIREMENTS_NOT_CONFIRMED", 409);
        var requirements = await database.JobRequirements.AsNoTracking()
            .Where(item => item.RequirementSetId == requirementSet.Id && item.State == RequirementState.Confirmed)
            .OrderBy(item => item.Key).ToArrayAsync(ct);
        var requirementSnapshot = new JobRequirementSnapshot(job.Id, requirementSet.Id, requirementSet.Version,
            requirements.Select(item => new JobRequirementItemSnapshot(item.Id, item.Key, item.Category,
                item.Level, item.Importance, item.OriginalWording, item.SkillId, item.BehavioralThemeKey,
                Deserialize<string[]>(item.QualifiersJson) ?? [], item.GroupKey, item.GroupType,
                item.IsEvaluable, item.IsScoreEligible, item.NormalizationStatus)).ToArray(), Hash(requirements.Select(item =>
                    $"{item.Id}:{item.Key}:{item.Category}:{item.Level}:{item.Importance}:{item.SkillId}:{item.GroupKey}:{item.GroupType}:{item.IsEvaluable}:{item.IsScoreEligible}")));
        var candidateSnapshot = await CandidateSnapshot(job.CandidateProfileId, ct);
        var draft = MatchingEngine.Calculate(candidateSnapshot, requirementSnapshot, MatchingConfiguration.V1);
        var result = new MatchResult
        {
            Id = Guid.NewGuid(),
            CandidateProfileId = job.CandidateProfileId,
            JobId = job.Id,
            RequirementSetId = requirementSet.Id,
            ScoringVersionId = draft.ScoringVersion,
            CandidateSnapshotJson = Serialize(candidateSnapshot),
            RequirementSnapshotJson = Serialize(requirementSnapshot),
            ResultJson = Serialize(draft),
            OverallScore = draft.OverallScore is null ? null : (decimal)draft.OverallScore,
            OverallClassification = draft.Classification?.ToString(),
            OverallStatus = draft.Status.ToString(),
            OverallConfidence = (decimal)draft.OverallConfidence,
            EvaluationCoverage = (decimal)draft.EvaluationCoverage,
            CreatedAt = DateTime.UtcNow
        };
        database.MatchResults.Add(result);
        foreach (var match in draft.Requirements)
        {
            var requirement = requirementSnapshot.Requirements.Single(item => item.Id == match.RequirementId);
            var record = new RequirementMatchRecord
            {
                Id = Guid.NewGuid(),
                MatchResultId = result.Id,
                RequirementId = match.RequirementId,
                RequirementSnapshotJson = Serialize(requirement),
                ResultSnapshotJson = Serialize(match)
            };
            database.RequirementMatchRecords.Add(record);
            foreach (var evidence in match.Evidence)
                database.RequirementMatchEvidenceRecords.Add(new RequirementMatchEvidenceRecord
                {
                    Id = Guid.NewGuid(),
                    RequirementMatchRecordId = record.Id,
                    EvidenceItemId = evidence.EvidenceId,
                    EvidenceSnapshotJson = Serialize(evidence),
                    Contribution = (decimal)evidence.Contribution
                });
        }
        foreach (var recommendation in Recommendations(result.Id, draft))
            database.MatchRecommendations.Add(recommendation);
        await database.SaveChangesAsync(ct);
        return View(result, draft);
    }

    public async Task<MatchView?> LatestAsync(string userId, Guid jobId, CancellationToken ct)
    {
        if (await OwnedJob(userId, jobId, ct) is null) throw new MatchingProblem("JOB_NOT_FOUND", 404);
        var result = await database.MatchResults.AsNoTracking().Where(item => item.JobId == jobId)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).FirstOrDefaultAsync(ct);
        return result is null ? null : View(result, Deserialize<MatchResultDraft>(result.ResultJson)!);
    }
    public async Task<MatchView?> GetAsync(string userId, Guid jobId, Guid matchId, CancellationToken ct)
    {
        if (await OwnedJob(userId, jobId, ct) is null) throw new MatchingProblem("JOB_NOT_FOUND", 404);
        var result = await database.MatchResults.AsNoTracking().SingleOrDefaultAsync(item => item.JobId == jobId && item.Id == matchId, ct);
        return result is null ? null : View(result, Deserialize<MatchResultDraft>(result.ResultJson)!);
    }
    public async Task<MatchSummary[]> ListAsync(string userId, Guid jobId, CancellationToken ct)
    {
        if (await OwnedJob(userId, jobId, ct) is null) throw new MatchingProblem("JOB_NOT_FOUND", 404);
        var stored = await database.MatchResults.AsNoTracking().Where(item => item.JobId == jobId)
            .OrderByDescending(item => item.CreatedAt).ToArrayAsync(ct);
        return stored.Select(item => new MatchSummary(item.Id, item.CreatedAt, item.ScoringVersionId,
            item.OverallScore == null ? null : (double)item.OverallScore,
            item.OverallClassification == null ? null : Enum.Parse<OverallMatchClassification>(item.OverallClassification),
            Enum.Parse<OverallMatchStatus>(item.OverallStatus), (double)item.EvaluationCoverage)).ToArray();
    }

    private async Task<Job?> OwnedJob(string userId, Guid jobId, CancellationToken ct) =>
        await (from job in database.Jobs.AsNoTracking()
               join profile in database.CandidateProfiles.AsNoTracking() on job.CandidateProfileId equals profile.Id
               where job.Id == jobId && profile.UserId == userId
               select job).SingleOrDefaultAsync(ct);

    private async Task<CandidateEvidenceSnapshot> CandidateSnapshot(Guid profileId, CancellationToken ct)
    {
        var activeExtractionIds = await (from extraction in database.ResumeExtractions.AsNoTracking()
                                         join resume in database.Resumes.AsNoTracking() on extraction.ResumeId equals resume.Id
                                         where resume.CandidateProfileId == profileId && extraction.Active && extraction.ConfirmedAt != null
                                         select extraction.Id).ToArrayAsync(ct);
        var evidence = await database.EvidenceItems.AsNoTracking().Where(item => item.CandidateProfileId == profileId &&
            (item.ResumeExtractionId == null || activeExtractionIds.Contains(item.ResumeExtractionId.Value))).OrderBy(item => item.Id).ToArrayAsync(ct);
        var experiences = await database.Experiences.AsNoTracking().Where(item => item.CandidateProfileId == profileId &&
            item.ResumeExtractionId != null && activeExtractionIds.Contains(item.ResumeExtractionId.Value)).OrderBy(item => item.Id).ToArrayAsync(ct);
        var educations = await database.Educations.AsNoTracking().Where(item => item.CandidateProfileId == profileId &&
            item.ResumeExtractionId != null && activeExtractionIds.Contains(item.ResumeExtractionId.Value)).OrderBy(item => item.Id).ToArrayAsync(ct);
        var credentials = await database.Credentials.AsNoTracking().Where(item => item.CandidateProfileId == profileId &&
            item.ResumeExtractionId != null && activeExtractionIds.Contains(item.ResumeExtractionId.Value)).OrderBy(item => item.Id).ToArrayAsync(ct);
        var behavior = await database.BehavioralEvidenceItems.AsNoTracking().Where(item => item.CandidateProfileId == profileId &&
            activeExtractionIds.Contains(item.ResumeExtractionId)).OrderBy(item => item.Id).ToArrayAsync(ct);
        var includedRepositories = await database.Repositories.AsNoTracking().Where(item => item.CandidateProfileId == profileId && item.IncludedForAnalysis).ToArrayAsync(ct);
        var completedRepositories = includedRepositories.Count(item => item.ScanStatus is RepositoryScanStatus.Completed or RepositoryScanStatus.CompletedLimited);
        var sourceCoverage = activeExtractionIds.Length > 0 ? 1d : includedRepositories.Length > 0 ? (double)completedRepositories / includedRepositories.Length : evidence.Length > 0 ? .6 : 0;
        string Source(EvidenceItem item) => item.ResumeExtractionId is not null ? $"resume:{item.ResumeExtractionId}" : item.RepositoryAnalysisId is not null ? $"repository:{item.RepositoryAnalysisId}" : item.ProjectId is not null ? $"project:{item.ProjectId}" : $"evidence:{item.Id}";
        CandidateFactSnapshot Fact(CandidateFact item, string kind) => new(item.Id, kind, item.Name, item.Organization, item.Detail, item.StartDateText, item.EndDateText, item.Status, item.Quote);
        var snapshotEvidence = evidence.Select(item => new CandidateEvidenceItemSnapshot(item.Id, item.SkillId, item.Strength,
            (double)item.ExtractionConfidence, item.Lifecycle, Source(item), item.EvidenceType, item.Quote,
            item.SourcePath ?? item.SourceBlockId, [])).ToArray();
        var revision = Hash(snapshotEvidence.Select(item => $"{item.Id}:{item.SkillId}:{item.Strength}:{item.Lifecycle}:{item.SourceEntityId}:{item.ContributionKey()}"));
        return new(profileId, DateTime.UtcNow, sourceCoverage, snapshotEvidence,
            experiences.Select(item => Fact(item, "Experience")).ToArray(), educations.Select(item => Fact(item, "Education")).ToArray(),
            credentials.Select(item => Fact(item, "Credential")).ToArray(), behavior.Select(item => new BehavioralEvidenceSnapshot(
                item.Id, item.ThemeKey, item.Statement, item.Basis, item.Strength, item.Quote)).ToArray(), revision);
    }

    private static MatchView View(MatchResult entity, MatchResultDraft result) => new(entity.Id, entity.JobId,
        entity.RequirementSetId, entity.ScoringVersionId, entity.CreatedAt, result);
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static T? Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, Json);
    private static string Hash(IEnumerable<string> values) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", values)))).ToLowerInvariant();

    private static MatchRecommendation[] Recommendations(Guid matchResultId, MatchResultDraft result)
    {
        var now = DateTime.UtcNow;
        var ranked = result.Gaps.OrderBy(item => item.Priority).ThenBy(item => item.Type)
            .ThenBy(item => item.Requirement, StringComparer.Ordinal).Take(5).ToArray();
        if (ranked.Length == 0)
            return [new MatchRecommendation
            {
                Id = Guid.NewGuid(), MatchResultId = matchResultId, Rank = 1, Kind = "MaintainEvidence",
                Title = "Keep your strongest evidence current",
                Rationale = "This snapshot has no scored gaps. Preserve its strengths before applying.",
                Action = "Review the evidence trace, refresh stale sources, and prepare the strongest examples for the application.",
                CreatedAt = now, UpdatedAt = now
            }];
        return ranked.Select((gap, index) => new MatchRecommendation
        {
            Id = Guid.NewGuid(),
            MatchResultId = matchResultId,
            RequirementId = gap.RequirementId,
            Rank = index + 1,
            Kind = gap.Type.ToString(),
            Title = $"Improve evidence for {gap.Requirement}",
            Rationale = gap.Reason,
            Action = Action(gap),
            CreatedAt = now,
            UpdatedAt = now
        }).ToArray();
    }

    private static string Action(MatchGapDraft gap) => gap.Type switch
    {
        MatchGapType.SkillGap => $"Build or extend a focused project that uses {gap.Requirement}, then capture implementation and test evidence.",
        MatchGapType.EvidenceGap => $"Strengthen {gap.Requirement} with tests, usage examples, and a concise explanation of the implementation.",
        MatchGapType.ExperienceGap => $"Document a concrete experience demonstrating {gap.Requirement}, including scope, responsibility, and outcome.",
        MatchGapType.EducationCredentialGap => $"Verify the credential requirement for {gap.Requirement} and record an equivalent or in-progress qualification.",
        _ => $"Review and verify the available evidence for {gap.Requirement} before relying on this match."
    };
}

internal static class MatchingSnapshotExtensions
{
    public static string ContributionKey(this CandidateEvidenceItemSnapshot item) => $"{item.EvidenceType}:{item.ExtractionConfidence:0.####}:{item.SourceReference}";
}

public static class MatchingRegistration
{
    public static IServiceCollection AddMatchingModule(this IServiceCollection services)
    {
        services.AddScoped<IMatchingWorkspace, MatchingWorkspace>();
        return services;
    }
}
