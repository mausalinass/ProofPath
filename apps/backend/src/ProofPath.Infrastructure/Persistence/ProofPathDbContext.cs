using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProofPath.Domain.Entities;
using ProofPath.Infrastructure.Identity;

namespace ProofPath.Infrastructure.Persistence;

public class ProofPathDbContext : IdentityDbContext<ApplicationUser>
{
    public ProofPathDbContext(DbContextOptions<ProofPathDbContext> options)
        : base(options)
    {
    }

    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();

    public DbSet<AnalysisJob> AnalysisJobs => Set<AnalysisJob>();

    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<ResumeExtraction> ResumeExtractions => Set<ResumeExtraction>();
    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<Education> Educations => Set<Education>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Credential> Credentials => Set<Credential>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<SkillAlias> SkillAliases => Set<SkillAlias>();
    public DbSet<EvidenceItem> EvidenceItems => Set<EvidenceItem>();
    public DbSet<BehavioralEvidenceItem> BehavioralEvidenceItems => Set<BehavioralEvidenceItem>();
    public DbSet<PrivateFileDeletion> PrivateFileDeletions => Set<PrivateFileDeletion>();
    public DbSet<ConnectedAccount> ConnectedAccounts => Set<ConnectedAccount>();
    public DbSet<GitHubInstallation> GitHubInstallations => Set<GitHubInstallation>();
    public DbSet<GitHubConnectionAttempt> GitHubConnectionAttempts => Set<GitHubConnectionAttempt>();
    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<RepositoryAnalysis> RepositoryAnalyses => Set<RepositoryAnalysis>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobRequirementExtraction> JobRequirementExtractions => Set<JobRequirementExtraction>();
    public DbSet<RequirementSet> RequirementSets => Set<RequirementSet>();
    public DbSet<JobRequirement> JobRequirements => Set<JobRequirement>();
    public DbSet<MatchingScoringVersion> MatchingScoringVersions => Set<MatchingScoringVersion>();
    public DbSet<MatchResult> MatchResults => Set<MatchResult>();
    public DbSet<RequirementMatchRecord> RequirementMatchRecords => Set<RequirementMatchRecord>();
    public DbSet<RequirementMatchEvidenceRecord> RequirementMatchEvidenceRecords => Set<RequirementMatchEvidenceRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new AnalysisJobConfiguration());
        builder.ConfigureResumes();
        builder.ConfigureGitHub();
        builder.ConfigureJobs();
        builder.ConfigureMatching();

        builder.Entity<CandidateProfile>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId)
                  .IsRequired();

            entity.HasIndex(e => e.UserId)
                  .IsUnique();

            entity.HasOne<ApplicationUser>()
                  .WithOne()
                  .HasForeignKey<CandidateProfile>(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
