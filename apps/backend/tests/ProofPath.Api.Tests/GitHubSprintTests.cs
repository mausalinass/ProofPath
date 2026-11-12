using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProofPath.Application.Analysis;
using ProofPath.Application.GitHub;
using ProofPath.Domain.Entities;
using ProofPath.Infrastructure.GitHub;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Api.Tests;

public sealed class FakeGitHubProvider : IGitHubProvider
{
    public Uri BuildInstallationUri(string state) => new($"https://github.test/install?state={Uri.EscapeDataString(state)}");
    public Uri BuildAuthorizationUri(string state, string challenge) => new($"https://github.test/authorize?state={Uri.EscapeDataString(state)}");
    public Task<VerifiedGitHubInstallation?> VerifyInstallationAsync(string code, string verifier, long? installationId, CancellationToken ct)
    {
        var identity = code switch
        {
            "existing" => (User: 42L, Installation: 99L, Login: "octocat"),
            "alice" => (User: 43L, Installation: 100L, Login: "synthetic-a"),
            "bob" => (User: 44L, Installation: 200L, Login: "synthetic-b"),
            _ => (User: 0L, Installation: 0L, Login: "")
        };
        if (installationId is not null && installationId != identity.Installation)
            throw new GitHubProblem("GITHUB_INSTALLATION_NOT_AUTHORIZED", 403);
        return Task.FromResult<VerifiedGitHubInstallation?>(identity.User == 0 ? null : new(identity.User, identity.Login,
            identity.Installation, identity.User, identity.Login, "User", "selected",
            new Dictionary<string, string> { ["contents"] = "read", ["metadata"] = "read" }));
    }
    public Task<GitHubInstallationAccess> CreateInstallationAccessAsync(long installationId, IReadOnlyCollection<long> repositoryIds, CancellationToken ct) =>
        Task.FromResult(new GitHubInstallationAccess("ephemeral-test-token", DateTime.UtcNow.AddHours(1)));
    public Task<IReadOnlyList<GitHubRepositoryDescriptor>> ListRepositoriesAsync(long installationId, CancellationToken ct) => Task.FromResult<IReadOnlyList<GitHubRepositoryDescriptor>>(
        Enumerable.Range(installationId == 200 ? 101 : 1, 6).Select(index => new GitHubRepositoryDescriptor(index, "octocat", $"repo-{index}", $"octocat/repo-{index}", index == 2, "main", $"https://github.test/octocat/repo-{index}")).ToArray());
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

    [Theory]
    [InlineData(42, "User", 77, true)]
    [InlineData(43, "User", 77, false)]
    [InlineData(42, "Organization", 77, false)]
    [InlineData(42, "User", 88, false)]
    public async Task AuthorizationUsesTheUserTokenAndRequiresTheClaimedPersonalInstallation(
        long installationOwner, string targetType, long requestedInstallation, bool authorized)
    {
        using var http = new HttpClient(new Handler(async request =>
        {
            var url = request.RequestUri!;
            if (url.AbsolutePath == "/login/oauth/access_token")
            {
                Assert.Equal("github.com", url.Host);
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Empty(url.Query);
                var body = await request.Content!.ReadAsStringAsync();
                Assert.Contains("code_verifier=synthetic-verifier", body);
                Assert.Contains("code=synthetic-code", body);
                return Json(HttpStatusCode.OK, new { access_token = "synthetic-user-token", token_type = "bearer" });
            }
            Assert.Equal("api.github.com", url.Host);
            Assert.Equal("synthetic-user-token", request.Headers.Authorization!.Parameter);
            return url.AbsolutePath switch
            {
                "/user" => Json(HttpStatusCode.OK, new { id = 42, login = "synthetic-user" }),
                "/user/installations" => Json(HttpStatusCode.OK, new { installations = new[]
                {
                    new { id = 77, account = new { id = installationOwner }, target_type = targetType,
                        permissions = new { metadata = "read", contents = "read" } }
                } }),
                _ => throw new InvalidOperationException(url.AbsolutePath)
            };
        }));
        var provider = new GitHubAppProvider(http, Configuration());
        var uri = provider.BuildAuthorizationUri("synthetic-state", "synthetic-challenge");
        Assert.Contains("code_challenge_method=S256", uri.Query);
        Assert.Contains("state=synthetic-state", uri.Query);
        if (authorized)
        {
            var result = await provider.VerifyInstallationAsync("synthetic-code", "synthetic-verifier", requestedInstallation, default);
            Assert.Equal(42L, result!.AuthorizingUserId);
            Assert.Equal(77L, result.InstallationId);
        }
        else
        {
            var rejected = await Assert.ThrowsAsync<GitHubProblem>(() =>
                provider.VerifyInstallationAsync("synthetic-code", "synthetic-verifier", requestedInstallation, default));
            Assert.Equal(403, rejected.Status);
        }
    }

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

