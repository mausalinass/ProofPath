using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProofPath.Domain.Entities;
using ProofPath.Domain.Matching;

namespace ProofPath.Infrastructure.Persistence;

public static class MatchingModelConfiguration
{
    public static void ConfigureMatching(this ModelBuilder builder)
    {
        builder.Entity<MatchingScoringVersion>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasMaxLength(64);
            entity.Property(item => item.ConfigurationJson).HasColumnType("jsonb");
            entity.HasData(new MatchingScoringVersion
            {
                Id = MatchingConfiguration.V1.Version,
                ConfigurationJson = JsonSerializer.Serialize(MatchingConfiguration.V1),
                Frozen = true,
                CreatedAt = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc)
            });
        });
        builder.Entity<MatchResult>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.CandidateSnapshotJson).HasColumnType("jsonb");
            entity.Property(item => item.RequirementSnapshotJson).HasColumnType("jsonb");
            entity.Property(item => item.ResultJson).HasColumnType("jsonb");
            entity.Property(item => item.OverallScore).HasPrecision(6, 2);
            entity.Property(item => item.OverallConfidence).HasPrecision(6, 4);
            entity.Property(item => item.EvaluationCoverage).HasPrecision(6, 4);
            entity.Property(item => item.OverallClassification).HasMaxLength(32);
            entity.Property(item => item.OverallStatus).HasMaxLength(32);
            entity.HasIndex(item => new { item.JobId, item.CreatedAt });
            entity.HasOne<Job>().WithMany().HasForeignKey(item => item.JobId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<RequirementSet>().WithMany().HasForeignKey(item => item.RequirementSetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MatchingScoringVersion>().WithMany().HasForeignKey(item => item.ScoringVersionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<RequirementMatchRecord>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.RequirementSnapshotJson).HasColumnType("jsonb");
            entity.Property(item => item.ResultSnapshotJson).HasColumnType("jsonb");
            entity.HasIndex(item => new { item.MatchResultId, item.RequirementId }).IsUnique();
            entity.HasOne<MatchResult>().WithMany().HasForeignKey(item => item.MatchResultId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<RequirementMatchEvidenceRecord>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EvidenceSnapshotJson).HasColumnType("jsonb");
            entity.Property(item => item.Contribution).HasPrecision(6, 4);
            entity.HasIndex(item => new { item.RequirementMatchRecordId, item.EvidenceItemId }).IsUnique();
            entity.HasOne<RequirementMatchRecord>().WithMany().HasForeignKey(item => item.RequirementMatchRecordId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
