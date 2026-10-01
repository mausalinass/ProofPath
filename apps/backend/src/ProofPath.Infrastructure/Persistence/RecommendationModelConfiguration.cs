using Microsoft.EntityFrameworkCore;
using ProofPath.Domain.Entities;

namespace ProofPath.Infrastructure.Persistence;

public static class RecommendationModelConfiguration
{
    public static void ConfigureRecommendations(this ModelBuilder builder)
    {
        builder.Entity<JobTracking>(entity =>
        {
            entity.HasKey(item => item.JobId);
            entity.Property(item => item.Stage).HasConversion<string>().HasMaxLength(24);
            entity.Property(item => item.Notes).HasMaxLength(2_000);
            entity.HasOne<Job>().WithOne().HasForeignKey<JobTracking>(item => item.JobId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<MatchRecommendation>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Kind).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Title).HasMaxLength(255).IsRequired();
            entity.Property(item => item.Rationale).HasMaxLength(1_000).IsRequired();
            entity.Property(item => item.Action).HasMaxLength(1_000).IsRequired();
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(24);
            entity.HasIndex(item => new { item.MatchResultId, item.Rank }).IsUnique();
            entity.HasOne<MatchResult>().WithMany().HasForeignKey(item => item.MatchResultId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
