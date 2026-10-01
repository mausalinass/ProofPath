using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProofPath.Application.Analysis;
using ProofPath.Application.GitHub;
using ProofPath.Infrastructure.GitHub;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Api.Tests;

public sealed class FakeGitHubProvider : IGitHubProvider
{
    public Uri BuildInstallationUri(string state) => new($"https://github.test/install?state={Uri.EscapeDataString(state)}");
    public Task<VerifiedGitHubInstallation> VerifyInstallationAsync(long installationId, string authorizationCode, string pkceVerifier, CancellationToken ct) =>
        Task.FromResult(new VerifiedGitHubInstallation(42, "octocat", installationId, 42, "octocat", "User", "selected", new Dictionary<string, string> { ["contents"] = "read", ["metadata"] = "read" }));
    public Task<GitHubInstallationAccess> CreateInstallationAccessAsync(long installationId, IReadOnlyCollection<long> repositoryIds, CancellationToken ct) =>
        Task.FromResult(new GitHubInstallationAccess("ephemeral-test-token", DateTime.UtcNow.AddHours(1)));
    public Task<IReadOnlyList<GitHubRepositoryDescriptor>> ListRepositoriesAsync(long installationId, CancellationToken ct) => Task.FromResult<IReadOnlyList<GitHubRepositoryDescriptor>>(
        Enumerable.Range(1, 6).Select(index => new GitHubRepositoryDescriptor(index, "octocat", $"repo-{index}", $"octocat/repo-{index}", index == 2, "main", $"https://github.test/octocat/repo-{index}")).ToArray());
    public Task<GitHubRepositorySnapshot> ReadSnapshotAsync(long installationId, long repositoryId, CancellationToken ct)
    {
        var repository = new GitHubRepositoryDescriptor(repositoryId, "octocat", $"repo-{repositoryId}", $"octocat/repo-{repositoryId}", false, "main", $"https://github.test/octocat/repo-{repositoryId}");
        return Task.FromResult(new GitHubRepositorySnapshot(repository, new string((char)('a' + repositoryId), 40), new Dictionary<string, long> { ["C#"] = 1000, ["TypeScript"] = 500 },
        [new("src/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PackageReference Include=\"Microsoft.EntityFrameworkCore\"/><PackageReference Include=\"Npgsql\"/></Project>", 150),
         new("src/Data.cs", "services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connection));", 70),
         new("web/package.json", "{\"dependencies\":{\"react\":\"19\",\"typescript\":\"6\"},\"devDependencies\":{\"vite\":\"8\",\"vitest\":\"5\"}}", 120),
         new("web/App.tsx", "export function App(){ const [value] = useState(0); return <main>{value}</main> }", 90),
         new("Dockerfile", "FROM mcr.microsoft.com/dotnet/aspnet:10.0", 45),
         new(".github/workflows/ci.yml", "jobs: { build: { steps: [] } }", 40)], 25, false, 6, 515, []));
    }
    public Task RevokeInstallationAsync(long installationId, CancellationToken ct) => Task.CompletedTask;
}

