using System.Security.Claims;
using System.Text.Json.Serialization;
using ProofPath.Application.Jobs;

namespace ProofPath.Api.Endpoints;

public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/jobs").RequireAuthorization();
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (JobProblem problem) { return Results.Problem(Message(problem.Code), statusCode: problem.Status, extensions: new Dictionary<string, object?> { ["code"] = problem.Code }); }
        });
        group.MapGet("/", (ClaimsPrincipal user, IJobWorkspace workspace, CancellationToken ct) => workspace.ListAsync(UserId(user), ct));
        group.MapGet("/skills", (IJobWorkspace workspace, CancellationToken ct) => workspace.SkillsAsync(ct));
        group.MapPost("/", async (JobRequest request, ClaimsPrincipal user, IJobWorkspace workspace, CancellationToken ct) =>
        {
            var created = await workspace.CreateAsync(UserId(user), request.Input(), ct);
            return Results.Accepted($"/api/v1/analysis-jobs/{created.AnalysisJobId}", created);
        });
        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IJobWorkspace workspace, CancellationToken ct) =>
            await workspace.GetAsync(UserId(user), id, ct) is { } job ? Results.Ok(job) : Results.NotFound());
        group.MapPut("/{id:guid}", async (Guid id, JobRequest request, ClaimsPrincipal user, IJobWorkspace workspace, CancellationToken ct) =>
            Results.Ok(await workspace.UpdateAsync(UserId(user), id, request.Input(), ct)));
        group.MapGet("/{id:guid}/requirements", async (Guid id, ClaimsPrincipal user, IJobWorkspace workspace, CancellationToken ct) =>
            await workspace.ReviewAsync(UserId(user), id, ct) is { } review ? Results.Ok(review) : Results.NotFound());
        group.MapPut("/{id:guid}/requirements", (Guid id, ReviewRequest request, ClaimsPrincipal user, IJobWorkspace workspace, CancellationToken ct) =>
            workspace.SaveReviewAsync(UserId(user), id, request.Revision, request.Draft, ct));
        group.MapPost("/{id:guid}/confirm", async (Guid id, ConfirmRequest request, ClaimsPrincipal user, IJobWorkspace workspace, CancellationToken ct) =>
        { await workspace.ConfirmAsync(UserId(user), id, request.Revision, ct); return Results.NoContent(); });
        return endpoints;
    }
    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static string Message(string code) => code switch
    {
        "JOB_DESCRIPTION_LENGTH" => "Enter a job description between 100 and 50,000 characters.",
        "JOB_SOURCE_URL_INVALID" => "Use a valid http or https source URL.",
        "JOB_VERSION_CONFLICT" or "JOB_REVIEW_CONFLICT" => "This job changed in another tab. Reload before saving.",
        "JOB_EXTRACTION_OUTDATED" => "The job description changed. Review the newest extraction.",
        "JOB_REQUIREMENTS_CONFIRMED" => "Confirmed requirements are immutable. Edit the job description to create a new version.",
        "INVALID_JOB_REVIEW" => "Keep valid categories, groups, catalog skills and source citations.",
        _ => "The job could not be processed. Review the description and retry."
    };
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record JobRequest(string? Company, string? Title, string Description, string? SourceUrl, int? DescriptionVersion)
    { public JobWrite Input() => new(Company, Title, Description, SourceUrl, DescriptionVersion); }
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record ReviewRequest(int Revision, JobRequirementDraftSet Draft);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record ConfirmRequest(int Revision);
}