    [Theory]
    [InlineData(401, "", "GITHUB_APP_CREDENTIALS", false)]
    [InlineData(403, "", "GITHUB_PERMISSIONS_INVALID", false)]
    [InlineData(403, "You have exceeded a secondary rate limit.", "GITHUB_RATE_LIMITED", true)]
    [InlineData(404, "", "GITHUB_ACCESS_LOST", false)]
    [InlineData(422, "", "GITHUB_REPOSITORY_NOT_GRANTED", false)]
    [InlineData(503, "", "GITHUB_UNAVAILABLE", true)]
    public async Task InstallationTokenFailuresAreClassified(int status, string message, string code, bool retryable)
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Json((HttpStatusCode)status, new { message }))));
        var provider = new GitHubAppProvider(http, Configuration());
        var error = await Assert.ThrowsAsync<AnalysisFailure>(() => provider.CreateInstallationAccessAsync(9, [7], default));
        Assert.Equal(code, error.Code);
        Assert.Equal(retryable, error.Retryable);
    }

    [Theory]
    [InlineData(409, "Git Repository is empty.", "GITHUB_REPOSITORY_EMPTY")]
    [InlineData(409, "Conflict", "GITHUB_REPOSITORY_CONFLICT")]
    [InlineData(404, "Not found", "GITHUB_BRANCH_MISSING")]
    public async Task DefaultBranchFailuresDoNotRetry(int status, string message, string code)
    {
        using var http = new HttpClient(new Handler(request => Task.FromResult(request.Method == HttpMethod.Post ? Token()
            : request.RequestUri!.AbsolutePath == "/repositories/7" ? Json(HttpStatusCode.OK, Repository(7))
            : Json((HttpStatusCode)status, new { message }))));
        var error = await Assert.ThrowsAsync<AnalysisFailure>(() => new GitHubAppProvider(http, Configuration()).ReadSnapshotAsync(9, 7, default));
        Assert.Equal(code, error.Code);
        Assert.False(error.Retryable);
    }

    [Fact]
    public async Task InstallationRateLimitHonorsTheLaterOfRetryAfterAndReset()
    {
        var reset = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        using var http = new HttpClient(new Handler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
            response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "0");
            response.Headers.TryAddWithoutValidation("X-RateLimit-Reset", reset.ToString());
            response.Headers.RetryAfter = new(TimeSpan.FromSeconds(60));
            return Task.FromResult(response);
        }));
        var provider = new GitHubAppProvider(http, Configuration());
        var error = await Assert.ThrowsAsync<AnalysisFailure>(() => provider.CreateInstallationAccessAsync(9, [7], default));
        Assert.Equal("GITHUB_RATE_LIMITED", error.Code);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(reset), error.RetryNotBefore);
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
    private static async Task<string> Start(HttpClient client)
    {
        using var connect = await Write(client, HttpMethod.Post, "/api/v1/github/connect", new { });
        Assert.Equal(HttpStatusCode.OK, connect.StatusCode);
        var url = new Uri((await connect.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!);
        return url.Query.Split("state=")[1];
    }
    [Fact]
    public async Task ConnectionSelectionAnalysisEvidenceAndDisconnectAreOwnerScoped()
    {
        using var client = await User(); var state = await Start(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/v1/github/callback?state={state}&installation_id=99&setup_action=unexpected")).StatusCode);
        var callback = await client.GetAsync($"/api/v1/github/callback?state={state}&code=existing"); Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/v1/github/callback?state={state}&code=existing")).StatusCode);
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
        using (var rescan = await Write(client, HttpMethod.Post, "/api/v1/github/scans", new { }))
        {
            var ids = (await rescan.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("analysisJobIds")
                .EnumerateArray().Select(item => item.GetGuid()).ToArray();
            Assert.Empty(ids.Intersect(jobs));
            using var repeated = await Write(client, HttpMethod.Post, "/api/v1/github/scans", new { });
            Assert.Equal(ids, (await repeated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("analysisJobIds")
                .EnumerateArray().Select(item => item.GetGuid()).ToArray());
            foreach (var id in ids)
            {
                using var scope = fixture.Factory.Services.CreateScope();
                var queue = scope.ServiceProvider.GetRequiredService<IAnalysisQueue>();
                var lease = await queue.ClaimAsync(default);
                Assert.Equal(id, lease!.Id);
                var handler = scope.ServiceProvider.GetServices<IAnalysisHandler>().Single(item => item.Kind == lease.Kind);
                Assert.True(await queue.CompleteAsync(lease, await handler.ProcessAsync(lease, default), default));
            }
            var rescanned = (await client.GetFromJsonAsync<GitHubEvidenceView[]>("/api/v1/github/evidence"))!;
            Assert.Equal(evidence!.Select(item => item.Id).Order(), rescanned.Select(item => item.Id).Order());
        }
        using var other = await User(); Assert.Empty((await other.GetFromJsonAsync<GitHubEvidenceView[]>("/api/v1/github/evidence"))!);
        Assert.Equal(HttpStatusCode.NoContent, (await Write(client, HttpMethod.Post, "/api/v1/github/disconnect", new { })).StatusCode);
        evidence = await client.GetFromJsonAsync<GitHubEvidenceView[]>("/api/v1/github/evidence"); Assert.All(evidence!, item => Assert.Equal("Stale", item.Lifecycle));
        var storedEvidence = evidence!; var repositoryIds = storedEvidence.Select(item => item.RepositoryId).Distinct().ToArray(); var evidenceIds = storedEvidence.Select(item => item.Id).ToArray();
        Assert.Equal(HttpStatusCode.NoContent, (await Write(client, HttpMethod.Delete, "/api/v1/account", new { confirm = true, password = Password })).StatusCode);
        await using var verification = fixture.Factory.Services.CreateAsyncScope(); var database = verification.ServiceProvider.GetRequiredService<ProofPathDbContext>();
        Assert.False(await database.Repositories.AnyAsync(item => repositoryIds.Contains(item.Id)));
        Assert.False(await database.EvidenceItems.AnyAsync(item => evidenceIds.Contains(item.Id)));
    }

    [Fact]
    public async Task TwoCandidatesConnectOnlyTheirOwnInstallationsAndCannotUseEachOthersStateOrRepositories()
    {
        using var alice = await User();
        using var bob = await User();
        using var anonymous = fixture.Client();
        var bobIdentity = await bob.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        var bobId = bobIdentity.GetProperty("id").GetString();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ProofPathDbContext>();
            var profile = await database.CandidateProfiles.SingleAsync(item => item.UserId == bobId);
            var legacy = new ConnectedAccount { Id = Guid.NewGuid(), CandidateProfileId = profile.Id, ExternalAccountId = 43, ExternalLogin = "synthetic-a" };
            database.ConnectedAccounts.Add(legacy);
            database.GitHubInstallations.Add(new GitHubInstallation { Id = Guid.NewGuid(), ConnectedAccountId = legacy.Id,
                InstallationId = 100, TargetAccountId = 43, TargetLogin = "synthetic-a", TargetType = "User" });
            await database.SaveChangesAsync();
        }
        var legacyStatus = await bob.GetFromJsonAsync<GitHubConnectionView>("/api/v1/github/status");
        Assert.False(legacyStatus!.Connected);
        Assert.Null(legacyStatus.Login);
        Assert.Equal(HttpStatusCode.Conflict, (await bob.GetAsync("/api/v1/github/repositories")).StatusCode);
        var state = await Start(alice);
        var callback = $"/api/v1/github/callback?state={state}&code=alice";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(callback)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await bob.GetAsync(callback)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await alice.GetAsync(callback)).StatusCode);

        var bobState = await Start(bob);
        var install = await bob.GetAsync($"/api/v1/github/callback?state={bobState}&code=uninstalled");
        Assert.Equal(HttpStatusCode.Redirect, install.StatusCode);
        var setupState = install.Headers.Location!.Query.Split("state=")[1];
        var authorization = await bob.GetAsync($"/api/v1/github/callback?state={setupState}&installation_id=100&setup_action=install");
        Assert.Equal(HttpStatusCode.Redirect, authorization.StatusCode);
        var claimedState = authorization.Headers.Location!.Query.Split("state=")[1];
        var rejected = await bob.GetAsync($"/api/v1/github/callback?state={claimedState}&code=bob");
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.Equal("GITHUB_INSTALLATION_NOT_AUTHORIZED", (await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.False((await bob.GetFromJsonAsync<GitHubConnectionView>("/api/v1/github/status"))!.Connected);

        bobState = await Start(bob);
        Assert.Equal(HttpStatusCode.Redirect, (await bob.GetAsync($"/api/v1/github/callback?state={bobState}&code=bob")).StatusCode);
        var aliceRepos = (await alice.GetFromJsonAsync<RepositoryView[]>("/api/v1/github/repositories"))!;
        var bobRepos = (await bob.GetFromJsonAsync<RepositoryView[]>("/api/v1/github/repositories"))!;
        Assert.Equal(6, aliceRepos.Length);
        Assert.Equal(6, bobRepos.Length);
        Assert.Empty(aliceRepos.Select(repo => repo.GitHubId).Intersect(bobRepos.Select(repo => repo.GitHubId)));
        Assert.Equal(HttpStatusCode.Forbidden, (await Write(bob, HttpMethod.Put, "/api/v1/github/repositories/selection",
            new { repositoryIds = new[] { aliceRepos[0].GitHubId } })).StatusCode);
    }
}
