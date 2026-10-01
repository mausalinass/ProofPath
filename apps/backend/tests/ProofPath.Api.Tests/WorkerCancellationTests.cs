using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using ProofPath.Application.Analysis;
using ProofPath.Domain.Entities;
using ProofPath.Infrastructure.Identity;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Api.Tests;

[Collection("PostgreSQL API")]
public sealed class WorkerCancellationTests(ApiFixture fixture)
{
    private sealed class BlockingHandler : IAnalysisHandler
    {
        public AnalysisKind Kind => AnalysisKind.Resume;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<AnalysisOutput> ProcessAsync(AnalysisLease lease, CancellationToken ct)
        {
            Started.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); return new AnalysisOutput("{}"); }
            catch (OperationCanceledException) { Cancelled.TrySetResult(); throw; }
        }
    }
    [Fact]
    public async Task CancellationReachesTheRunningHandlerAndCannotPublishAResult()
    {
        var handler = new BlockingHandler();
        await using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        { services.RemoveAll<IAnalysisHandler>(); services.AddSingleton<IAnalysisHandler>(handler); }));
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ProofPathDbContext>();
        await db.AnalysisJobs.ExecuteDeleteAsync();
        var owner = Guid.NewGuid().ToString(); var profile = Guid.NewGuid();
        db.Users.Add(new ApplicationUser { Id = owner, UserName = owner, NormalizedUserName = owner.ToUpperInvariant() });
        db.CandidateProfiles.Add(new CandidateProfile { Id = profile, UserId = owner, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var queue = scope.ServiceProvider.GetRequiredService<IAnalysisQueue>();
        var id = await queue.EnqueueAsync(owner, AnalysisKind.Resume, Guid.NewGuid(), "cancellation-test", default);
        using var worker = new ProofPath.Worker.Worker(factory.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<ProofPath.Worker.Worker>.Instance);
        await worker.StartAsync(default);
        try
        {
            await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(await queue.CancelAsync(owner, id, default));
            await handler.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(8));
            Assert.Equal(AnalysisState.Cancelled, (await queue.GetAsync(owner, id, default))!.State);
            Assert.Null((await db.AnalysisJobs.AsNoTracking().SingleAsync(job => job.Id == id)).ResultJson);
        }
        finally { await worker.StopAsync(default); }
    }
}
