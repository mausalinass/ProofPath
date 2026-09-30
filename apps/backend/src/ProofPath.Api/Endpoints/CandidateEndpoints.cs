using System.Security.Claims;
using ProofPath.Domain.Entities;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProofPath.Application.Candidates;
using ProofPath.Application.GitHub;
using ProofPath.Infrastructure.Identity;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Api.Endpoints;

public static class CandidateEndpoints
{
    public static IEndpointRouteBuilder MapCandidateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").RequireAuthorization();
        group.MapGet("/profile", async (ClaimsPrincipal user, CandidateProfileService profiles, CancellationToken ct) =>
        {
            var profile = await profiles.GetAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, ct);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        });
        group.MapPut("/profile", async (ProfileRequest request, ClaimsPrincipal user,
            CandidateProfileService profiles, CancellationToken ct) =>
        {
            return Results.Ok(await profiles.SaveAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!,
                new ProfileInput(request.FirstName, request.LastName, request.Headline, request.Location,
                    request.WorkAuthorization, request.EducationSummary), ct));
        });
        group.MapGet("/home", async (ClaimsPrincipal user, CandidateProfileService profiles, ProofPathDbContext database, CancellationToken ct) =>
        {
            var profile = await profiles.GetAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, ct);
            return Results.Ok(new
            {
                profile,
                profileComplete = !string.IsNullOrWhiteSpace(profile?.FirstName),
                nextAction = string.IsNullOrWhiteSpace(profile?.FirstName) ? "complete_profile" : "profile_ready",
                resumeAvailable = profile is not null && await database.Resumes.AnyAsync(resume => resume.CandidateProfileId == profile.Id, ct),
                githubAvailable = profile is not null && await database.ConnectedAccounts.AnyAsync(
                    account => account.CandidateProfileId == profile.Id && account.Status == ConnectedAccountStatus.Active, ct)
            });
        });
        group.MapDelete("/account", async ([FromBody] DeleteAccountRequest request, ClaimsPrincipal principal,
            UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
            ProofPathDbContext database, IGitHubProvider github, CancellationToken ct) =>
        {
            var user = await users.FindByIdAsync(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (user is null) return Results.Unauthorized();
            if (!request.Confirm || string.IsNullOrEmpty(request.Password) || !await users.CheckPasswordAsync(user, request.Password))
                return Results.Problem("Confirm deletion and provide your current password.", statusCode: 400,
                    extensions: new Dictionary<string, object?> { ["code"] = "ACCOUNT_CONFIRMATION_REQUIRED" });
            await using var transaction = await database.Database.BeginTransactionAsync();
            var profiles = await database.CandidateProfiles.FromSqlInterpolated($"""SELECT * FROM "CandidateProfiles" WHERE "UserId" = {user.Id} FOR UPDATE""").ToListAsync();
            var profileIds = profiles.Select(profile => profile.Id).ToArray();
            var installationIds = await (from account in database.ConnectedAccounts
                join installation in database.GitHubInstallations on account.Id equals installation.ConnectedAccountId
                where profileIds.Contains(account.CandidateProfileId)
                select installation.InstallationId).Distinct().ToArrayAsync(ct);
            foreach (var installationId in installationIds) await github.RevokeInstallationAsync(installationId, ct);
            var keys = await database.Resumes.Where(resume => profileIds.Contains(resume.CandidateProfileId)).Select(resume => resume.StorageKey).ToArrayAsync();
            foreach (var key in keys) database.PrivateFileDeletions.Add(new PrivateFileDeletion
                { Id = Guid.NewGuid(), StorageKey = key, CreatedAt = DateTime.UtcNow, NextAttemptAt = DateTime.UtcNow });
            await database.SaveChangesAsync();
            var result = await users.DeleteAsync(user);
            if (!result.Succeeded) return Results.Problem("Account deletion could not be completed.", statusCode: 409);
            await transaction.CommitAsync();
            await signIn.SignOutAsync();
            return Results.NoContent();
        });
        return endpoints;
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record ProfileRequest(string? FirstName, string? LastName, string? Headline,
        string? Location, string? WorkAuthorization, string? EducationSummary);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record DeleteAccountRequest(bool Confirm, string? Password);
}
