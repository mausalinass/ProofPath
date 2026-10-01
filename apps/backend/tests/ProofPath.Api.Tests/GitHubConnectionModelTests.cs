using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProofPath.Domain.Entities;
using ProofPath.Infrastructure.Identity;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Api.Tests;

[Collection("PostgreSQL API")]
public sealed class GitHubConnectionModelTests(ApiFixture fixture)
{
    [Fact]
    public async Task ConnectionMetadataCascadesWithCandidateAndNeverStoresTokens()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ProofPathDbContext>();
        var userId = Guid.NewGuid().ToString();
        var profileId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        database.Users.Add(new ApplicationUser { Id = userId, UserName = userId, NormalizedUserName = userId.ToUpperInvariant() });
        database.CandidateProfiles.Add(new CandidateProfile { Id = profileId, UserId = userId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        database.ConnectedAccounts.Add(new ConnectedAccount
        {
            Id = accountId,
            CandidateProfileId = profileId,
            ExternalAccountId = Random.Shared.NextInt64(1, long.MaxValue),
            ExternalLogin = "octocat",
            ConnectedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        database.GitHubInstallations.Add(new GitHubInstallation
        {
            Id = Guid.NewGuid(),
            ConnectedAccountId = accountId,
            InstallationId = Random.Shared.NextInt64(1, long.MaxValue),
            TargetAccountId = Random.Shared.NextInt64(1, long.MaxValue),
            TargetLogin = "octo-org",
            TargetType = "Organization",
            RepositorySelection = "selected",
            PermissionsJson = "{\"metadata\":\"read\",\"contents\":\"read\"}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await database.SaveChangesAsync();

        Assert.DoesNotContain(typeof(ConnectedAccount).GetProperties(), property => property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(GitHubInstallation).GetProperties(), property => property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));

        await database.Users.Where(user => user.Id == userId).ExecuteDeleteAsync();
        Assert.False(await database.ConnectedAccounts.AnyAsync(item => item.Id == accountId));
        Assert.False(await database.GitHubInstallations.AnyAsync(item => item.ConnectedAccountId == accountId));
    }
}
