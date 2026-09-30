using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using ProofPath.Application.Files;
using ProofPath.Application.Resumes;

namespace ProofPath.Api.Endpoints;

public static class ResumeEndpoints
{
    public static IEndpointRouteBuilder MapResumeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/resumes").RequireAuthorization();
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (ResumeProblem problem) { return Results.Problem(Message(problem.Code), statusCode: problem.Status, extensions: new Dictionary<string, object?> { ["code"] = problem.Code }); }
            catch (InvalidDataException) { return Results.Problem("Invalid upload or file exceeds 10 MB.", statusCode: 400); }
        });
        group.MapGet("/", (ClaimsPrincipal user, IResumeWorkspace workspace, CancellationToken ct) => workspace.ListAsync(UserId(user), ct));
        group.MapPost("/", async (HttpRequest request, ClaimsPrincipal user, IResumeWorkspace workspace, CancellationToken ct) =>
        {
            if (!request.HasFormContentType) throw new ResumeProblem("MULTIPART_REQUIRED");
            if (request.ContentLength > ResumeLimits.MaxBytes + 65536) throw new ResumeProblem("FILE_TOO_LARGE", 413);
            var feature = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = ResumeLimits.MaxBytes + 65536;
            var form = await request.ReadFormAsync(ct);
            if (form.Files.Count != 1 || form.Files[0].Name != "file" || form.Count != 0) throw new ResumeProblem("SINGLE_FILE_REQUIRED");
            var file = form.Files[0]; if (file.Length > ResumeLimits.MaxBytes) throw new ResumeProblem("FILE_TOO_LARGE", 413);
            await using var input = file.OpenReadStream(); using var bytes = new MemoryStream(); var buffer = new byte[32768]; int count;
            while ((count = await input.ReadAsync(buffer, ct)) > 0)
            {
                if (bytes.Length + count > ResumeLimits.MaxBytes) throw new ResumeProblem("FILE_TOO_LARGE", 413);
                bytes.Write(buffer, 0, count);
            }
            var result = await workspace.UploadAsync(UserId(user), file.FileName, file.ContentType, bytes.ToArray(), ct);
            return Results.Accepted($"/api/v1/analysis-jobs/{result.AnalysisJobId}", result);
        });
        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IResumeWorkspace workspace, CancellationToken ct) =>
        {
            var resume = (await workspace.ListAsync(UserId(user), ct)).SingleOrDefault(item => item.Id == id);
            return resume is null ? Results.NotFound() : Results.Ok(resume);
        });
        group.MapGet("/{id:guid}/download", async (Guid id, HttpContext context, IResumeWorkspace workspace, CancellationToken ct) =>
        {
            var file = await workspace.DownloadAsync(UserId(context.User), id, ct);
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return file is null ? Results.NotFound() : Results.File(file.Content, file.ContentType, file.FileName, enableRangeProcessing: false);
        });
        group.MapGet("/{id:guid}/extraction", async (Guid id, ClaimsPrincipal user, IResumeWorkspace workspace, CancellationToken ct) =>
        {
            var review = await workspace.ReviewAsync(UserId(user), id, ct);
            return review is null ? Results.NotFound() : Results.Ok(review);
        });
        group.MapPut("/{id:guid}/extraction", (Guid id, ReviewRequest request, ClaimsPrincipal user, IResumeWorkspace workspace, CancellationToken ct) =>
            workspace.SaveAsync(UserId(user), id, request.Revision, request.Draft, ct));
        group.MapPost("/{id:guid}/confirm", async (Guid id, ConfirmRequest request, ClaimsPrincipal user, IResumeWorkspace workspace, CancellationToken ct) =>
        {
            await workspace.ConfirmAsync(UserId(user), id, request.Revision, ct); return Results.NoContent();
        });
        return endpoints;
    }
    private static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static string Message(string code) => code switch
    {
        "FILE_TOO_LARGE" => "Choose a rÃ©sumÃ© smaller than 10 MB.",
        "PROFILE_REQUIRED" => "Complete your professional profile before uploading a rÃ©sumÃ©.",
        "REVIEW_CONFLICT" => "This review changed in another tab. Reload before saving.",
        "EXTRACTION_ALREADY_CONFIRMED" => "This version is already confirmed. Upload a new version to replace it.",
        "INVALID_REVIEW" => "Review the fields and keep valid source citations. Scores and strength cannot be edited.",
        _ => "The rÃ©sumÃ© could not be accepted. Use a valid text-based PDF or DOCX without macros, embedded files or encryption."
    };
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record ReviewRequest(int Revision, ResumeDraft Draft);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record ConfirmRequest(int Revision);
}
