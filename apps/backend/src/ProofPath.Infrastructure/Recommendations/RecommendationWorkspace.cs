using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProofPath.Application.Recommendations;
using ProofPath.Domain.Entities;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Infrastructure.Recommendations;

public sealed class RecommendationWorkspace(ProofPathDbContext database) : IRecommendationWorkspace
{
    public async Task<JobTrackingView> TrackingAsync(string userId, Guid jobId, CancellationToken ct)
    {
        await RequireJob(userId, jobId, ct);
        var tracking = await database.JobTrackings.AsNoTracking().SingleOrDefaultAsync(item => item.JobId == jobId, ct);
        return tracking is null
            ? new(jobId, ApplicationStage.Saved, null, null, DateTime.MinValue)
            : View(tracking);
    }

    public async Task<JobTrackingView> UpdateTrackingAsync(string userId, Guid jobId, JobTrackingUpdate update, CancellationToken ct)
    {
        await RequireJob(userId, jobId, ct);
        var notes = string.IsNullOrWhiteSpace(update.Notes) ? null : update.Notes.Trim();
        if (notes?.Length > 2_000) throw new RecommendationProblem("TRACKING_NOTES_LENGTH");
        var tracking = await database.JobTrackings.SingleOrDefaultAsync(item => item.JobId == jobId, ct);
        if (tracking is null)
        {
            tracking = new JobTracking { JobId = jobId };
            database.JobTrackings.Add(tracking);
        }
        tracking.Stage = update.Stage;
        tracking.Notes = notes;
        tracking.NextActionAt = update.NextActionAt?.ToUniversalTime();
        tracking.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(ct);
        return View(tracking);
    }

    public async Task<RecommendationView[]> RecommendationsAsync(string userId, Guid jobId, CancellationToken ct)
    {
        await RequireJob(userId, jobId, ct);
        var latestId = await database.MatchResults.AsNoTracking().Where(item => item.JobId == jobId)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Select(item => (Guid?)item.Id).FirstOrDefaultAsync(ct);
        if (latestId is null) return [];
        return (await database.MatchRecommendations.AsNoTracking().Where(item => item.MatchResultId == latestId)
            .OrderBy(item => item.Rank).ThenBy(item => item.Id).ToArrayAsync(ct)).Select(View).ToArray();
    }

    public async Task<RecommendationView> UpdateRecommendationAsync(string userId, Guid jobId,
        Guid recommendationId, RecommendationUpdate update, CancellationToken ct)
    {
        await RequireJob(userId, jobId, ct);
        var recommendation = await (from item in database.MatchRecommendations
                                    join match in database.MatchResults on item.MatchResultId equals match.Id
                                    where item.Id == recommendationId && match.JobId == jobId
                                    select item).SingleOrDefaultAsync(ct)
            ?? throw new RecommendationProblem("RECOMMENDATION_NOT_FOUND", 404);
        recommendation.Status = update.Status;
        recommendation.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(ct);
        return View(recommendation);
    }

    public async Task<ScoreHistoryPoint[]> ScoreHistoryAsync(string userId, Guid jobId, CancellationToken ct)
    {
        await RequireJob(userId, jobId, ct);
        var matches = await database.MatchResults.AsNoTracking().Where(item => item.JobId == jobId)
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id).ToArrayAsync(ct);
        double? previous = null;
        return matches.Select(item =>
        {
            double? score = item.OverallScore is null ? null : (double)item.OverallScore;
            double? delta = score is not null && previous is not null ? Math.Round(score.Value - previous.Value, 2) : null;
            if (score is not null) previous = score;
            return new ScoreHistoryPoint(item.Id, item.CreatedAt, score, delta, item.OverallClassification,
                item.OverallStatus, (double)item.EvaluationCoverage);
        }).Reverse().ToArray();
    }

    private async Task RequireJob(string userId, Guid jobId, CancellationToken ct)
    {
        var owned = await (from job in database.Jobs.AsNoTracking()
                           join profile in database.CandidateProfiles.AsNoTracking() on job.CandidateProfileId equals profile.Id
                           where job.Id == jobId && profile.UserId == userId
                           select job.Id).AnyAsync(ct);
        if (!owned) throw new RecommendationProblem("JOB_NOT_FOUND", 404);
    }

    private static JobTrackingView View(JobTracking item) => new(item.JobId, item.Stage, item.Notes,
        item.NextActionAt, item.UpdatedAt);
    private static RecommendationView View(MatchRecommendation item) => new(item.Id, item.MatchResultId,
        item.RequirementId, item.Rank, item.Kind, item.Title, item.Rationale, item.Action, item.Status,
        item.CreatedAt, item.UpdatedAt);
}

public static class RecommendationRegistration
{
    public static IServiceCollection AddRecommendationModule(this IServiceCollection services)
    {
        services.AddScoped<IRecommendationWorkspace, RecommendationWorkspace>();
        return services;
    }
}
