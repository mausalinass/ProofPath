using System.Security.Claims;
using System.Text.Json.Serialization;
using ProofPath.Application.GitHub;

namespace ProofPath.Api.Endpoints;

public static class GitHubEndpoints
{
    public static IEndpointRouteBuilder MapGitHubEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/github").RequireAuthorization();
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (GitHubProblem problem) { return Results.Problem(Message(problem.Code), statusCode: problem.Status, extensions: new Dictionary<string, object?> { ["code"] = problem.Code }); }
        });
        group.MapGet("/status", (ClaimsPrincipal user, IGitHubWorkspace workspace, CancellationToken ct) => workspace.StatusAsync(UserId(user), ct));
        group.MapPost("/connect", async (ClaimsPrincipal user, IGitHubWorkspace workspace, CancellationToken ct) =>
            Results.Ok(new { url = (await workspace.BeginConnectionAsync(UserId(user), ct)).ToString() }));
        group.MapGet("/callback", async (string state, long installation_id, string code,
            IGitHubWorkspace workspace, IConfiguration configuration, CancellationToken ct) =>
        {
            await workspace.CompleteConnectionAsync(state, installation_id, code, ct);
            return Results.Redirect(configuration["Frontend:GitHubReturnUrl"] ?? "http://localhost:5173/onboarding/github?github=connected");
        }).AllowAnonymous();
        group.MapPost("/disconnect", async (ClaimsPrincipal user, IGitHubWorkspace workspace, CancellationToken ct) =>
        { await workspace.DisconnectAsync(UserId(user), ct); return Results.NoContent(); });
        group.MapGet("/repositories", (bool? sync, ClaimsPrincipal user, IGitHubWorkspace workspace, CancellationToken ct) =>
            workspace.RepositoriesAsync(UserId(user), sync == true, ct));
        group.MapPut("/repositories/selection", async (SelectionRequest request, ClaimsPrincipal user, IGitHubWorkspace workspace, CancellationToken ct) =>
        { await workspace.SaveSelectionAsync(UserId(user), request.RepositoryIds, ct); return Results.NoContent(); });
        group.MapPost("/scans", async (ClaimsPrincipal user, IGitHubWorkspace workspace, CancellationToken ct) =>
        { var ids = await workspace.ScanAsync(UserId(user), ct); return Results.Accepted(value: new { analysisJobIds = ids }); });
        group.MapGet("/evidence", (ClaimsPrincipal user, IGitHubWorkspace workspace, CancellationToken ct) => workspace.EvidenceAsync(UserId(user), ct));
        return endpoints;
    }
    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static string Message(string code) => code switch
    {
        "GITHUB_NOT_CONFIGURED" => "GitHub App credentials are not configured for this environment.",
        "GITHUB_STATE_INVALID" or "GITHUB_STATE_REPLAYED" => "This GitHub connection request expired or was already used. Start again.",
        "GITHUB_PERMISSIONS_INVALID" => "The GitHub App must have read-only Contents and Metadata permissions.",
        "REPOSITORY_LIMIT_EXCEEDED" => "Select no more than five repositories.",
        "REPOSITORY_NOT_AUTHORIZED" => "One or more repositories are outside the GitHub installation.",
        "REPOSITORY_SELECTION_REQUIRED" => "Select at least one repository before analysis.",
        _ => "GitHub could not complete this request. Retry or reconnect the source."
    };
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record SelectionRequest(long[] RepositoryIds);
}
