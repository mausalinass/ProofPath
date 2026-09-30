using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ProofPath.Application.Analysis;
using ProofPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Api.Tests;

[Collection("PostgreSQL API")]
public sealed class AnalysisApiTests(ApiFixture fixture)
{
    private static async Task<HttpResponseMessage> Write(HttpClient client, string path)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }
    private async Task<(HttpClient Client, string Id)> User()
    {
        var client = fixture.Client(); var email = $"{Guid.NewGuid():N}@example.test";
        async Task<HttpResponseMessage> Auth(string action)
        {
            var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/" + action)
            { Content = JsonContent.Create(new { email, password = "Api-test.P4ssword!", firstName = "Ada" }) };
            request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
            return await client.SendAsync(request);
        }
        Assert.Equal(HttpStatusCode.Created, (await Auth("register")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Auth("login")).StatusCode);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        return (client, me.GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task PollingCancelAndRetryEnforceAuthenticationOwnershipAndCsrf()
    {
        var a = await User(); var b = await User(); using var first = a.Client; using var second = b.Client;
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<IAnalysisQueue>();
        var id = await queue.EnqueueAsync(a.Id, AnalysisKind.Resume, Guid.NewGuid(), "api-test-v1", default);
        var path = $"/api/v1/analysis-jobs/{id}";
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Write(second, path + "/cancel")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Write(second, path + "/retry")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await first.PostAsync(path + "/cancel", null)).StatusCode);
        var status = await first.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal("Pending", status.GetProperty("state").GetString());
        Assert.False(status.TryGetProperty("resultJson", out _));
        Assert.Equal(HttpStatusCode.NoContent, (await Write(first, path + "/cancel")).StatusCode);
        status = await first.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal("Cancelled", status.GetProperty("state").GetString());
        var retryId = await queue.EnqueueAsync(a.Id, AnalysisKind.Resume, Guid.NewGuid(), "retry-test-v1", default);
        var db = scope.ServiceProvider.GetRequiredService<ProofPathDbContext>();
        await db.AnalysisJobs.Where(job => job.Id == retryId).ExecuteUpdateAsync(set =>
            set.SetProperty(job => job.State, AnalysisState.Failed).SetProperty(job => job.Retryable, true));
        var response = await Write(first, $"/api/v1/analysis-jobs/{retryId}/retry");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(retryId, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        Assert.Equal("Pending", (await first.GetFromJsonAsync<JsonElement>($"/api/v1/analysis-jobs/{retryId}")).GetProperty("state").GetString());
        await Write(first, $"/api/v1/analysis-jobs/{retryId}/cancel");
    }
}
