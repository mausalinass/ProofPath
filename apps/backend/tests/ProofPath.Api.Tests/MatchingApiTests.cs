using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProofPath.Application.Matching;
using ProofPath.Application.Resumes;
using ProofPath.Domain.Entities;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Api.Tests;

[Collection("PostgreSQL API")]
public sealed class MatchingApiTests(ApiFixture fixture)
{
    private const string Password = "Matching-tests.P4ssword!";

    [Fact]
    public async Task CalculatePersistsImmutableHistoryAndEnforcesOwnership()
    {
        var (owner, ownerEmail) = await User();
        using var other = (await User()).Client;
        using var anonymous = fixture.Client();
        Guid jobId;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ProofPathDbContext>();
            var user = await database.Users.SingleAsync(item => item.Email == ownerEmail);
            var profile = await database.CandidateProfiles.SingleAsync(item => item.UserId == user.Id);
            if (!await database.Skills.AnyAsync(item => item.Id == "csharp"))
                database.Skills.Add(new Skill { Id = "csharp", DisplayName = "C#" });
            jobId = Guid.NewGuid();
            var extractionId = Guid.NewGuid();
            var setId = Guid.NewGuid();
            database.Jobs.Add(new Job
            {
                Id = jobId,
                CandidateProfileId = profile.Id,
                Company = "Acme",
                Title = "Engineer",
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
                Key = "csharp",
                Category = RequirementCategory.TechnicalSkill,
                Level = RequirementLevel.Required,
                Importance = RequirementImportance.High,
                State = RequirementState.Confirmed,
                OriginalWording = "C#",
                SkillTerm = "C#",
                SkillId = "csharp",
                NormalizationStatus = RequirementNormalizationStatus.Exact,
                SourceBlockId = "jd-1",
                Quote = "C# is required",
                IsEvaluable = true,
                IsScoreEligible = true
            });
            database.EvidenceItems.Add(new EvidenceItem
            {
                Id = Guid.NewGuid(),
                CandidateProfileId = profile.Id,
                SkillId = "csharp",
                OriginalTerm = "C#",
                Context = "Built an API",
                Strength = "Strong",
                ExtractionConfidence = 1,
                EvidenceType = "Implementation",
                Lifecycle = "Active",
                SourceBlockId = "resume-1",
                Quote = "Built an API in C#",
                ObservedAt = DateTime.UtcNow
            });
            await database.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(anonymous, $"/api/v1/jobs/{jobId}/matches/", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(other, $"/api/v1/jobs/{jobId}/matches/", new { })).StatusCode);
        var firstResponse = await Post(owner, $"/api/v1/jobs/{jobId}/matches/", new { });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var first = (await firstResponse.Content.ReadFromJsonAsync<MatchView>(ResumeJson.Options))!;
        var second = (await (await Post(owner, $"/api/v1/jobs/{jobId}/matches/", new { })).Content.ReadFromJsonAsync<MatchView>(ResumeJson.Options))!;
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.Result.OverallScore, second.Result.OverallScore);
        Assert.Equal("matching-v1", first.ScoringVersion);

        var history = (await owner.GetFromJsonAsync<MatchSummary[]>($"/api/v1/jobs/{jobId}/matches/", ResumeJson.Options))!;
        Assert.Equal(2, history.Length);
        var latest = (await owner.GetFromJsonAsync<MatchView>($"/api/v1/jobs/{jobId}/matches/latest", ResumeJson.Options))!;
        Assert.Equal(second.Id, latest.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/jobs/{jobId}/matches/latest")).StatusCode);
        await using var verifyScope = fixture.Factory.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<ProofPathDbContext>();
        Assert.Equal(2, await verify.MatchResults.CountAsync(item => item.JobId == jobId));
        Assert.Equal(2, await verify.RequirementMatchRecords.CountAsync(item => item.RequirementId == first.Result.Requirements.Single().RequirementId));
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object body)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }
    private async Task<(HttpClient Client, string Email)> User()
    {
        var client = fixture.Client(); var email = $"{Guid.NewGuid():N}@example.test";
        Assert.Equal(HttpStatusCode.Created, (await Post(client, "/api/v1/auth/register",
            new { email, password = Password, firstName = "Ada", lastName = "Lovelace" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/v1/auth/login",
            new { email, password = Password })).StatusCode);
        return (client, email);
    }
}
