using Microsoft.EntityFrameworkCore;
using ProofPath.Domain.Entities;

namespace ProofPath.Infrastructure.Persistence;

public static class ResumeModelConfiguration
{
    public static void ConfigureResumes(this ModelBuilder builder)
    {
        builder.Entity<Resume>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.CandidateProfileId, item.Version }).IsUnique();
            entity.HasIndex(item => item.StorageKey).IsUnique();
            entity.HasOne<CandidateProfile>().WithMany().HasForeignKey(item => item.CandidateProfileId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ResumeExtraction>(entity =>
        {
            entity.HasKey(item => item.Id); entity.HasIndex(item => item.ResumeId).IsUnique();
            entity.Property(item => item.MachineJson).HasColumnType("jsonb");
            entity.Property(item => item.DraftJson).HasColumnType("jsonb");
            entity.HasOne<Resume>().WithOne().HasForeignKey<ResumeExtraction>(item => item.ResumeId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<CandidateFact>(entity =>
        {
            entity.UseTpcMappingStrategy(); entity.HasKey(item => item.Id);
            entity.HasOne<CandidateProfile>().WithMany().HasForeignKey(item => item.CandidateProfileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ResumeExtraction>().WithMany().HasForeignKey(item => item.ResumeExtractionId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<Experience>().ToTable("Experiences"); builder.Entity<Education>().ToTable("Educations");
        builder.Entity<Project>(entity =>
        {
            entity.ToTable("Projects");
            entity.HasIndex(item => item.RepositoryId).IsUnique().HasFilter("\"RepositoryId\" IS NOT NULL");
            entity.HasOne<Repository>().WithOne().HasForeignKey<Project>(item => item.RepositoryId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<Credential>().ToTable("Credentials");
        builder.Entity<Skill>().HasKey(item => item.Id);
        builder.Entity<SkillAlias>(entity =>
        {
            entity.HasKey(item => item.Alias);
            entity.HasOne<Skill>().WithMany().HasForeignKey(item => item.SkillId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<EvidenceItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasOne<CandidateProfile>().WithMany().HasForeignKey(item => item.CandidateProfileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ResumeExtraction>().WithMany().HasForeignKey(item => item.ResumeExtractionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Skill>().WithMany().HasForeignKey(item => item.SkillId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RepositoryAnalysis>().WithMany().HasForeignKey(item => item.RepositoryAnalysisId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Project>().WithMany().HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(item => item.ExtractionConfidence).HasPrecision(4, 3);
            entity.HasIndex(item => new { item.RepositoryAnalysisId, item.SkillId, item.SourcePath, item.Detector }).IsUnique()
                .HasFilter("\"RepositoryAnalysisId\" IS NOT NULL");
        });
        builder.Entity<BehavioralEvidenceItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasOne<CandidateProfile>().WithMany().HasForeignKey(item => item.CandidateProfileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ResumeExtraction>().WithMany().HasForeignKey(item => item.ResumeExtractionId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<PrivateFileDeletion>(entity => { entity.HasKey(item => item.Id); entity.HasIndex(item => item.StorageKey).IsUnique(); });
        // Initial supported stack from the project specification; unknown terms remain unmapped.
        var catalog = new Dictionary<string, string>
        {
            ["csharp"] = "C#", ["dotnet"] = ".NET", ["aspnet-core"] = "ASP.NET Core", ["entity-framework-core"] = "Entity Framework Core",
            ["react"] = "React", ["typescript"] = "TypeScript", ["javascript"] = "JavaScript", ["postgresql"] = "PostgreSQL",
            ["sql"] = "SQL", ["docker"] = "Docker", ["git"] = "Git", ["github-actions"] = "GitHub Actions",
            ["python"] = "Python", ["java"] = "Java", ["nodejs"] = "Node.js", ["express"] = "Express",
            ["rest-api"] = "REST APIs", ["dependency-injection"] = "Dependency Injection",
            ["authentication-authorization"] = "Authentication & Authorization", ["automated-testing"] = "Automated Testing",
            ["ci-cd"] = "CI/CD", ["aws"] = "AWS"
        };
        builder.Entity<Skill>().HasData(catalog.Select(item => new Skill { Id = item.Key, DisplayName = item.Value }));
        builder.Entity<SkillAlias>().HasData(catalog.Select(item => new SkillAlias { Alias = item.Value.ToUpperInvariant(), SkillId = item.Key })
            .Concat([new SkillAlias { Alias = "EF CORE", SkillId = "entity-framework-core" },
                new SkillAlias { Alias = "POSTGRES", SkillId = "postgresql" }, new SkillAlias { Alias = "CSHARP", SkillId = "csharp" },
                new SkillAlias { Alias = ".NET WEB API", SkillId = "aspnet-core" }, new SkillAlias { Alias = "ASP.NET WEB API", SkillId = "aspnet-core" },
                new SkillAlias { Alias = "NODE", SkillId = "nodejs" }, new SkillAlias { Alias = "REST", SkillId = "rest-api" },
                new SkillAlias { Alias = "AUTH", SkillId = "authentication-authorization" }, new SkillAlias { Alias = "TESTING", SkillId = "automated-testing" }]));
    }
}
