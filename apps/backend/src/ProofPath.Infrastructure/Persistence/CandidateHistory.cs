using Microsoft.EntityFrameworkCore;
using ProofPath.Application.Analysis;

namespace ProofPath.Infrastructure.Persistence;

public sealed record HistoryPosition(DateTime CreatedAt, Guid Id);

public sealed record MatchHistoryItem(Guid Id, Guid JobId, string? JobTitle, string? Company,
    DateTime CreatedAt, string ScoringVersion, string Status, string? Classification, decimal? Score, decimal Coverage);

public sealed class CandidateHistory(ProofPathDbContext database)
{
    public Task<AnalysisStatus[]> EvaluationsAsync(string userId, HistoryPosition? before, int count, CancellationToken ct)
    {
        var query = from evaluation in database.AnalysisJobs.AsNoTracking()
                    join profile in database.CandidateProfiles on evaluation.CandidateProfileId equals profile.Id
                    where profile.UserId == userId
                    select evaluation;
        if (before is not null)
            query = query.Where(item => EF.Functions.LessThan(
                ValueTuple.Create(item.CreatedAt, item.Id), ValueTuple.Create(before.CreatedAt, before.Id)));
        return query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Take(count)
            .Select(item => new AnalysisStatus(item.Id, item.Kind, item.ResourceId, item.State, item.Attempts,
                item.ErrorCode, item.Retryable, item.CreatedAt, item.UpdatedAt))
            .ToArrayAsync(ct);
    }

    public Task<MatchHistoryItem[]> MatchesAsync(string userId, HistoryPosition? before, int count, CancellationToken ct)
    {
        var query = from match in database.MatchResults.AsNoTracking()
                    join profile in database.CandidateProfiles on match.CandidateProfileId equals profile.Id
                    join job in database.Jobs on match.JobId equals job.Id
                    where profile.UserId == userId && job.CandidateProfileId == profile.Id
                    select new { Match = match, job.Title, job.Company };
        if (before is not null)
            query = query.Where(item => EF.Functions.LessThan(
                ValueTuple.Create(item.Match.CreatedAt, item.Match.Id), ValueTuple.Create(before.CreatedAt, before.Id)));
        return query.OrderByDescending(item => item.Match.CreatedAt).ThenByDescending(item => item.Match.Id).Take(count)
            .Select(item => new MatchHistoryItem(item.Match.Id, item.Match.JobId, item.Title, item.Company,
                item.Match.CreatedAt, item.Match.ScoringVersionId, item.Match.OverallStatus,
                item.Match.OverallClassification, item.Match.OverallScore, item.Match.EvaluationCoverage))
            .ToArrayAsync(ct);
    }
}
