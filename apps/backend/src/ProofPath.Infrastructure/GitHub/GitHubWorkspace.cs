using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProofPath.Application.Analysis;
using ProofPath.Application.GitHub;
using ProofPath.Domain.Entities;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Infrastructure.GitHub;

public sealed class GitHubWorkspace(ProofPathDbContext database, IGitHubProvider provider, IAnalysisQueue queue,
    IDataProtectionProvider protection) : IGitHubWorkspace
{
    private readonly IDataProtector protector = protection.CreateProtector("ProofPath.GitHub.Connection.v1");
    private sealed record State(string UserId, Guid ProfileId, string Nonce, string Verifier, DateTime ExpiresAt);
    private IQueryable<ConnectedAccount> Accounts(string userId) => database.ConnectedAccounts.Where(item =>
        database.CandidateProfiles.Any(profile => profile.Id == item.CandidateProfileId && profile.UserId == userId) && item.Provider == "GitHub");

    public async Task<Uri> BeginConnectionAsync(string userId, CancellationToken ct)
    {
        var profileId = await database.CandidateProfiles.Where(item => item.UserId == userId).Select(item => (Guid?)item.Id).SingleOrDefaultAsync(ct)
            ?? throw new GitHubProblem("PROFILE_REQUIRED", 409);
        var nonce = Random(32); var now = DateTime.UtcNow; var state = new State(userId, profileId, nonce, Random(48), now.AddMinutes(10));
        await database.GitHubConnectionAttempts.Where(item => item.CandidateProfileId == profileId || item.ExpiresAt <= now).ExecuteDeleteAsync(ct);
        database.GitHubConnectionAttempts.Add(new()
        {
            Id = Guid.NewGuid(), CandidateProfileId = profileId, NonceHash = Hash(nonce),
            ProtectedPayload = protector.Protect(JsonSerializer.Serialize(state)), CreatedAt = now, ExpiresAt = state.ExpiresAt
        });
        await database.SaveChangesAsync(ct);
        return provider.BuildInstallationUri(nonce);
    }

    public async Task CompleteConnectionAsync(string protectedState, long installationId, string code, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(protectedState) || installationId <= 0 || string.IsNullOrWhiteSpace(code)) throw new GitHubProblem("GITHUB_STATE_INVALID");
        var attempt = await database.GitHubConnectionAttempts.SingleOrDefaultAsync(item =>
            item.NonceHash == Hash(protectedState) && item.ExpiresAt > now, ct);
        if (attempt is null) throw new GitHubProblem("GITHUB_STATE_REPLAYED");
        State state; try { state = JsonSerializer.Deserialize<State>(protector.Unprotect(attempt.ProtectedPayload))!; }
        catch (Exception exception) when (exception is CryptographicException or JsonException) { throw new GitHubProblem("GITHUB_STATE_INVALID"); }
        if (state.ProfileId != attempt.CandidateProfileId || state.Nonce != protectedState || state.ExpiresAt <= now)
            throw new GitHubProblem("GITHUB_STATE_INVALID");
        var consumed = await database.GitHubConnectionAttempts.Where(item => item.Id == attempt.Id).ExecuteDeleteAsync(ct);
        if (consumed != 1) throw new GitHubProblem("GITHUB_STATE_REPLAYED");
        var verified = await provider.VerifyInstallationAsync(installationId, code, state.Verifier, ct);
        if (verified.InstallationId != installationId || !verified.Permissions.TryGetValue("contents", out var contents) || contents != "read")
            throw new GitHubProblem("GITHUB_PERMISSIONS_INVALID", 403);
        var account = await Accounts(state.UserId).SingleOrDefaultAsync(ct);
        if (account is null)
        {
            account = new() { Id = Guid.NewGuid(), CandidateProfileId = state.ProfileId, ConnectedAt = now };
            database.ConnectedAccounts.Add(account);
        }
        account.ExternalAccountId = verified.AuthorizingUserId; account.ExternalLogin = verified.AuthorizingLogin;
        account.Status = ConnectedAccountStatus.Active; account.DisconnectedAt = null; account.UpdatedAt = now;
        var installation = await database.GitHubInstallations.SingleOrDefaultAsync(item => item.ConnectedAccountId == account.Id, ct);
        if (installation is null) { installation = new() { Id = Guid.NewGuid(), ConnectedAccountId = account.Id, CreatedAt = now }; database.GitHubInstallations.Add(installation); }
        installation.InstallationId = verified.InstallationId; installation.TargetAccountId = verified.TargetAccountId;
        installation.TargetLogin = verified.TargetLogin; installation.TargetType = verified.TargetType;
        installation.RepositorySelection = verified.RepositorySelection; installation.PermissionsJson = JsonSerializer.Serialize(verified.Permissions);
        installation.UpdatedAt = now; installation.SuspendedAt = null; await database.SaveChangesAsync(ct);
        await Synchronize(state.UserId, account, installation, ct);
    }

    public async Task<GitHubConnectionView> StatusAsync(string userId, CancellationToken ct)
    {
        var result = await (from account in Accounts(userId)
            join installation in database.GitHubInstallations on account.Id equals installation.ConnectedAccountId
            select new { account, installation }).SingleOrDefaultAsync(ct);
        return result is null ? new(false, null, null, null, null) : new(result.account.Status == ConnectedAccountStatus.Active,
            result.account.ExternalLogin, result.installation.TargetLogin, result.account.Status.ToString(), result.account.ConnectedAt);
    }

    public async Task DisconnectAsync(string userId, CancellationToken ct)
    {
        var result = await (from account in Accounts(userId)
            join installation in database.GitHubInstallations on account.Id equals installation.ConnectedAccountId
            select new { account, installation }).SingleOrDefaultAsync(ct) ?? throw new GitHubProblem("GITHUB_NOT_CONNECTED", 404);
        if (result.account.Status == ConnectedAccountStatus.Active) await provider.RevokeInstallationAsync(result.installation.InstallationId, ct);
        result.account.Status = ConnectedAccountStatus.Disconnected; result.account.DisconnectedAt = DateTime.UtcNow; result.account.UpdatedAt = DateTime.UtcNow;
        await database.Repositories.Where(item => item.ConnectedAccountId == result.account.Id).ExecuteUpdateAsync(set => set
            .SetProperty(item => item.IncludedForAnalysis, false).SetProperty(item => item.ScanStatus, RepositoryScanStatus.AccessLost), ct);
        await database.EvidenceItems.Where(item => item.RepositoryAnalysisId != null && database.RepositoryAnalyses.Any(analysis =>
            analysis.Id == item.RepositoryAnalysisId && database.Repositories.Any(repo => repo.Id == analysis.RepositoryId && repo.ConnectedAccountId == result.account.Id)))
            .ExecuteUpdateAsync(set => set.SetProperty(item => item.Lifecycle, "Stale"), ct);
        await database.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RepositoryView>> RepositoriesAsync(string userId, bool synchronize, CancellationToken ct)
    {
        var pair = await Active(userId, ct); if (synchronize) await Synchronize(userId, pair.Account, pair.Installation, ct);
        return await Views(pair.Account.CandidateProfileId).ToArrayAsync(ct);
    }

    public async Task SaveSelectionAsync(string userId, IReadOnlyCollection<long> repositoryIds, CancellationToken ct)
    {
        var ids = repositoryIds.Distinct().ToArray(); if (ids.Length > 5) throw new GitHubProblem("REPOSITORY_LIMIT_EXCEEDED");
        var pair = await Active(userId, ct); await Synchronize(userId, pair.Account, pair.Installation, ct);
        var available = await database.Repositories.Where(item => item.ConnectedAccountId == pair.Account.Id).ToListAsync(ct);
        if (ids.Except(available.Select(item => item.GitHubId)).Any()) throw new GitHubProblem("REPOSITORY_NOT_AUTHORIZED", 403);
        foreach (var repository in available)
        {
            var included = ids.Contains(repository.GitHubId);
            if (repository.IncludedForAnalysis && !included)
                await database.EvidenceItems.Where(item => item.RepositoryAnalysisId != null && database.RepositoryAnalyses.Any(analysis => analysis.Id == item.RepositoryAnalysisId && analysis.RepositoryId == repository.Id))
                    .ExecuteUpdateAsync(set => set.SetProperty(item => item.Lifecycle, "Inactive"), ct);
            repository.IncludedForAnalysis = included;
        }
        await database.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> ScanAsync(string userId, CancellationToken ct)
    {
        var pair = await Active(userId, ct); var repositories = await database.Repositories.Where(item => item.ConnectedAccountId == pair.Account.Id && item.IncludedForAnalysis).ToArrayAsync(ct);
        if (repositories.Length == 0) throw new GitHubProblem("REPOSITORY_SELECTION_REQUIRED", 409);
        var jobs = new List<Guid>();
        foreach (var repository in repositories)
        {
            var id = await queue.EnqueueAsync(userId, AnalysisKind.Repository, repository.Id, "github-onboarding-v1", ct);
            repository.ScanStatus = RepositoryScanStatus.Pending; jobs.Add(id);
        }
        await database.SaveChangesAsync(ct); return jobs;
    }

    public async Task<IReadOnlyList<GitHubEvidenceView>> EvidenceAsync(string userId, CancellationToken ct) =>
        await (from evidence in database.EvidenceItems.AsNoTracking()
         join analysis in database.RepositoryAnalyses on evidence.RepositoryAnalysisId equals analysis.Id
         join repository in database.Repositories on analysis.RepositoryId equals repository.Id
         where database.CandidateProfiles.Any(profile => profile.Id == evidence.CandidateProfileId && profile.UserId == userId)
         orderby evidence.ObservedAt descending
         select new GitHubEvidenceView(evidence.Id, repository.Id, repository.FullName, evidence.SkillId, evidence.OriginalTerm,
             evidence.EvidenceType, evidence.Context, evidence.Strength, evidence.ExtractionConfidence, evidence.Lifecycle,
             evidence.RevisionSha!, evidence.SourcePath, evidence.Detector!, evidence.ObservedAt)).ToArrayAsync(ct);

    private async Task<(ConnectedAccount Account, GitHubInstallation Installation)> Active(string userId, CancellationToken ct)
    {
        var result = await (from account in Accounts(userId) where account.Status == ConnectedAccountStatus.Active
            join installation in database.GitHubInstallations on account.Id equals installation.ConnectedAccountId select new { account, installation }).SingleOrDefaultAsync(ct);
        return result is null ? throw new GitHubProblem("GITHUB_NOT_CONNECTED", 409) : (result.account, result.installation);
    }
    private async Task Synchronize(string userId, ConnectedAccount account, GitHubInstallation installation, CancellationToken ct)
    {
        var remote = await provider.ListRepositoriesAsync(installation.InstallationId, ct); var current = await database.Repositories.Where(item => item.ConnectedAccountId == account.Id).ToDictionaryAsync(item => item.GitHubId, ct);
        foreach (var item in remote)
        {
            if (!current.TryGetValue(item.Id, out var repository))
            {
                repository = new() { Id = Guid.NewGuid(), CandidateProfileId = account.CandidateProfileId, ConnectedAccountId = account.Id, GitHubId = item.Id, IncludedForAnalysis = false, ScanStatus = RepositoryScanStatus.NeverScanned };
                database.Repositories.Add(repository);
            }
            repository.Owner = item.Owner; repository.Name = item.Name; repository.FullName = item.FullName; repository.Private = item.Private;
            repository.DefaultBranch = item.DefaultBranch; repository.HtmlUrl = item.HtmlUrl; repository.LastSyncedAt = DateTime.UtcNow;
        }
        await database.SaveChangesAsync(ct);
    }
    private IQueryable<RepositoryView> Views(Guid profileId) => database.Repositories.AsNoTracking().Where(item => item.CandidateProfileId == profileId)
        .OrderBy(item => item.FullName).Select(item => new RepositoryView(item.Id, item.GitHubId, item.FullName, item.Private,
            item.DefaultBranch, item.HtmlUrl, item.IncludedForAnalysis, item.ScanStatus.ToString(), database.AnalysisJobs.Where(job => job.ResourceId == item.Id && job.Kind == AnalysisKind.Repository).OrderByDescending(job => job.CreatedAt).Select(job => (Guid?)job.Id).FirstOrDefault(), item.LastRevisionSha, item.LastScanAt, item.CoverageJson));
    private static string Random(int bytes) => Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

public sealed class RepositoryAnalysisHandler(ProofPathDbContext database, IGitHubProvider provider, DeterministicRepositoryAnalyzer analyzer) : IAnalysisHandler
{
    public AnalysisKind Kind => AnalysisKind.Repository;
    public async Task<AnalysisOutput> ProcessAsync(AnalysisLease lease, CancellationToken ct)
    {
        var input = await (from repository in database.Repositories.AsNoTracking()
            join account in database.ConnectedAccounts.AsNoTracking() on repository.ConnectedAccountId equals account.Id
            join installation in database.GitHubInstallations.AsNoTracking() on account.Id equals installation.ConnectedAccountId
            where repository.Id == lease.ResourceId && repository.CandidateProfileId == lease.CandidateProfileId && repository.IncludedForAnalysis && account.Status == ConnectedAccountStatus.Active
            select new { repository, installation.InstallationId }).SingleOrDefaultAsync(ct) ?? throw new AnalysisFailure("GITHUB_ACCESS_LOST", false);
        await database.Repositories.Where(item => item.Id == lease.ResourceId).ExecuteUpdateAsync(set => set.SetProperty(item => item.ScanStatus, RepositoryScanStatus.Processing), ct);
        GitHubRepositorySnapshot snapshot;
        try { snapshot = await provider.ReadSnapshotAsync(input.InstallationId, input.repository.GitHubId, ct); }
        catch (AnalysisFailure failure)
        {
            var status = failure.Code == "GITHUB_ACCESS_LOST" ? RepositoryScanStatus.AccessLost : RepositoryScanStatus.FailedRetryable;
            await database.Repositories.Where(item => item.Id == lease.ResourceId).ExecuteUpdateAsync(set => set.SetProperty(item => item.ScanStatus, status), ct); throw;
        }
        var identity = $"{input.repository.Id}:{snapshot.RevisionSha}:{DeterministicRepositoryAnalyzer.ExtractionVersion}:{DeterministicRepositoryAnalyzer.PolicyVersion}";
        var prior = await database.RepositoryAnalyses.AsNoTracking().Where(item => item.ScanIdentity == identity).Select(item => item.ResultJson).SingleOrDefaultAsync(ct);
        var json = prior ?? JsonSerializer.Serialize(analyzer.Analyze(snapshot));
        return new(json, JsonSerializer.Deserialize<RepositoryAnalysisResult>(json)!.Status == "CompletedLimited");
    }
}

public sealed class RepositoryAnalysisCompletion(ProofPathDbContext database) : IAnalysisCompletion
{
    public async Task SaveAsync(AnalysisLease lease, AnalysisOutput output, CancellationToken ct)
    {
        if (lease.Kind != AnalysisKind.Repository) return; var result = JsonSerializer.Deserialize<RepositoryAnalysisResult>(output.ResultJson) ?? throw new AnalysisFailure("GITHUB_INVALID_RESULT", false);
        var repository = await database.Repositories.SingleOrDefaultAsync(item => item.Id == lease.ResourceId && item.CandidateProfileId == lease.CandidateProfileId, ct); if (repository is null) return;
        var identity = $"{repository.Id}:{result.RevisionSha}:{result.ExtractionVersion}:{result.AnalysisPolicyVersion}";
        if (await database.RepositoryAnalyses.AnyAsync(item => item.ScanIdentity == identity, ct)) return;
        var analysis = new RepositoryAnalysis { Id = Guid.NewGuid(), RepositoryId = repository.Id, AnalysisJobId = lease.Id, RevisionSha = result.RevisionSha,
            ExtractionVersion = result.ExtractionVersion, AnalysisPolicyVersion = result.AnalysisPolicyVersion, ScanIdentity = identity,
            Status = Enum.Parse<RepositoryScanStatus>(result.Status), CoverageJson = JsonSerializer.Serialize(result.Coverage), WarningsJson = JsonSerializer.Serialize(result.Warnings), ResultJson = output.ResultJson, CreatedAt = DateTime.UtcNow };
        database.RepositoryAnalyses.Add(analysis);
        var project = await database.Projects.SingleOrDefaultAsync(item => item.RepositoryId == repository.Id, ct);
        if (project is null) { project = new Project { Id = Guid.NewGuid(), CandidateProfileId = lease.CandidateProfileId, RepositoryId = repository.Id, Name = repository.Name, SourceUrl = repository.HtmlUrl, Detail = $"GitHub repository {repository.FullName}", SourceBlockId = result.RevisionSha, Quote = repository.FullName }; database.Projects.Add(project); }
        var catalogSkillIds = await database.Skills.AsNoTracking().Select(item => item.Id).ToHashSetAsync(ct);
        foreach (var candidate in result.Evidence)
            database.EvidenceItems.Add(new EvidenceItem { Id = Guid.NewGuid(), CandidateProfileId = lease.CandidateProfileId, RepositoryAnalysisId = analysis.Id,
                ProjectId = project.Id, SkillId = catalogSkillIds.Contains(candidate.SkillKey) ? candidate.SkillKey : null, OriginalTerm = candidate.OriginalTerm, Context = candidate.Detail,
                Strength = candidate.Strength, ExtractionConfidence = candidate.ExtractionConfidence, EvidenceType = candidate.EvidenceType,
                Lifecycle = "Active", SourceBlockId = candidate.Provenance.Path, Quote = candidate.Detail, ObservedAt = candidate.Provenance.ObservedAt,
                RevisionSha = candidate.Provenance.RevisionSha, SourcePath = candidate.Provenance.Path, StartLine = candidate.Provenance.StartLine,
                EndLine = candidate.Provenance.EndLine, Detector = candidate.Provenance.Detector, DetectorVersion = candidate.Provenance.DetectorVersion });
        repository.LastRevisionSha = result.RevisionSha; repository.LastScanAt = DateTime.UtcNow; repository.ScanStatus = analysis.Status; repository.CoverageJson = analysis.CoverageJson;
        await database.SaveChangesAsync(ct);
    }
}

public static class GitHubRegistration
{
    public static IServiceCollection AddGitHubModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDataProtection();
        services.AddSingleton<IGitHubProvider>(_ => new GitHubAppProvider(new HttpClient { Timeout = TimeSpan.FromSeconds(60) }, configuration));
        services.AddSingleton<DeterministicRepositoryAnalyzer>(); services.AddScoped<IGitHubWorkspace, GitHubWorkspace>();
        services.AddScoped<IAnalysisHandler, RepositoryAnalysisHandler>(); services.AddScoped<IAnalysisCompletion, RepositoryAnalysisCompletion>(); return services;
    }
}
