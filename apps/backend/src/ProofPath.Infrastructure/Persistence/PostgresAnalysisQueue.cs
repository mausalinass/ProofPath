using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProofPath.Application.Analysis;
using ProofPath.Domain.Entities;

namespace ProofPath.Infrastructure.Persistence;

public sealed class PostgresAnalysisQueue(ProofPathDbContext database, IEnumerable<IAnalysisCompletion> completions) : IAnalysisQueue
{
    private const int MaxAttempts = 3;
    // A handler has a four-minute timeout; the five-minute lease also covers persistence.
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    public async Task<Guid> EnqueueAsync(string userId, AnalysisKind kind, Guid resourceId, string inputVersion, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputVersion);
        if (!Enum.IsDefined(kind) || resourceId == Guid.Empty) throw new ArgumentException("Invalid analysis input.");
        var profileId = await database.CandidateProfiles.Where(profile => profile.UserId == userId).Select(profile => (Guid?)profile.Id).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Candidate profile is required before analysis.");
        var id = Guid.NewGuid(); var now = DateTime.UtcNow; var kindName = kind.ToString();
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AnalysisJobs" ("Id", "CandidateProfileId", "Kind", "ResourceId", "InputVersion", "State",
                "Attempts", "AvailableAt", "CreatedAt", "UpdatedAt", "Retryable")
            VALUES ({id}, {profileId}, {kindName}, {resourceId}, {inputVersion}, 'Pending', 0, {now}, {now}, {now}, false)
            ON CONFLICT ("CandidateProfileId", "Kind", "ResourceId", "InputVersion") DO NOTHING
            """, ct);
        return await database.AnalysisJobs.Where(job => job.CandidateProfileId == profileId && job.Kind == kind &&
            job.ResourceId == resourceId && job.InputVersion == inputVersion).Select(job => job.Id).SingleAsync(ct);
    }

    public async Task<AnalysisLease?> ClaimAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "AnalysisJobs" SET "State" = CASE WHEN "Attempts" >= {MaxAttempts} THEN 'Failed' ELSE 'Pending' END,
                "LeaseToken" = NULL, "LeaseExpiresAt" = NULL, "UpdatedAt" = {now}, "AvailableAt" = {now},
                "ErrorCode" = 'WORKER_INTERRUPTED', "Retryable" = true
            WHERE "State" = 'Processing' AND "LeaseExpiresAt" <= {now}
            """, ct);
        var token = Guid.NewGuid(); var expires = now + LeaseDuration;
        // One SQL statement locks and claims exactly one job, even across multiple workers.
        var claimed = await database.AnalysisJobs.FromSqlInterpolated($"""
            UPDATE "AnalysisJobs" SET "State" = 'Processing', "Attempts" = "Attempts" + 1,
                "LeaseToken" = {token}, "LeaseExpiresAt" = {expires}, "UpdatedAt" = {now}, "ErrorCode" = NULL
            WHERE "Id" = (SELECT "Id" FROM "AnalysisJobs"
                WHERE "State" = 'Pending' AND "AvailableAt" <= {now} AND "Attempts" < {MaxAttempts}
                ORDER BY "AvailableAt", "CreatedAt", "Id" FOR UPDATE SKIP LOCKED LIMIT 1)
            RETURNING *
            """).AsNoTracking().ToListAsync(ct);
        var job = claimed.SingleOrDefault();
        return job is null ? null : new AnalysisLease(job.Id, job.CandidateProfileId, job.Kind, job.ResourceId,
            job.InputVersion, token, expires, job.Attempts);
    }

    public async Task<bool> CompleteAsync(AnalysisLease lease, AnalysisOutput output, CancellationToken ct)
    {
        using var parsed = JsonDocument.Parse(output.ResultJson);
        await using var transaction = await database.Database.BeginTransactionAsync(ct);
        var state = output.Partial ? AnalysisState.PartiallyCompleted : AnalysisState.Completed;
        var now = DateTime.UtcNow;
        var saved = await Current(lease, now).ExecuteUpdateAsync(set => set.SetProperty(job => job.State, state)
            .SetProperty(job => job.ResultJson, output.ResultJson).SetProperty(job => job.UpdatedAt, now)
            .SetProperty(job => job.LeaseToken, (Guid?)null).SetProperty(job => job.LeaseExpiresAt, (DateTime?)null)
            .SetProperty(job => job.ErrorCode, (string?)null).SetProperty(job => job.Retryable, false), ct) == 1;
        if (!saved) return false;
        foreach (var completion in completions) await completion.SaveAsync(lease, output, ct);
        await transaction.CommitAsync(ct); return true;
    }

    public async Task<bool> FailAsync(AnalysisLease lease, string errorCode, bool retryable, CancellationToken ct)
    {
        // Error codes are operational constants, never exception messages or provider response bodies.
        if (string.IsNullOrWhiteSpace(errorCode) || errorCode.Length > 64 ||
            errorCode.Any(character => character != '_' && !char.IsAsciiLetterUpper(character)))
            throw new ArgumentException("Use a sanitized error code.");
        var now = DateTime.UtcNow;
        var state = retryable && lease.Attempt < MaxAttempts ? AnalysisState.Pending : AnalysisState.Failed;
        var next = now.AddSeconds(15 * Math.Pow(2, lease.Attempt - 1));
        return await Current(lease, now).ExecuteUpdateAsync(set => set.SetProperty(job => job.State, state)
            .SetProperty(job => job.ErrorCode, errorCode).SetProperty(job => job.Retryable, retryable)
            .SetProperty(job => job.AvailableAt, next).SetProperty(job => job.UpdatedAt, now)
            .SetProperty(job => job.LeaseToken, (Guid?)null).SetProperty(job => job.LeaseExpiresAt, (DateTime?)null), ct) == 1;
    }

    public Task<bool> IsCurrentAsync(AnalysisLease lease, CancellationToken ct) => Current(lease, DateTime.UtcNow).AnyAsync(ct);

    private IQueryable<AnalysisJob> Current(AnalysisLease lease, DateTime now) => database.AnalysisJobs.Where(job =>
        job.Id == lease.Id && job.CandidateProfileId == lease.CandidateProfileId && job.State == AnalysisState.Processing &&
        job.LeaseToken == lease.Token && job.LeaseExpiresAt > now);

    public Task<AnalysisStatus?> GetAsync(string userId, Guid id, CancellationToken ct) => database.AnalysisJobs.AsNoTracking()
        .Where(job => database.CandidateProfiles.Any(profile => profile.Id == job.CandidateProfileId && profile.UserId == userId) && job.Id == id).Select(job => new AnalysisStatus(job.Id, job.Kind,
            job.ResourceId, job.State, job.Attempts, job.ErrorCode, job.Retryable, job.CreatedAt, job.UpdatedAt)).SingleOrDefaultAsync(ct);

    public async Task<bool> CancelAsync(string userId, Guid id, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await database.AnalysisJobs.Where(job => database.CandidateProfiles.Any(profile => profile.Id == job.CandidateProfileId && profile.UserId == userId) && job.Id == id &&
            (job.State == AnalysisState.Pending || job.State == AnalysisState.Processing || job.State == AnalysisState.Failed))
            .ExecuteUpdateAsync(set => set.SetProperty(job => job.State, AnalysisState.Cancelled)
                .SetProperty(job => job.LeaseToken, (Guid?)null).SetProperty(job => job.LeaseExpiresAt, (DateTime?)null)
                .SetProperty(job => job.UpdatedAt, now).SetProperty(job => job.Retryable, false), ct) == 1;
    }

    public async Task<bool> RetryAsync(string userId, Guid id, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await database.AnalysisJobs.Where(job => database.CandidateProfiles.Any(profile => profile.Id == job.CandidateProfileId && profile.UserId == userId) && job.Id == id &&
            job.State == AnalysisState.Failed && job.Retryable).ExecuteUpdateAsync(set =>
                set.SetProperty(job => job.State, AnalysisState.Pending).SetProperty(job => job.Attempts, 0)
                .SetProperty(job => job.AvailableAt, now).SetProperty(job => job.UpdatedAt, now)
                .SetProperty(job => job.ErrorCode, (string?)null).SetProperty(job => job.Retryable, false), ct) == 1;
    }
}
