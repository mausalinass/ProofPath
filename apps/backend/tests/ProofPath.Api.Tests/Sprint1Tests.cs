using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProofPath.Application.GitHub;
using ProofPath.Application.Candidates;
using ProofPath.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace ProofPath.Api.Tests;

[CollectionDefinition("PostgreSQL API")]
public sealed class ApiCollection : ICollectionFixture<ApiFixture> { }

public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("proofpath_tests").WithUsername("proofpath_tests").WithPassword("isolated-fixture-only").Build();
    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "proofpath-api-tests", Guid.NewGuid().ToString("N"));
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        await container.StartAsync();
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = container.GetConnectionString(),
                    ["Storage:PrivateRoot"] = StorageRoot,
                    ["OpenAI:ApiKey"] = "",
                    ["Security:AuthRequestsPerMinute"] = "10000",
                    ["Logging:LogLevel:Default"] = "Warning"
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGitHubProvider>();
                services.AddSingleton<IGitHubProvider, FakeGitHubProvider>();
            });
        });
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ProofPathDbContext>().Database.MigrateAsync();
    }
    public HttpClient Client() => Factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });
    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync(); await container.DisposeAsync();
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "proofpath-api-tests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(StorageRoot).StartsWith(parent, StringComparison.OrdinalIgnoreCase) || !Guid.TryParseExact(Path.GetFileName(StorageRoot), "N", out _)) throw new InvalidOperationException("Unexpected test storage path.");
        if (Directory.Exists(StorageRoot)) Directory.Delete(StorageRoot, recursive: true);
    }
}

