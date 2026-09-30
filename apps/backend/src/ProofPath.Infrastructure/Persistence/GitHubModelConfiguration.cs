using Microsoft.EntityFrameworkCore;
using ProofPath.Domain.Entities;

namespace ProofPath.Infrastructure.Persistence;

public static class GitHubModelConfiguration
{
    public static void ConfigureGitHub(this ModelBuilder builder)
    {
        builder.Entity<ConnectedAccount>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Provider).HasMaxLength(32).IsRequired();
            entity.Property(item => item.ExternalLogin).HasMaxLength(255).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(item => new { item.CandidateProfileId, item.Provider }).IsUnique();
            entity.HasIndex(item => new { item.Provider, item.ExternalAccountId }).IsUnique();
            entity.HasOne<CandidateProfile>().WithMany().HasForeignKey(item => item.CandidateProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<GitHubInstallation>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TargetLogin).HasMaxLength(255).IsRequired();
            entity.Property(item => item.TargetType).HasMaxLength(32).IsRequired();
            entity.Property(item => item.RepositorySelection).HasMaxLength(32).IsRequired();
            entity.Property(item => item.PermissionsJson).HasColumnType("jsonb");
            entity.HasIndex(item => item.InstallationId).IsUnique();
            entity.HasIndex(item => item.ConnectedAccountId).IsUnique();
            entity.HasOne<ConnectedAccount>().WithOne().HasForeignKey<GitHubInstallation>(item => item.ConnectedAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<GitHubConnectionAttempt>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.NonceHash).HasMaxLength(64).IsRequired();
            entity.Property(item => item.ProtectedPayload).HasMaxLength(4096).IsRequired();
            entity.HasIndex(item => item.NonceHash).IsUnique();
            entity.HasIndex(item => item.ExpiresAt);
            entity.HasOne<CandidateProfile>().WithMany().HasForeignKey(item => item.CandidateProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<Repository>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Owner).HasMaxLength(255).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(255).IsRequired();
            entity.Property(item => item.FullName).HasMaxLength(512).IsRequired();
            entity.Property(item => item.DefaultBranch).HasMaxLength(255).IsRequired();
            entity.Property(item => item.HtmlUrl).HasMaxLength(2048).IsRequired();
            entity.Property(item => item.ScanStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.CoverageJson).HasColumnType("jsonb");
            entity.HasIndex(item => new { item.CandidateProfileId, item.GitHubId }).IsUnique();
            entity.HasIndex(item => new { item.CandidateProfileId, item.IncludedForAnalysis });
            entity.HasOne<CandidateProfile>().WithMany().HasForeignKey(item => item.CandidateProfileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ConnectedAccount>().WithMany().HasForeignKey(item => item.ConnectedAccountId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<RepositoryAnalysis>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.RevisionSha).HasMaxLength(64).IsRequired();
            entity.Property(item => item.ExtractionVersion).HasMaxLength(64).IsRequired();
            entity.Property(item => item.AnalysisPolicyVersion).HasMaxLength(64).IsRequired();
            entity.Property(item => item.ScanIdentity).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.CoverageJson).HasColumnType("jsonb");
            entity.Property(item => item.WarningsJson).HasColumnType("jsonb");
            entity.Property(item => item.ResultJson).HasColumnType("jsonb");
            entity.HasIndex(item => item.ScanIdentity).IsUnique();
            entity.HasIndex(item => item.AnalysisJobId).IsUnique();
            entity.HasOne<Repository>().WithMany().HasForeignKey(item => item.RepositoryId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<AnalysisJob>().WithMany().HasForeignKey(item => item.AnalysisJobId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
