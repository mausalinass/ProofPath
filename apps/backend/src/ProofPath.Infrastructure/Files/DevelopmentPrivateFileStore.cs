using System.Security.Cryptography;
using ProofPath.Application.Files;

namespace ProofPath.Infrastructure.Files;

// Development only. API use cases must authorize the resource before using its opaque key.
// Configure a dedicated directory outside the repository, static-file roots and synced folders.
public sealed class DevelopmentPrivateFileStore : IPrivateFileStore
{
    private readonly string root;
    public DevelopmentPrivateFileStore(string rootDirectory)
    {
        if (!Path.IsPathFullyQualified(rootDirectory)) throw new ArgumentException("Use an absolute private storage directory.");
        root = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(root);
    }

    public Task<StoredPrivateFile> PutAsync(Stream content, CancellationToken ct) => PutAsync(Guid.NewGuid().ToString("N"), content, ct);

    public async Task<StoredPrivateFile> PutAsync(string key, Stream content, CancellationToken ct)
    {
        var destination = Resolve(key); var pending = destination + ".upload";
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long length = 0;
            await using (var output = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None, 32768,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[32768]; int read;
                while ((read = await content.ReadAsync(buffer, ct)) > 0)
                {
                    length += read;
                    if (length > ResumeLimits.MaxBytes) throw new PrivateFileTooLargeException();
                    hash.AppendData(buffer, 0, read); await output.WriteAsync(buffer.AsMemory(0, read), ct);
                }
                await output.FlushAsync(ct);
            }
            ct.ThrowIfCancellationRequested();
            File.Move(pending, destination, overwrite: false);
            return new StoredPrivateFile(key, length, Convert.ToHexString(hash.GetHashAndReset()));
        }
        finally { File.Delete(pending); }
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var path = Resolve(key);
        try
        {
            return Task.FromResult<Stream?>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                32768, FileOptions.Asynchronous | FileOptions.SequentialScan));
        }
        catch (FileNotFoundException) { return Task.FromResult<Stream?>(null); }
    }
    public Task DeleteAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var path = Resolve(key); File.Delete(path); File.Delete(path + ".upload"); return Task.CompletedTask;
    }
    private string Resolve(string key)
    {
        if (key.Length != 32 || !Guid.TryParseExact(key, "N", out _)) throw new ArgumentException("Invalid private file key.");
        return Path.Combine(root, key);
    }
}
