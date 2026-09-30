using Microsoft.EntityFrameworkCore;
using ProofPath.Application.Analysis;
using ProofPath.Application.Resumes;
using ProofPath.Application.Jobs;
using ProofPath.Domain.Entities;
using ProofPath.Infrastructure.Persistence;
using ProofPath.Infrastructure.Resumes;

// Test-only host, not referenced by the API, Worker or production solution.
var connection = Environment.GetEnvironmentVariable("PROOFPATH_E2E_CONNECTION")
    ?? throw new InvalidOperationException("An isolated E2E database is required.");
var root = Environment.GetEnvironmentVariable("PROOFPATH_E2E_STORAGE")
    ?? throw new InvalidOperationException("An isolated E2E storage directory is required.");
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:PrivateRoot"] = root, ["OpenAI:ApiKey"] = "" });
builder.Services.AddDbContext<ProofPathDbContext>(options => options.UseNpgsql(connection));
builder.Services.AddScoped<IAnalysisQueue, PostgresAnalysisQueue>();
builder.Services.AddResumeModule(builder.Configuration, "Testing", builder.Environment.ContentRootPath);
builder.Services.AddSingleton<ILlmProvider, SyntheticFixtureProvider>();
builder.Services.AddHostedService<ProofPath.Worker.Worker>();
var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<ProofPathDbContext>().Database.MigrateAsync();
app.MapGet("/health", () => Results.Ok());
app.MapPost("/fixtures/sprints-3-5", async (FixtureRequest request, ProofPathDbContext database, CancellationToken ct) =>
{
    var user = await database.Users.SingleAsync(item => item.Email == request.Email, ct);
    var profile = await database.CandidateProfiles.SingleAsync(item => item.UserId == user.Id, ct);
    var now = DateTime.UtcNow;

    var accountId = Guid.NewGuid();
    var repositoryId = Guid.NewGuid();
    var analysisJobId = Guid.NewGuid();
    var analysisId = Guid.NewGuid();
    var externalId = BitConverter.ToInt64(profile.Id.ToByteArray(), 0) & long.MaxValue;
    database.ConnectedAccounts.Add(new ConnectedAccount
    {
        Id = accountId, CandidateProfileId = profile.Id, Provider = "GitHub", ExternalAccountId = externalId,
        ExternalLogin = "proofpath-fixture", Status = ConnectedAccountStatus.Active, ConnectedAt = now, UpdatedAt = now
    });
    database.GitHubInstallations.Add(new GitHubInstallation
    {
        Id = Guid.NewGuid(), ConnectedAccountId = accountId, InstallationId = externalId,
        TargetAccountId = externalId, TargetLogin = "proofpath-fixture", TargetType = "User",
        RepositorySelection = "selected", PermissionsJson = "{\"metadata\":\"read\",\"contents\":\"read\"}",
        CreatedAt = now, UpdatedAt = now
    });
    database.Repositories.Add(new Repository
    {
        Id = repositoryId, CandidateProfileId = profile.Id, ConnectedAccountId = accountId, GitHubId = externalId,
        Owner = "proofpath-fixture", Name = "evidence-api", FullName = "proofpath-fixture/evidence-api",
        DefaultBranch = "main", HtmlUrl = "https://github.com/proofpath-fixture/evidence-api",
        IncludedForAnalysis = true, LastSyncedAt = now, LastScanAt = now,
        LastRevisionSha = "0123456789abcdef0123456789abcdef01234567", ScanStatus = RepositoryScanStatus.Completed,
        CoverageJson = "{\"filesRead\":3,\"candidateFiles\":3}"
    });
    database.AnalysisJobs.Add(new AnalysisJob
    {
        Id = analysisJobId, CandidateProfileId = profile.Id, Kind = AnalysisKind.Repository,
        ResourceId = repositoryId, InputVersion = "github-onboarding-v1", State = AnalysisState.Completed,
        AvailableAt = now, CreatedAt = now, UpdatedAt = now, ResultJson = "{}"
    });
    database.RepositoryAnalyses.Add(new RepositoryAnalysis
    {
        Id = analysisId, RepositoryId = repositoryId, AnalysisJobId = analysisJobId,
        RevisionSha = "0123456789abcdef0123456789abcdef01234567", ScanIdentity = $"{repositoryId}:fixture",
        Status = RepositoryScanStatus.Completed, CoverageJson = "{\"filesRead\":3,\"candidateFiles\":3}",
        ResultJson = "{}", CreatedAt = now
    });
    database.EvidenceItems.Add(new EvidenceItem
    {
        Id = Guid.NewGuid(), CandidateProfileId = profile.Id, RepositoryAnalysisId = analysisId,
        SkillId = "csharp", OriginalTerm = "C#", Context = "Implemented an ASP.NET Core API with authenticated endpoints.",
        Strength = "Strong", ExtractionConfidence = 1m, EvidenceType = "Implementation", Lifecycle = "Active",
        SourceBlockId = "src/Program.cs", Quote = "builder.Services.AddAuthentication();", ObservedAt = now,
        RevisionSha = "0123456789abcdef0123456789abcdef01234567", SourcePath = "src/Program.cs",
        StartLine = 12, EndLine = 12, Detector = "csharp-api", DetectorVersion = "github-extraction-v1"
    });

    const string description = "We require C# for building and maintaining authenticated ASP.NET Core services. The engineer will design APIs, write automated tests, review changes, and collaborate with the product team.";
    var source = new JobDescriptionSource([new JobSourceBlock("jd-1", description)], []);
    var draft = new JobRequirementDraftSet([
        new JobRequirementDraft("csharp", RequirementCategory.TechnicalSkill, RequirementLevel.Required,
            RequirementImportance.Critical, RequirementState.Extracted, "C# is required", "C#", "csharp",
            RequirementNormalizationStatus.Exact, null, [], null, RequirementGroupType.None, "jd-1", "We require C#")
    ]);
    var result = new JobAnalysisResult(source, draft, "synthetic-e2e-fixture", 0, 0, 0);
    var jobId = Guid.NewGuid();
    database.Jobs.Add(new Job
    {
        Id = jobId, CandidateProfileId = profile.Id, Company = "Acme", Title = "Backend Engineer",
        Description = description, DescriptionVersion = 1, Status = JobStatus.ReadyForReview,
        CreatedAt = now, UpdatedAt = now
    });
    database.JobRequirementExtractions.Add(new JobRequirementExtraction
    {
        Id = Guid.NewGuid(), JobId = jobId, DescriptionVersion = 1, Revision = 1,
        MachineJson = ResumeJson.Serialize(result), DraftJson = ResumeJson.Serialize(draft),
        Model = result.Model, CreatedAt = now
    });
    await database.SaveChangesAsync(ct);
    return Results.Ok(new { jobId, repositoryId });
});
await app.RunAsync();

internal sealed record FixtureRequest(string Email);

internal sealed class SyntheticFixtureProvider : ILlmProvider
{
    public Task<LlmResumeResult> ExtractResumeAsync(DocumentText source, CancellationToken ct)
    {
        var block = source.Blocks.First();
        if (!block.Text.Contains("Engineer at Acme. Built React applications.")) throw new AnalysisFailure("FIXTURE_INPUT_REQUIRED", false);
        var draft = new ResumeDraft([new FactDraft("Experience", "Engineer", "Acme", "Built React applications.", null, null, null, block.Id, "Engineer at Acme. Built React applications.")],
            [new SkillDraft("React", "ExperienceStatement", block.Id, "Built React applications.")], []);
        return Task.FromResult(new LlmResumeResult(draft, "synthetic-e2e-fixture", 0, 0, 0));
    }
}
