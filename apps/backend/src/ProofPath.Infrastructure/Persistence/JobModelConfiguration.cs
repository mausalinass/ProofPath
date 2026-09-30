using Microsoft.EntityFrameworkCore;
using ProofPath.Domain.Entities;

namespace ProofPath.Infrastructure.Persistence;

public static class JobModelConfiguration
{
    public static void ConfigureJobs(this ModelBuilder builder)
    {
        builder.Entity<Job>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Company).HasMaxLength(255);
            entity.Property(item => item.Title).HasMaxLength(255);
            entity.Property(item => item.Description).HasMaxLength(50_000).IsRequired();
            entity.Property(item => item.SourceUrl).HasMaxLength(2048);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(item => new { item.CandidateProfileId, item.UpdatedAt });
            entity.HasOne<CandidateProfile>().WithMany().HasForeignKey(item => item.CandidateProfileId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<JobRequirementExtraction>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.MachineJson).HasColumnType("jsonb");
            entity.Property(item => item.DraftJson).HasColumnType("jsonb");
            entity.HasIndex(item => new { item.JobId, item.DescriptionVersion }).IsUnique();
            entity.HasOne<Job>().WithMany().HasForeignKey(item => item.JobId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<RequirementSet>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.JobId, item.Version }).IsUnique();
            entity.HasIndex(item => item.JobRequirementExtractionId).IsUnique();
            entity.HasIndex(item => new { item.JobId, item.Active }).HasFilter("\"Active\" = true").IsUnique();
            entity.HasOne<Job>().WithMany().HasForeignKey(item => item.JobId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<JobRequirementExtraction>().WithOne().HasForeignKey<RequirementSet>(item => item.JobRequirementExtractionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<JobRequirement>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Key).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Category).HasConversion<string>().HasMaxLength(32);
            entity.Property(item => item.Level).HasConversion<string>().HasMaxLength(16);
            entity.Property(item => item.Importance).HasConversion<string>().HasMaxLength(16);
            entity.Property(item => item.State).HasConversion<string>().HasMaxLength(16);
            entity.Property(item => item.NormalizationStatus).HasConversion<string>().HasMaxLength(24);
            entity.Property(item => item.GroupType).HasConversion<string>().HasMaxLength(16);
            entity.Property(item => item.QualifiersJson).HasColumnType("jsonb");
            entity.HasIndex(item => new { item.RequirementSetId, item.Key }).IsUnique();
            entity.HasOne<RequirementSet>().WithMany().HasForeignKey(item => item.RequirementSetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Skill>().WithMany().HasForeignKey(item => item.SkillId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
