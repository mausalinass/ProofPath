using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProofPath.Application.Analysis;
using ProofPath.Application.Files;
using ProofPath.Application.Resumes;
using ProofPath.Domain.Entities;
using ProofPath.Infrastructure.Persistence;
using ProofPath.Infrastructure.Resumes;

namespace ProofPath.Api.Tests;

public sealed class ResumePolicyTests
{
    [Fact]
    public void ContactRedactionPreservesProfessionalDates()
    {
        var text = ResumeProviderInput.RedactText("test@example.test +1 (212) 555-1234 Engineer 2021-2024");
        Assert.DoesNotContain("test@example.test", text); Assert.DoesNotContain("555-1234", text); Assert.Contains("2021-2024", text);
    }
    [Fact]
    public void BehavioralRulesRejectUnrelatedThemesAndDoNotScorePersonality()
    {
        Assert.Equal("Weak", BehaviorEvidenceRules.Strength("LEADERSHIP", "Leadership"));
        Assert.Equal("Moderate", BehaviorEvidenceRules.Strength("MENTORING", "Mentored teammates."));
        Assert.Null(BehaviorEvidenceRules.Strength("LEADERSHIP", "Built React applications."));
        Assert.Null(BehaviorEvidenceRules.Strength("PERSONALITY", "Strong personality"));
    }
    private sealed class Input : IResumeInputReader
    {
        public Task<ResumeInput?> GetInputAsync(Guid profile, Guid id, CancellationToken ct) => Task.FromResult<ResumeInput?>(new(id, Guid.NewGuid().ToString("N"), DocumentTextExtractor.Docx));
    }
    private sealed class FileStore : IPrivateFileStore
    {
        public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) => Task.FromResult<Stream?>(new MemoryStream(ResumeFixtures.Docx()));
        public Task<StoredPrivateFile> PutAsync(Stream content, CancellationToken ct) => throw new NotSupportedException();
        public Task<StoredPrivateFile> PutAsync(string key, Stream content, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class InvalidBehaviorProvider : ILlmProvider
    {
        public Task<LlmResumeResult> ExtractResumeAsync(DocumentText source, CancellationToken ct)
        {
            var draft = ResumeFixtures.Draft(source) with { Behaviors = [new BehaviorDraft("LEADERSHIP", "Built React applications.", source.Blocks[0].Id, "Built React applications.")] };
            return Task.FromResult(new LlmResumeResult(draft, "fixture", 0, 0, 0));
        }
    }
    [Fact]
    public async Task InvalidBehaviorDoesNotDiscardValidTechnicalFacts()
    {
        var handler = new ResumeAnalysisHandler(new Input(), new FileStore(), new DocumentTextExtractor(), new InvalidBehaviorProvider());
        var lease = new AnalysisLease(Guid.NewGuid(), Guid.NewGuid(), AnalysisKind.Resume, Guid.NewGuid(), "v1", Guid.NewGuid(), DateTime.UtcNow.AddMinutes(5), 1);
        var output = await handler.ProcessAsync(lease, default); Assert.True(output.Partial);
        var result = ResumeJson.Read<ResumeAnalysisResult>(output.ResultJson);
        Assert.Single(result.Draft.Facts); Assert.Single(result.Draft.Skills); Assert.Empty(result.Draft.Behaviors);
        Assert.Contains(result.Source.Warnings, warning => warning.Contains("behavioral"));
    }
}

[Collection("PostgreSQL API")]
public sealed class ResumeRecoveryTests(ApiFixture fixture)
{
    private sealed class RecoverableStore(IPrivateFileStore inner) : IPrivateFileStore
    {
        public bool Fail { get; set; } = true;
        public Task DeleteAsync(string key, CancellationToken ct) => Fail ? throw new IOException("fixture outage") : inner.DeleteAsync(key, ct);
        public Task<Stream?> OpenReadAsync(string key, CancellationToken ct) => inner.OpenReadAsync(key, ct);
        public Task<StoredPrivateFile> PutAsync(Stream content, CancellationToken ct) => inner.PutAsync(content, ct);
        public Task<StoredPrivateFile> PutAsync(string key, Stream content, CancellationToken ct) => inner.PutAsync(key, content, ct);
    }
    [Fact]
    public async Task FileDeletionSurvivesStorageFailureAndRetriesDurably()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ProofPathDbContext>();
        var store = new RecoverableStore(scope.ServiceProvider.GetRequiredService<IPrivateFileStore>());
        var stored = await store.PutAsync(new MemoryStream([1, 2, 3]), default);
        var deletion = new PrivateFileDeletion { Id = Guid.NewGuid(), StorageKey = stored.Key, CreatedAt = DateTime.UtcNow, NextAttemptAt = DateTime.UtcNow };
        db.PrivateFileDeletions.Add(deletion); await db.SaveChangesAsync();
        var cleanup = new PrivateFileCleanup(db, store); await cleanup.RunAsync(default);
        var pending = await db.PrivateFileDeletions.AsNoTracking().SingleAsync(item => item.Id == deletion.Id);
        Assert.Equal(1, pending.Attempts); Assert.True(pending.NextAttemptAt > DateTime.UtcNow);
        store.Fail = false;
        await db.PrivateFileDeletions.Where(item => item.Id == deletion.Id).ExecuteUpdateAsync(set => set.SetProperty(item => item.NextAttemptAt, DateTime.UtcNow.AddMinutes(-1)));
        await cleanup.RunAsync(default);
        Assert.False(await db.PrivateFileDeletions.AnyAsync(item => item.Id == deletion.Id));
        Assert.Null(await store.OpenReadAsync(stored.Key, default));
    }
}
