using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Api.Tests;

[Collection("PostgreSQL API")]
public sealed class DatabaseIsolationTests(ApiFixture fixture)
{
    [Fact]
    public async Task RuntimeLoginsIsolateCandidatesAcrossPooledConnections()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ProofPathDbContext>();
        await using var owner = new NpgsqlConnection(database.Database.GetConnectionString());
        await owner.OpenAsync();
        var policies = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "postgres-runtime-security.sql"));
        await using (var install = new NpgsqlCommand(
            string.Join('\n', policies.Split('\n').Where(line => !line.StartsWith('\\'))), owner))
            await install.ExecuteNonQueryAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var apiRole = "pp_api_" + suffix;
        var workerRole = "pp_worker_" + suffix;
        // These logins exist only in the disposable test database.
        const string password = "fixture-only-role-password";
        await using (var provision = new NpgsqlCommand($"""
            CREATE ROLE {apiRole} LOGIN PASSWORD '{password}';
            GRANT proofpath_api_runtime TO {apiRole};
            CREATE ROLE {workerRole} LOGIN PASSWORD '{password}';
            GRANT proofpath_worker_runtime TO {workerRole};
            """, owner))
            await provision.ExecuteNonQueryAsync();

        string ConnectionFor(string role) => new NpgsqlConnectionStringBuilder(owner.ConnectionString)
        {
            Username = role, Password = password, MaxPoolSize = 1
        }.ConnectionString;

        var apiConnection = ConnectionFor(apiRole);
        using var runtime = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = apiConnection
            })));
        using var a = runtime.CreateClient();
        using var b = runtime.CreateClient();
        var aId = await RegisterAndSignInAsync(a, "Ada");
        var bId = await RegisterAndSignInAsync(b, "Grace");
        foreach (var (client, name) in new[] { (a, "Ada"), (b, "Grace"), (a, "Ada") })
        {
            var profile = await client.GetFromJsonAsync<JsonElement>("/api/v1/profile");
            Assert.Equal(name, profile.GetProperty("firstName").GetString());
        }

        Assert.Equal(HttpStatusCode.BadRequest,
            await WriteAsync(a, HttpMethod.Put, "/api/v1/profile", new { firstName = "Ada", userId = bId }));

        await using var api = new NpgsqlConnection(apiConnection);
        await api.OpenAsync();
        await using (var profiles = new NpgsqlCommand("""select count(*) from "CandidateProfiles" """, api))
            Assert.Equal(0L, await profiles.ExecuteScalarAsync());
        await using (var context = new NpgsqlCommand("select set_config('proofpath.user_id', @userId, false)", api))
        {
            context.Parameters.AddWithValue("userId", aId);
            await context.ExecuteNonQueryAsync();
        }
        await using (var profiles = new NpgsqlCommand("""select "UserId" from "CandidateProfiles" """, api))
            Assert.Equal(aId, await profiles.ExecuteScalarAsync());
        await using (var write = new NpgsqlCommand("""update "CandidateProfiles" set "FirstName" = 'Blocked' where "UserId" = @id""", api))
        {
            write.Parameters.AddWithValue("id", bId);
            Assert.Equal(0, await write.ExecuteNonQueryAsync());
        }
        await using (var transfer = new NpgsqlCommand("""update "CandidateProfiles" set "UserId" = @id""", api))
        {
            transfer.Parameters.AddWithValue("id", bId);
            var denied = await Assert.ThrowsAsync<PostgresException>(() => transfer.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }

        await using var worker = new NpgsqlConnection(ConnectionFor(workerRole));
        await worker.OpenAsync();
        await using (var queue = new NpgsqlCommand("""select count(*) from "AnalysisJobs" """, worker))
            Assert.IsType<long>(await queue.ExecuteScalarAsync());
        await using (var users = new NpgsqlCommand("""select count(*) from "AspNetUsers" """, worker))
        {
            var denied = await Assert.ThrowsAsync<PostgresException>(() => users.ExecuteScalarAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }
    }

    private static async Task<string> RegisterAndSignInAsync(HttpClient client, string firstName)
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        const string password = "Fixture-only.P4ssword!";
        Assert.Equal(HttpStatusCode.Created,
            await WriteAsync(client, HttpMethod.Post, "/api/v1/auth/register", new { email, password, firstName }));
        Assert.Equal(HttpStatusCode.OK,
            await WriteAsync(client, HttpMethod.Post, "/api/v1/auth/login", new { email, password }));
        var user = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        return user.GetProperty("id").GetString()!;
    }

    private static async Task<HttpStatusCode> WriteAsync(HttpClient client, HttpMethod method, string path, object body)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }
}