public sealed class RepositoryAnalyzerTests
{
    [Fact]
    public void DetectionIsDeterministicAndDoesNotTurnDependencyPresenceIntoStrongEvidence()
    {
        var repository = new GitHubRepositoryDescriptor(1, "o", "r", "o/r", false, "main", "https://example.test/o/r");
        var snapshot = new GitHubRepositorySnapshot(repository, new string('a', 40), new Dictionary<string, long>(),
            [new("package.json", "{\"dependencies\":{\"react\":\"19\"}}", 35)], 1, false, 1, 35, []);
        var analyzer = new DeterministicRepositoryAnalyzer(); var first = analyzer.Analyze(snapshot); var second = analyzer.Analyze(snapshot);
        var react = Assert.Single(first.Evidence, item => item.SkillKey == "react"); Assert.Equal("Moderate", react.Strength); Assert.Equal("Configuration", react.EvidenceType);
        Assert.Contains(first.Evidence, item => item.SkillKey == "nodejs" && item.Strength == "Weak");
        Assert.Equal(first.Evidence.Select(item => (item.SkillKey, item.Strength, item.ExtractionConfidence)), second.Evidence.Select(item => (item.SkillKey, item.Strength, item.ExtractionConfidence)));
        Assert.DoesNotContain(first.Evidence, item => item.OriginalTerm.Contains("README", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReadmeClaimsStayWeakAndEmptyTestTemplatesDoNotBecomeEvidence()
    {
        var repository = new GitHubRepositoryDescriptor(2, "o", "r", "o/r", false, "main", "https://example.test/o/r");
        var snapshot = new GitHubRepositorySnapshot(repository, new string('b', 40), new Dictionary<string, long>(),
            [new("README.md", "This project uses React.", 24), new("TemplateTests.cs", "public sealed class TemplateTests { }", 38)], 2, false, 2, 62, []);
        var result = new DeterministicRepositoryAnalyzer().Analyze(snapshot);
        Assert.Contains(result.Evidence, item => item.SkillKey == "react" && item.EvidenceType == "Claim" && item.Strength == "Weak");
        Assert.DoesNotContain(result.Evidence, item => item.SkillKey == "xunit");
    }
}

public sealed class GitHubProviderContractTests
{
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request);
    }
    private static IConfiguration Configuration()
    {
        using var rsa = RSA.Create(2048);
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GitHub:AppId"] = "123",
            ["GitHub:ClientId"] = "client",
            ["GitHub:ClientSecret"] = "secret",
            ["GitHub:AppSlug"] = "proofpath-test",
            ["GitHub:PrivateKeyPem"] = rsa.ExportRSAPrivateKeyPem()
        }).Build();
    }
    private static Dictionary<string, object?> Repository(long id) => new()
    {
        ["id"] = id,
        ["owner"] = new { login = "octocat" },
        ["name"] = $"repo-{id}",
        ["full_name"] = $"octocat/repo-{id}",
        ["private"] = false,
        ["default_branch"] = "main",
        ["html_url"] = $"https://github.test/octocat/repo-{id}"
    };
    private static HttpResponseMessage Json(HttpStatusCode status, object body) => new(status) { Content = JsonContent.Create(body) };
    private static HttpResponseMessage Token() => Json(HttpStatusCode.OK, new Dictionary<string, object?>
    { ["token"] = "ephemeral", ["expires_at"] = DateTime.UtcNow.AddHours(1) });

    [Fact]
    public async Task RepositoryDiscoveryPaginatesAndClassifiesRateLimitsAsRetryable()
    {
        var pages = new List<int>();
        using var http = new HttpClient(new Handler(request =>
        {
            if (request.Method == HttpMethod.Post) return Task.FromResult(Token());
            var page = request.RequestUri!.Query.Contains("page=2", StringComparison.Ordinal) ? 2 : 1; pages.Add(page);
            var repositories = page == 1 ? Enumerable.Range(1, 100).Select(index => Repository(index)).ToArray() : [Repository(101)];
            return Task.FromResult(Json(HttpStatusCode.OK, new { repositories }));
        }));
        var provider = new GitHubAppProvider(http, Configuration());
        Assert.Equal(101, (await provider.ListRepositoriesAsync(9, default)).Count); Assert.Equal([1, 2], pages);

        using var limitedHttp = new HttpClient(new Handler(request => Task.FromResult(request.Method == HttpMethod.Post ? Token() : new HttpResponseMessage(HttpStatusCode.TooManyRequests))));
        var failure = await Assert.ThrowsAsync<AnalysisFailure>(() => new GitHubAppProvider(limitedHttp, Configuration()).ListRepositoriesAsync(9, default));
        Assert.Equal("GITHUB_RATE_LIMITED", failure.Code); Assert.True(failure.Retryable);
    }

    [Fact]
    public async Task SnapshotIsPinnedToShaAndStopsAtTheFileBudget()
    {
        var revision = new string('c', 40); var blobs = 0;
        using var http = new HttpClient(new Handler(request =>
        {
            var uri = request.RequestUri!.AbsoluteUri;
            if (request.Method == HttpMethod.Post) return Task.FromResult(Token());
            if (uri.EndsWith("/repositories/7", StringComparison.Ordinal)) return Task.FromResult(Json(HttpStatusCode.OK, Repository(7)));
            if (uri.Contains("/git/ref/heads/main", StringComparison.Ordinal)) return Task.FromResult(Json(HttpStatusCode.OK, new { @object = new { sha = revision } }));
            if (uri.Contains("/git/commits/", StringComparison.Ordinal)) return Task.FromResult(Json(HttpStatusCode.OK, new { tree = new { sha = "tree-sha" } }));
            if (uri.Contains("/git/trees/", StringComparison.Ordinal)) return Task.FromResult(Json(HttpStatusCode.OK, new
            {
                tree = Enumerable.Range(1, 151).Select(index => new { path = $"src/File{index}.cs", type = "blob", sha = $"blob-{index}", size = 10 }).ToArray(),
                truncated = false
            }));
            if (uri.EndsWith("/languages", StringComparison.Ordinal)) return Task.FromResult(Json(HttpStatusCode.OK, new Dictionary<string, long> { ["C#"] = 1510 }));
            if (uri.Contains("/git/blobs/", StringComparison.Ordinal)) { blobs++; return Task.FromResult(Json(HttpStatusCode.OK, new { content = Convert.ToBase64String("class A{}"u8.ToArray()), encoding = "base64" })); }
            throw new InvalidOperationException(uri);
        }));
        var snapshot = await new GitHubAppProvider(http, Configuration()).ReadSnapshotAsync(9, 7, default);
        Assert.Equal(revision, snapshot.RevisionSha); Assert.Equal(150, snapshot.Files.Count); Assert.Equal(150, blobs);
        Assert.Contains("FILE_COUNT_LIMIT_REACHED", snapshot.Warnings);
    }

    [Fact]
    public async Task RevokingAnAlreadyRemovedInstallationIsIdempotent()
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))));
        await new GitHubAppProvider(http, Configuration()).RevokeInstallationAsync(9, default);
    }
}

