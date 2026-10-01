using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Api.Tests;

[Collection("PostgreSQL API")]
public sealed class ProductionReadinessTests(ApiFixture fixture)
{
    [Fact]
    public async Task LivenessAndReadinessAreMachineReadable()
    {
        using var client = fixture.Client();
        foreach (var endpoint in new[] { "/health/live", "/health/ready", "/health" })
        {
            var response = await client.GetAsync(endpoint);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("healthy", body.GetProperty("status").GetString());
        }
    }

    [Fact]
    public async Task ResponsesCarrySecurityAndTraceHeaders()
    {
        using var client = fixture.Client();
        var response = await client.GetAsync("/health/live");
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.True(response.Headers.Contains("X-Trace-Id"));
    }

    [Fact]
    public async Task DataProtectionKeysArePersistedInDatabase()
    {
        using var client = fixture.Client();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/csrf")).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ProofPathDbContext>();
        Assert.True(await database.DataProtectionKeys.AnyAsync());
    }

    [Fact]
    public async Task TemplateWeatherEndpointIsNotExposed()
    {
        using var client = fixture.Client();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/weatherforecast")).StatusCode);
    }
}
