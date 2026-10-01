using System.Security.Claims;
using ProofPath.Application.Matching;
using ProofPath.Application.Recommendations;

namespace ProofPath.Api.Endpoints;

public static class RecommendationEndpoints
{
    public static IEndpointRouteBuilder MapRecommendationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/jobs/{jobId:guid}").RequireAuthorization();
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (RecommendationProblem problem)
            {
                var message = problem.Code switch
                {
                    "JOB_NOT_FOUND" => "The job was not found.",
                    "RECOMMENDATION_NOT_FOUND" => "The recommendation was not found.",
                    "TRACKING_NOTES_LENGTH" => "Keep tracking notes under 2,000 characters.",
                    _ => "The recommendation workspace could not be updated."
                };
                return Results.Problem(message, statusCode: problem.Status,
                    extensions: new Dictionary<string, object?> { ["code"] = problem.Code });
            }
            catch (MatchingProblem problem)
            {
                var message = problem.Code switch
                {
                    "REQUIREMENTS_NOT_CONFIRMED" => "Confirm the current requirement set before rescanning.",
                    "JOB_NOT_FOUND" => "The job was not found.",
                    _ => "The match could not be recalculated."
                };
                return Results.Problem(message, statusCode: problem.Status,
                    extensions: new Dictionary<string, object?> { ["code"] = problem.Code });
            }
        });
        group.MapGet("/tracking", (Guid jobId, ClaimsPrincipal user, IRecommendationWorkspace workspace, CancellationToken ct) =>
            workspace.TrackingAsync(UserId(user), jobId, ct));
        group.MapPut("/tracking", (Guid jobId, JobTrackingUpdate update, ClaimsPrincipal user, IRecommendationWorkspace workspace, CancellationToken ct) =>
            workspace.UpdateTrackingAsync(UserId(user), jobId, update, ct));
        group.MapGet("/recommendations", (Guid jobId, ClaimsPrincipal user, IRecommendationWorkspace workspace, CancellationToken ct) =>
            workspace.RecommendationsAsync(UserId(user), jobId, ct));
        group.MapPut("/recommendations/{recommendationId:guid}", (Guid jobId, Guid recommendationId,
            RecommendationUpdate update, ClaimsPrincipal user, IRecommendationWorkspace workspace, CancellationToken ct) =>
            workspace.UpdateRecommendationAsync(UserId(user), jobId, recommendationId, update, ct));
        group.MapGet("/score-history", (Guid jobId, ClaimsPrincipal user, IRecommendationWorkspace workspace, CancellationToken ct) =>
            workspace.ScoreHistoryAsync(UserId(user), jobId, ct));
        group.MapPost("/rescan", async (Guid jobId, ClaimsPrincipal user, IMatchingWorkspace workspace, CancellationToken ct) =>
        {
            var result = await workspace.CalculateAsync(UserId(user), jobId, ct);
            return Results.Created($"/api/v1/jobs/{jobId}/matches/{result.Id}", result);
        });
        return endpoints;
    }

    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
