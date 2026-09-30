using System.Security.Claims;
using ProofPath.Application.Analysis;

namespace ProofPath.Api.Endpoints;

public static class AnalysisEndpoints
{
    public static IEndpointRouteBuilder MapAnalysisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/analysis-jobs").RequireAuthorization();
        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IAnalysisQueue queue, CancellationToken ct) =>
        {
            var status = await queue.GetAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, id, ct);
            return status is null ? Results.NotFound() : Results.Ok(status);
        });
        group.MapPost("/{id:guid}/cancel", async (Guid id, ClaimsPrincipal user, IAnalysisQueue queue, CancellationToken ct) =>
            await queue.CancelAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, id, ct)
                ? Results.NoContent() : Results.NotFound());
        group.MapPost("/{id:guid}/retry", async (Guid id, ClaimsPrincipal user, IAnalysisQueue queue, CancellationToken ct) =>
            await queue.RetryAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, id, ct)
                ? Results.Accepted($"/api/v1/analysis-jobs/{id}", new { id }) : Results.NotFound());
        return endpoints;
    }
}
