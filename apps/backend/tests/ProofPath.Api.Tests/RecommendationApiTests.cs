using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProofPath.Application.Matching;
using ProofPath.Application.Recommendations;
using ProofPath.Application.Resumes;
using ProofPath.Domain.Entities;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Api.Tests;

[Collection("PostgreSQL API")]
public sealed class RecommendationApiTests(ApiFixture fixture)
{
    private const string Password = "Recommendations.P4ssword!";

    [Fact]
    public async Task TrackingRecommendationsAndRescanRemainOwnedAndVersioned()
    {
        var (owner, ownerEmail) = await User();
        using var other = (await User()).Client;
        Guid jobId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ProofPathDbContext>();
            var user = await database.Users.SingleAsync(item => item.Email == ownerEmail);
            var profile = await database.CandidateProfiles.SingleAsync(item => item.UserId == user.Id);
            if (!await database.Skills.AnyAsync(item => item.Id == "typescript"))
                database.Skills.Add(new Skill { Id = "typescript", DisplayName = "TypeScript" });
            jobId = Guid.NewGuid();
            var extractionId = Guid.NewGuid();
            var setId = Guid.NewGuid();
            database.Jobs.Add(new Job
            {
                Id = jobId,
                CandidateProfileId = profile.Id,
                Company = "ProofPath",
                Title = "Frontend Engineer",
                Description = new string('x', 120),
                DescriptionVersion = 1,
                Status = JobStatus.Confirmed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            database.JobRequirementExtractions.Add(new JobRequirementExtraction
            {
                Id = extractionId,
                JobId = jobId,
                DescriptionVersion = 1,
                Revision = 1,
                MachineJson = "{}",
                DraftJson = "{}",
                ConfirmedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });
            database.RequirementSets.Add(new RequirementSet
            {
                Id = setId,
                JobId = jobId,
                JobRequirementExtractionId = extractionId,
                Version = 1,
                Active = true,
                ConfirmedAt = DateTime.UtcNow
            });
            database.JobRequirements.Add(new JobRequirement
            {
                Id = Guid.NewGuid(),
                RequirementSetId = setId,
                Key = "typescript",
                Category = RequirementCategory.TechnicalSkill,
                Level = RequirementLevel.Required,
                Importance = RequirementImportance.Critical,
                State = RequirementState.Confirmed,
                OriginalWording = "TypeScript",
                SkillTerm = "TypeScript",
                SkillId = "typescript",
                NormalizationStatus = RequirementNormalizationStatus.Exact,
                SourceBlockId = "jd-1",
                Quote = "TypeScript is required",
                IsEvaluable = true,
                IsScoreEligible = true
            });
            database.EvidenceItems.Add(new EvidenceItem
            {
                Id = Guid.NewGuid(),
                CandidateProfileId = profile.Id,
                SkillId = "typescript",
                OriginalTerm = "TypeScript",
                Context = "Used TypeScript in a small prototype",
                Strength = "Weak",
                ExtractionConfidence = 0.8m,
                EvidenceType = "Implementation",
                Lifecycle = "Active",
                SourceBlockId = "resume-1",
                Quote = "Built a small TypeScript prototype",
                ObservedAt = DateTime.UtcNow
            });
            await database.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/jobs/{jobId}/tracking")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(other, $"/api/v1/jobs/{jobId}/rescan", new { })).StatusCode);
        var saved = await Put(owner, $"/api/v1/jobs/{jobId}/tracking",
            new JobTrackingUpdate(ApplicationStage.Applied, "Follow up Friday", DateTime.UtcNow.AddDays(3)));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var tracking = (await saved.Content.ReadFromJsonAsync<JobTrackingView>(ResumeJson.Options))!;
        Assert.Equal(ApplicationStage.Applied, tracking.Stage);
        Assert.Equal("Follow up Friday", tracking.Notes);

        var firstResponse = await Post(owner, $"/api/v1/jobs/{jobId}/rescan", new { });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var first = (await firstResponse.Content.ReadFromJsonAsync<MatchView>(ResumeJson.Options))!;
        var recommendations = (await owner.GetFromJsonAsync<RecommendationView[]>($"/api/v1/jobs/{jobId}/recommendations", ResumeJson.Options))!;
        Assert.Single(recommendations);
        Assert.Equal(first.Id, recommendations[0].MatchResultId);
        Assert.Equal(1, recommendations[0].Rank);

        var updated = await Put(owner, $"/api/v1/jobs/{jobId}/recommendations/{recommendations[0].Id}",
            new RecommendationUpdate(RecommendationStatus.InProgress));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(RecommendationStatus.InProgress,
            (await updated.Content.ReadFromJsonAsync<RecommendationView>(ResumeJson.Options))!.Status);

        var secondResponse = await Post(owner, $"/api/v1/jobs/{jobId}/rescan", new { });
        var second = (await secondResponse.Content.ReadFromJsonAsync<MatchView>(ResumeJson.Options))!;
        Assert.NotEqual(first.Id, second.Id);
        var history = (await owner.GetFromJsonAsync<ScoreHistoryPoint[]>($"/api/v1/jobs/{jobId}/score-history", ResumeJson.Options))!;
        Assert.Equal(2, history.Length);
        Assert.Equal(second.Id, history[0].MatchResultId);
        Assert.NotNull(history[0].Delta);
        var latestRecommendations = (await owner.GetFromJsonAsync<RecommendationView[]>($"/api/v1/jobs/{jobId}/recommendations", ResumeJson.Options))!;
        Assert.All(latestRecommendations, item => Assert.Equal(second.Id, item.MatchResultId));
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object body) =>
        await Send(client, HttpMethod.Post, path, body);

    private static async Task<HttpResponseMessage> Put(HttpClient client, string path, object body) =>
        await Send(client, HttpMethod.Put, path, body);

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object body)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }

    private async Task<(HttpClient Client, string Email)> User()
    {
        var client = fixture.Client();
        var email = $"{Guid.NewGuid():N}@example.test";
        Assert.Equal(HttpStatusCode.Created, (await Post(client, "/api/v1/auth/register",
            new { email, password = Password, firstName = "Ada", lastName = "Lovelace" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/v1/auth/login",
            new { email, password = Password })).StatusCode);
        return (client, email);
    }
}
