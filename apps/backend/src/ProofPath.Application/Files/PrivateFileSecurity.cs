using ProofPath.Application.Files;

namespace ProofPath.Application.FileSecurity;

public interface IPrivateFileSecurityScanner
{
    Task ScanAsync(ReadOnlyMemory<byte> content, CancellationToken ct);
}

public sealed class UnsafePrivateFileException(string signature)
    : Exception($"The uploaded file was rejected by malware scanning ({signature}).")
{
    public string Signature { get; } = signature;
}

public sealed class PrivateFileSecurityUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
