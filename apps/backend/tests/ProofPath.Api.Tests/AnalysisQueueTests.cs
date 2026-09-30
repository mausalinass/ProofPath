using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProofPath.Application.Analysis;
using ProofPath.Domain.Entities;
using ProofPath.Infrastructure.Identity;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Api.Tests;

[Collection("PostgreSQL API")]
public sealed class AnalysisQueueTests(ApiFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProofPathDbContext>().AnalysisJobs.ExecuteDeleteAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;
    private async Task<string> Owner()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ProofPathDbContext>();
        var id = Guid.NewGuid().ToString();
        database.Users.Add(new ApplicationUser { Id = id, UserName = id, NormalizedUserName = id.ToUpperInvariant() });
        database.CandidateProfiles.Add(new CandidateProfile { Id = Guid.NewGuid(), UserId = id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await database.SaveChangesAsync(); return id;
    }
    private IAnalysisQueue Queue(IServiceProvider provider) => provider.GetRequiredService<IAnalysisQueue>();
    private async Task<Guid> Enqueue(string owner)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await Queue(scope.ServiceProvider).EnqueueAsync(owner, AnalysisKind.Resume, Guid.NewGuid(), "resume-v1", default);
    }

    [Fact]
    public async Task EnqueueIsIdempotentAndSurvivesNewScope()
    {
        var owner = await Owner(); var resource = Guid.NewGuid(); Guid id;
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var queue = Queue(scope.ServiceProvider);
            id = await queue.EnqueueAsync(owner, AnalysisKind.Resume, resource, "v1", default);
            Assert.Equal(id, await queue.EnqueueAsync(owner, AnalysisKind.Resume, resource, "v1", default));
        }
        await using var reopened = fixture.Factory.Services.CreateAsyncScope();
        var status = await Queue(reopened.ServiceProvider).GetAsync(owner, id, default);
        Assert.Equal(AnalysisState.Pending, status!.State);
    }

    [Fact]
    public async Task TwoWorkersCannotClaimTheSameJob()
    {
        var owner = await Owner(); var id = await Enqueue(owner);
        await using var first = fixture.Factory.Services.CreateAsyncScope();
        await using var second = fixture.Factory.Services.CreateAsyncScope();
        var claims = await Task.WhenAll(Queue(first.ServiceProvider).ClaimAsync(default), Queue(second.ServiceProvider).ClaimAsync(default));
        var lease = Assert.Single(claims, item => item is not null);
        Assert.Equal(id, lease!.Id); Assert.Equal(1, lease.Attempt);
    }

    [Fact]
    public async Task ExpiredLeaseIsRecoveredAndOldWorkerCannotCommit()
    {
        var owner = await Owner(); await Enqueue(owner);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var queue = Queue(scope.ServiceProvider);
        var old = (await queue.ClaimAsync(default))!;
        var database = scope.ServiceProvider.GetRequiredService<ProofPathDbContext>();
        await database.AnalysisJobs.Where(job => job.Id == old.Id).ExecuteUpdateAsync(set =>
            set.SetProperty(job => job.LeaseExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        await using var restarted = fixture.Factory.Services.CreateAsyncScope(); var currentQueue = Queue(restarted.ServiceProvider);
        var current = (await currentQueue.ClaimAsync(default))!;
        Assert.Equal(old.Id, current.Id); Assert.NotEqual(old.Token, current.Token); Assert.Equal(2, current.Attempt);
        Assert.False(await queue.CompleteAsync(old, new AnalysisOutput("{}"), default));
        Assert.True(await currentQueue.CompleteAsync(current, new AnalysisOutput("{\"reviewRequired\":true}"), default));
        Assert.False(await currentQueue.CompleteAsync(current, new AnalysisOutput("{}"), default));
        Assert.Equal(AnalysisState.Completed, (await queue.GetAsync(owner, old.Id, default))!.State);
    }

    [Fact]
    public async Task ForeignOwnerCannotObserveCancelOrRetry()
    {
        var owner = await Owner(); var outsider = await Owner(); var id = await Enqueue(owner);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var queue = Queue(scope.ServiceProvider);
        Assert.Null(await queue.GetAsync(outsider, id, default));
        Assert.False(await queue.CancelAsync(outsider, id, default));
        Assert.False(await queue.RetryAsync(outsider, id, default));
        var lease = (await queue.ClaimAsync(default))!;
        Assert.True(await queue.CancelAsync(owner, id, default));
        Assert.False(await queue.CompleteAsync(lease, new AnalysisOutput("{}"), default));
        Assert.Equal(AnalysisState.Cancelled, (await queue.GetAsync(owner, id, default))!.State);
    }

    [Fact]
    public async Task RetryBackoffIsFiniteAndManualRetryRequiresRetryableFailure()
    {
        var owner = await Owner(); var id = await Enqueue(owner);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var queue = Queue(scope.ServiceProvider);
        var database = scope.ServiceProvider.GetRequiredService<ProofPathDbContext>();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var lease = (await queue.ClaimAsync(default))!; Assert.Equal(attempt, lease.Attempt);
            Assert.True(await queue.FailAsync(lease, "PROVIDER_UNAVAILABLE", true, default));
            Assert.Null(await queue.ClaimAsync(default));
            if (attempt < 3) await database.AnalysisJobs.Where(job => job.Id == id)
                .ExecuteUpdateAsync(set => set.SetProperty(job => job.AvailableAt, DateTime.UtcNow.AddSeconds(-1)));
        }
        Assert.Equal(AnalysisState.Failed, (await queue.GetAsync(owner, id, default))!.State);
        Assert.True(await queue.RetryAsync(owner, id, default));
        var retried = (await queue.ClaimAsync(default))!; Assert.Equal(1, retried.Attempt);
        Assert.True(await queue.FailAsync(retried, "INVALID_DOCUMENT", false, default));
        Assert.False(await queue.RetryAsync(owner, id, default));
    }

    [Fact]
    public async Task AccountDeletionPreventsWorkerFromRepopulatingJobs()
    {
        var owner = await Owner(); await Enqueue(owner);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var queue = Queue(scope.ServiceProvider);
        var lease = (await queue.ClaimAsync(default))!;
        await scope.ServiceProvider.GetRequiredService<ProofPathDbContext>().Users.Where(user => user.Id == owner).ExecuteDeleteAsync();
        Assert.False(await queue.CompleteAsync(lease, new AnalysisOutput("{}"), default));
        Assert.Null(await queue.GetAsync(owner, lease.Id, default));
    }
}
