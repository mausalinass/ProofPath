namespace ProofPath.Domain.Entities;

public enum ConnectedAccountStatus { Active, Disconnected, Revoked }

public sealed class ConnectedAccount
{
    public Guid Id { get; set; }
    public Guid CandidateProfileId { get; set; }
    public string Provider { get; set; } = "GitHub";
    public long ExternalAccountId { get; set; }
    public string ExternalLogin { get; set; } = "";
    public ConnectedAccountStatus Status { get; set; } = ConnectedAccountStatus.Active;
    public DateTime ConnectedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DisconnectedAt { get; set; }
}

public sealed class GitHubInstallation
{
    public Guid Id { get; set; }
    public Guid ConnectedAccountId { get; set; }
    public long InstallationId { get; set; }
    public long TargetAccountId { get; set; }
    public string TargetLogin { get; set; } = "";
    public string TargetType { get; set; } = "";
    public string RepositorySelection { get; set; } = "";
    public string PermissionsJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? SuspendedAt { get; set; }
}

// The browser receives only a one-time nonce. The verifier remains inside a protected server-side envelope.
public sealed class GitHubConnectionAttempt
{
    public Guid Id { get; set; }
    public Guid CandidateProfileId { get; set; }
    public string NonceHash { get; set; } = "";
    public string ProtectedPayload { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
