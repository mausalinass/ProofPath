using System.Security.Claims;
using ProofPath.Application.Matching;

namespace ProofPath.Api.Endpoints;

public static class MatchingEndpoints
{
    public static IEndpointRouteBuilder MapMatchingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/jobs/{jobId:guid}/matches").RequireAuthorization();
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (MatchingProblem problem)
            {
                var message = problem.Code switch
                {
                    "REQUIREMENTS_NOT_CONFIRMED" => "Confirm the current requirement set before calculating a match.",
                    "JOB_NOT_FOUND" => "The job was not found.",
                    _ => "The match could not be calculated."
                };
                return Results.Problem(message, statusCode: problem.Status,
                    extensions: new Dictionary<string, object?> { ["code"] = problem.Code });
            }
        });
        group.MapPost("/", async (Guid jobId, ClaimsPrincipal user, IMatchingWorkspace workspace, CancellationToken ct) =>
        {
            var result = await workspace.CalculateAsync(UserId(user), jobId, ct);
            return Results.Created($"/api/v1/jobs/{jobId}/matches/{result.Id}", result);
        });
        group.MapGet("/", (Guid jobId, ClaimsPrincipal user, IMatchingWorkspace workspace, CancellationToken ct) =>
            workspace.ListAsync(UserId(user), jobId, ct));
        group.MapGet("/latest", async (Guid jobId, ClaimsPrincipal user, IMatchingWorkspace workspace, CancellationToken ct) =>
            await workspace.LatestAsync(UserId(user), jobId, ct) is { } result ? Results.Ok(result) : Results.NotFound());
        group.MapGet("/{matchId:guid}", async (Guid jobId, Guid matchId, ClaimsPrincipal user, IMatchingWorkspace workspace, CancellationToken ct) =>
            await workspace.GetAsync(UserId(user), jobId, matchId, ct) is { } result ? Results.Ok(result) : Results.NotFound());
        return endpoints;
    }
    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
