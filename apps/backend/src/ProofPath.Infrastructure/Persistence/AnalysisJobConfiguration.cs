using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProofPath.Domain.Entities;


namespace ProofPath.Infrastructure.Persistence;

public sealed class AnalysisJobConfiguration : IEntityTypeConfiguration<AnalysisJob>
{
    public void Configure(EntityTypeBuilder<AnalysisJob> entity)
    {
        entity.HasKey(job => job.Id);
        entity.Property(job => job.Kind).HasConversion<string>();
        entity.Property(job => job.State).HasConversion<string>();

        entity.Property(job => job.InputVersion).IsRequired();
        entity.Property(job => job.ResultJson).HasColumnType("jsonb");
        entity.HasIndex(job => new { job.CandidateProfileId, job.Kind, job.ResourceId, job.InputVersion }).IsUnique();
        entity.HasIndex(job => new { job.State, job.AvailableAt });
        entity.HasOne<CandidateProfile>().WithMany().HasForeignKey(job => job.CandidateProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}
