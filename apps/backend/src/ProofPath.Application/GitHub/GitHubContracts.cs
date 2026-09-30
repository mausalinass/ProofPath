namespace ProofPath.Application.GitHub;

public sealed class GitHubProblem(string code, int status = 400) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public sealed record VerifiedGitHubInstallation(
    long AuthorizingUserId,
    string AuthorizingLogin,
    long InstallationId,
    long TargetAccountId,
    string TargetLogin,
    string TargetType,
    string RepositorySelection,
    IReadOnlyDictionary<string, string> Permissions);

// This credential is deliberately ephemeral. Infrastructure creates it on demand and callers must
// keep it in memory only; it is never part of a persistence contract or API response.
public sealed record GitHubInstallationAccess(string Token, DateTime ExpiresAt);
public sealed record GitHubRepositoryDescriptor(long Id, string Owner, string Name, string FullName, bool Private,
    string DefaultBranch, string HtmlUrl);
public sealed record GitHubSourceFile(string Path, string Content, long Size);
public sealed record GitHubRepositorySnapshot(GitHubRepositoryDescriptor Repository, string RevisionSha,
    IReadOnlyDictionary<string, long> Languages, IReadOnlyList<GitHubSourceFile> Files,
    int TreeEntriesInspected, bool TreeTruncated, int CandidateFiles, long FetchedBytes,
    IReadOnlyList<string> Warnings);
public sealed record GitHubConnectionView(bool Connected, string? Login, string? TargetLogin, string? Status,
    DateTime? ConnectedAt);
public sealed record RepositoryView(Guid Id, long GitHubId, string FullName, bool Private, string DefaultBranch,
    string HtmlUrl, bool IncludedForAnalysis, string ScanStatus, Guid? AnalysisJobId, string? LastRevisionSha,
    DateTime? LastScanAt, string CoverageJson);
public sealed record GitHubEvidenceView(Guid Id, Guid RepositoryId, string Repository, string? SkillId,
    string OriginalTerm, string EvidenceType, string Detail, string Strength, decimal ExtractionConfidence,
    string Lifecycle, string RevisionSha, string? SourcePath, string Detector, DateTime ObservedAt);

public interface IGitHubWorkspace
{
    Task<Uri> BeginConnectionAsync(string userId, CancellationToken ct);
    Task CompleteConnectionAsync(string state, long installationId, string code, CancellationToken ct);
    Task<GitHubConnectionView> StatusAsync(string userId, CancellationToken ct);
    Task DisconnectAsync(string userId, CancellationToken ct);
    Task<IReadOnlyList<RepositoryView>> RepositoriesAsync(string userId, bool synchronize, CancellationToken ct);
    Task SaveSelectionAsync(string userId, IReadOnlyCollection<long> repositoryIds, CancellationToken ct);
    Task<IReadOnlyList<Guid>> ScanAsync(string userId, CancellationToken ct);
    Task<IReadOnlyList<GitHubEvidenceView>> EvidenceAsync(string userId, CancellationToken ct);
}

public interface IGitHubProvider
{
    Uri BuildInstallationUri(string state);
    Task<VerifiedGitHubInstallation> VerifyInstallationAsync(long installationId, string authorizationCode,
        string pkceVerifier, CancellationToken ct);
    Task<GitHubInstallationAccess> CreateInstallationAccessAsync(long installationId,
        IReadOnlyCollection<long> repositoryIds, CancellationToken ct);
    Task<IReadOnlyList<GitHubRepositoryDescriptor>> ListRepositoriesAsync(long installationId, CancellationToken ct);
    Task<GitHubRepositorySnapshot> ReadSnapshotAsync(long installationId, long repositoryId, CancellationToken ct);
    Task RevokeInstallationAsync(long installationId, CancellationToken ct);
}
