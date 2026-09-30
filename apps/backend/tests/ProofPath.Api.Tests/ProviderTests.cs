using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ProofPath.Application.Analysis;
using ProofPath.Application.Resumes;
using ProofPath.Infrastructure.Resumes;

namespace ProofPath.Api.Tests;

public sealed class ProviderTests
{
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request);
    }
    private static IConfiguration Config(string? key = "fixture-only") => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    { ["OpenAI:ApiKey"] = key }).Build();
    private static DocumentText Source => new([new SourceBlock("p1", 1, ResumeFixtures.Text)], []);
    [Fact] public async Task MissingKeyDoesNotMakeNetworkCall()
    {
        using var http = new HttpClient(new Handler(_ => throw new Exception("Must not call network")));
        var provider = new OpenAiResumeProvider(http, Config(null), new ProviderCircuit());
        var error = await Assert.ThrowsAsync<AnalysisFailure>(() => provider.ExtractResumeAsync(Source, default));
        Assert.Equal("PROVIDER_NOT_CONFIGURED", error.Code); Assert.True(error.Retryable);
    }
    [Fact] public async Task UsesRequestedModelStructuredOutputAndNoResponseStorage()
    {
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.Equal("https://api.openai.com/v1/responses", request.RequestUri!.ToString());
            var body = await request.Content!.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("gpt-6-astra", body.GetProperty("model").GetString());
            Assert.Equal("medium", body.GetProperty("reasoning").GetProperty("effort").GetString());
            Assert.False(body.GetProperty("store").GetBoolean());
            Assert.True(body.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new
            {
                status = "completed", output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = ResumeJson.Serialize(ResumeFixtures.Draft(Source)) } } } },
                usage = new { input_tokens = 150, output_tokens = 80 }
            }) };
        }));
        var result = await new OpenAiResumeProvider(http, Config(), new ProviderCircuit()).ExtractResumeAsync(Source, default);
        Assert.Equal("Engineer", result.Draft.Facts[0].Name); Assert.Equal(150, result.InputTokens);
    }
    [Fact] public async Task RepeatedTransientFailuresOpenCircuitWithoutLeakingResponse()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(_ => { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("private-provider-content") }); }));
        var provider = new OpenAiResumeProvider(http, Config(), new ProviderCircuit());
        for (var index = 0; index < 3; index++)
        {
            var error = await Assert.ThrowsAsync<AnalysisFailure>(() => provider.ExtractResumeAsync(Source, default));
            Assert.Equal("PROVIDER_UNAVAILABLE", error.Code); Assert.DoesNotContain("private", error.Message);
        }
        Assert.Equal("PROVIDER_CIRCUIT_OPEN", (await Assert.ThrowsAsync<AnalysisFailure>(() => provider.ExtractResumeAsync(Source, default))).Code);
        Assert.Equal(3, calls);
    }
    [Fact] public async Task IncompleteOutputNeverBecomesFacts()
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = JsonContent.Create(new { status = "incomplete" }) })));
        var error = await Assert.ThrowsAsync<AnalysisFailure>(() => new OpenAiResumeProvider(http, Config(), new ProviderCircuit()).ExtractResumeAsync(Source, default));
        Assert.Equal("EXTRACTION_INCOMPLETE", error.Code);
    }
}
