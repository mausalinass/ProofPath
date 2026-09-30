using Microsoft.EntityFrameworkCore;
using ProofPath.Application.Files;
using ProofPath.Application.Resumes;
using ProofPath.Domain.Entities;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Infrastructure.Resumes;

public sealed class PrivateFileCleanup(ProofPathDbContext database, IPrivateFileStore files) : IPrivateFileCleanup
{
    public async Task RunAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        var abandoned = await database.Resumes.AsNoTracking().Where(item =>
            (item.Status == "Uploading" || item.Status == "UploadFailed") && item.CreatedAt < cutoff).Take(20).ToListAsync(ct);
        foreach (var resume in abandoned)
        {
            try
            {
                await files.DeleteAsync(resume.StorageKey, ct);
                await database.Resumes.Where(item => item.Id == resume.Id && (item.Status == "Uploading" || item.Status == "UploadFailed"))
                    .ExecuteUpdateAsync(set => set.SetProperty(item => item.Status, "UploadFailedCleaned"), ct);
            }
            catch (OperationCanceledException) { throw; }
            catch { /* key remains discoverable in failed upload metadata */ }
        }
        var now = DateTime.UtcNow;
        var deletions = await database.PrivateFileDeletions.AsNoTracking().Where(item => item.NextAttemptAt <= now).Take(20).ToListAsync(ct);
        foreach (var deletion in deletions)
        {
            try
            {
                await files.DeleteAsync(deletion.StorageKey, ct);
                await database.PrivateFileDeletions.Where(item => item.Id == deletion.Id).ExecuteDeleteAsync(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                var next = now.AddMinutes(Math.Min(60, Math.Pow(2, Math.Min(6, deletion.Attempts))));
                await database.PrivateFileDeletions.Where(item => item.Id == deletion.Id).ExecuteUpdateAsync(set =>
                    set.SetProperty(item => item.Attempts, item => item.Attempts + 1).SetProperty(item => item.NextAttemptAt, next), ct);
            }
        }
    }
}