[Collection("PostgreSQL API")]
public sealed class GitHubSprintTests(ApiFixture fixture)
{
    private const string Password = "Test-only.P4ssword!";
    private static async Task<HttpResponseMessage> Write(HttpClient client, HttpMethod method, string path, object body)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf"); using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString()); return await client.SendAsync(request);
    }
    private async Task<HttpClient> User()
    {
        var client = fixture.Client(); var email = $"{Guid.NewGuid():N}@example.test";
        Assert.Equal(HttpStatusCode.Created, (await Write(client, HttpMethod.Post, "/api/v1/auth/register", new { email, password = Password, firstName = "Grace" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Write(client, HttpMethod.Post, "/api/v1/auth/login", new { email, password = Password })).StatusCode); return client;
    }
    [Fact]
    public async Task ConnectionSelectionAnalysisEvidenceAndDisconnectAreOwnerScoped()
    {
        using var client = await User(); var connect = await Write(client, HttpMethod.Post, "/api/v1/github/connect", new { });
        var url = new Uri((await connect.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!);
        var state = Uri.UnescapeDataString(url.Query.Split("state=")[1]);
        var callback = await client.GetAsync($"/api/v1/github/callback?state={Uri.EscapeDataString(state)}&installation_id=99&code=fixture"); Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/v1/github/callback?state={Uri.EscapeDataString(state)}&installation_id=99&code=fixture")).StatusCode);
        var repositories = await client.GetFromJsonAsync<RepositoryView[]>("/api/v1/github/repositories?sync=true"); Assert.Equal(6, repositories!.Length); Assert.All(repositories, item => Assert.False(item.IncludedForAnalysis));
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, HttpMethod.Put, "/api/v1/github/repositories/selection", new { repositoryIds = Enumerable.Range(1, 6).Select(i => (long)i) })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Write(client, HttpMethod.Put, "/api/v1/github/repositories/selection", new { repositoryIds = new[] { 500L } })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Write(client, HttpMethod.Put, "/api/v1/github/repositories/selection", new { repositoryIds = new[] { 1L, 2L } })).StatusCode);
        var scan = await Write(client, HttpMethod.Post, "/api/v1/github/scans", new { }); var jobs = (await scan.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("analysisJobIds").EnumerateArray().Select(item => item.GetGuid()).ToArray(); Assert.Equal(2, jobs.Length);
        foreach (var expected in jobs)
        {
            using var scope = fixture.Factory.Services.CreateScope(); var queue = scope.ServiceProvider.GetRequiredService<IAnalysisQueue>(); var lease = await queue.ClaimAsync(default); Assert.NotNull(lease); Assert.Equal(expected, lease.Id);
            var handler = scope.ServiceProvider.GetServices<IAnalysisHandler>().Single(item => item.Kind == lease.Kind); var output = await handler.ProcessAsync(lease, default); Assert.True(await queue.CompleteAsync(lease, output, default));
        }
        var evidence = await client.GetFromJsonAsync<GitHubEvidenceView[]>("/api/v1/github/evidence"); Assert.NotEmpty(evidence!); Assert.All(evidence!, item => Assert.Equal("Active", item.Lifecycle));
        using var other = await User(); Assert.Empty((await other.GetFromJsonAsync<GitHubEvidenceView[]>("/api/v1/github/evidence"))!);
        Assert.Equal(HttpStatusCode.NoContent, (await Write(client, HttpMethod.Post, "/api/v1/github/disconnect", new { })).StatusCode);
        evidence = await client.GetFromJsonAsync<GitHubEvidenceView[]>("/api/v1/github/evidence"); Assert.All(evidence!, item => Assert.Equal("Stale", item.Lifecycle));
        var storedEvidence = evidence!; var repositoryIds = storedEvidence.Select(item => item.RepositoryId).Distinct().ToArray(); var evidenceIds = storedEvidence.Select(item => item.Id).ToArray();
        Assert.Equal(HttpStatusCode.NoContent, (await Write(client, HttpMethod.Delete, "/api/v1/account", new { confirm = true, password = Password })).StatusCode);
        await using var verification = fixture.Factory.Services.CreateAsyncScope(); var database = verification.ServiceProvider.GetRequiredService<ProofPathDbContext>();
        Assert.False(await database.Repositories.AnyAsync(item => repositoryIds.Contains(item.Id)));
        Assert.False(await database.EvidenceItems.AnyAsync(item => evidenceIds.Contains(item.Id)));
    }
}