[Collection("PostgreSQL API")]
public sealed class Sprint1Tests(ApiFixture fixture)
{
    private const string Password = "Test-only.P4ssword!";
    private static string Email() => $"{Guid.NewGuid():N}@example.test";
    private static async Task<HttpResponseMessage> Write(HttpClient client, HttpMethod method, string path, object body)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }
    private async Task<(HttpClient Client, string Email)> User(string prefix = "/api/v1/auth")
    {
        var client = fixture.Client(); var email = Email();
        Assert.Equal(HttpStatusCode.Created, (await Write(client, HttpMethod.Post, prefix + "/register",
            new { email, password = Password, firstName = "Ada", lastName = "Lovelace" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Write(client, HttpMethod.Post, prefix + "/login", new { email, password = Password })).StatusCode);
        return (client, email);
    }

    [Fact]
    public async Task AnonymousCannotReadWorkspace()
    {
        using var client = fixture.Client();
        foreach (var path in new[] { "/api/v1/profile", "/api/v1/home", "/api/v1/auth/me", "/api/auth/me" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task RegistrationRequiresNameAndRejectsDuplicateEmail()
    {
        using var client = fixture.Client(); var email = Email();
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, HttpMethod.Post, "/api/v1/auth/register", new { email, password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Write(client, HttpMethod.Post, "/api/v1/auth/register", new { email, password = Password, firstName = "Ada" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Write(client, HttpMethod.Post, "/api/v1/auth/register", new { email, password = Password, firstName = "Other" })).StatusCode);
    }

    [Theory]
    [InlineData("/api/auth")]
    [InlineData("/api/v1/auth")]
    public async Task AuthAliasesPreserveLoginMeLogout(string prefix)
    {
        var (client, email) = await User(prefix); using (client)
        {
            var me = await client.GetFromJsonAsync<JsonElement>(prefix + "/me");
            Assert.Equal(email, me.GetProperty("email").GetString());
            Assert.Equal(HttpStatusCode.NoContent, (await Write(client, HttpMethod.Post, prefix + "/logout", new { })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(prefix + "/me")).StatusCode);
        }
    }

    [Fact]
    public async Task InvalidCredentialsDoNotAuthenticate()
    {
        using var client = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Write(client, HttpMethod.Post, "/api/v1/auth/login", new { email = Email(), password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task ProfilePersistsWithStableIdCreatedAtAndUtcTimestamps()
    {
        var (client, email) = await User(); using (client)
        {
            var initial = await client.GetFromJsonAsync<ProfileView>("/api/v1/profile"); Assert.NotNull(initial);
            Assert.Equal("Ada", initial.FirstName);
            Assert.Equal(HttpStatusCode.OK, (await Write(client, HttpMethod.Put, "/api/v1/profile",
                new { firstName = " Ada ", lastName = "Lovelace", headline = "Engineer", location = "Remote", workAuthorization = "Context", educationSummary = "CS" })).StatusCode);
            await Write(client, HttpMethod.Post, "/api/v1/auth/logout", new { });
            await Write(client, HttpMethod.Post, "/api/v1/auth/login", new { email, password = Password });
            var profile = await client.GetFromJsonAsync<ProfileView>("/api/v1/profile"); Assert.NotNull(profile);
            Assert.Equal(initial.Id, profile.Id); Assert.Equal(initial.CreatedAt, profile.CreatedAt);
            Assert.Equal("Ada", profile.FirstName); Assert.Equal("Engineer", profile.Headline);
            Assert.Equal(DateTimeKind.Utc, profile.UpdatedAt.Kind); Assert.True(profile.UpdatedAt >= initial.UpdatedAt);
        }
    }

    [Fact]
    public async Task TwoUsersCannotOverwriteOrReadEachOthersProfiles()
    {
        var (a, _) = await User(); var (b, _) = await User(); using (a) using (b)
        {
            var before = await b.GetFromJsonAsync<ProfileView>("/api/v1/profile"); Assert.NotNull(before);
            var bMe = await b.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
            Assert.Equal(HttpStatusCode.BadRequest, (await Write(a, HttpMethod.Put, "/api/v1/profile", new { firstName = "Intruder", userId = bMe.GetProperty("id").GetString() })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Write(a, HttpMethod.Put, "/api/v1/profile", new { firstName = "Intruder", id = before.Id })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync("/api/v1/profile/" + before.Id)).StatusCode);
            await Write(a, HttpMethod.Put, "/api/v1/profile", new { firstName = "User A" });
            var after = await b.GetFromJsonAsync<ProfileView>("/api/v1/profile");
            Assert.Equal(before, after);
        }
    }

    [Fact]
    public async Task MutationsRejectMissingAndInvalidCsrf()
    {
        var (client, _) = await User(); using (client)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/v1/profile", new { firstName = "Changed" })).StatusCode);
            using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/profile") { Content = JsonContent.Create(new { firstName = "Changed" }) };
            request.Headers.Add("X-CSRF-TOKEN", "invalid");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode);
            Assert.Equal("Ada", (await client.GetFromJsonAsync<ProfileView>("/api/v1/profile"))!.FirstName);
        }
    }

    [Fact]
    public async Task SignupAlsoRejectsMissingCsrf()
    {
        using var client = fixture.Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/register", new { email = Email(), password = Password, firstName = "Ada" })).StatusCode);
    }

    [Theory]
    [InlineData("http://localhost:5173", true)]
    [InlineData("https://untrusted.example", false)]
    public async Task CorsOnlyPermitsConfiguredOrigins(string origin, bool allowed)
    {
        using var client = fixture.Client(); using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/profile");
        request.Headers.Add("Origin", origin); request.Headers.Add("Access-Control-Request-Method", "PUT");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-csrf-token");
        var response = await client.SendAsync(request);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        if (allowed) Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    [Fact]
    public async Task DeletionRequiresPasswordRemovesProfileAndInvalidatesOtherSessions()
    {
        var (a, email) = await User(); using (a) using (var other = fixture.Client())
        {
            await Write(other, HttpMethod.Post, "/api/v1/auth/login", new { email, password = Password });
            var me = await a.GetFromJsonAsync<JsonElement>("/api/v1/auth/me"); var id = me.GetProperty("id").GetString();
            Assert.Equal(HttpStatusCode.BadRequest, (await Write(a, HttpMethod.Delete, "/api/v1/account", new { confirm = true, password = "wrong" })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await Write(a, HttpMethod.Delete, "/api/v1/account", new { confirm = true, password = Password })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/v1/auth/me")).StatusCode);
            using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ProofPathDbContext>();
            Assert.False(await db.Users.AnyAsync(u => u.Id == id)); Assert.False(await db.CandidateProfiles.AnyAsync(p => p.UserId == id));
        }
    }

    [Fact]
    public async Task ConcurrentSavesKeepExactlyOneProfile()
    {
        var (client, _) = await User(); using (client)
        {
            var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me"); var id = me.GetProperty("id").GetString()!;
            var responses = await Task.WhenAll(Write(client, HttpMethod.Put, "/api/v1/profile", new { firstName = "One" }), Write(client, HttpMethod.Put, "/api/v1/profile", new { firstName = "Two" }));
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            using var scope = fixture.Factory.Services.CreateScope();
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ProofPathDbContext>().CandidateProfiles.CountAsync(p => p.UserId == id));
        }
    }
}
