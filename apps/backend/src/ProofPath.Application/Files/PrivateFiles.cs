namespace ProofPath.Application.Files;

public static class ResumeLimits
{
    public const long MaxBytes = 10 * 1024 * 1024;
}

public sealed record StoredPrivateFile(string Key, long Length, string Sha256);
public interface IPrivateFileStore
{
    Task<StoredPrivateFile> PutAsync(Stream content, CancellationToken ct);
    Task<StoredPrivateFile> PutAsync(string key, Stream content, CancellationToken ct);
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}

public sealed class PrivateFileTooLargeException() : Exception("Resume exceeds the 10 MB limit.");
