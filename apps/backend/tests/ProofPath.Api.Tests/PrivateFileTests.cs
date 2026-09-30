using System.Security.Cryptography;
using System.Text;
using ProofPath.Application.Files;
using ProofPath.Infrastructure.Files;

namespace ProofPath.Api.Tests;

public sealed class PrivateFileTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "proofpath-storage-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task StoresOpaqueKeysVerifiesDigestAndDeletesIdempotently()
    {
        var store = new DevelopmentPrivateFileStore(root); var bytes = Encoding.UTF8.GetBytes("Synthetic resume fixture");
        var saved = await store.PutAsync(new MemoryStream(bytes), default);
        Assert.Equal(bytes.Length, saved.Length); Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), saved.Sha256);
        Assert.True(Guid.TryParseExact(saved.Key, "N", out _));
        await using (var read = await store.OpenReadAsync(saved.Key, default))
        {
            Assert.NotNull(read); using var output = new MemoryStream(); await read.CopyToAsync(output);
            Assert.Equal(bytes, output.ToArray());
        }
        await store.DeleteAsync(saved.Key, default); await store.DeleteAsync(saved.Key, default);
        Assert.Null(await store.OpenReadAsync(saved.Key, default));
    }
    [Theory]
    [InlineData("../private")]
    [InlineData("C:\\private")]
    [InlineData("00000000000000000000000000000000/other")]
    public async Task RejectsTraversalBeforeReadingOrDeleting(string key)
    {
        var store = new DevelopmentPrivateFileStore(root);
        await Assert.ThrowsAsync<ArgumentException>(() => store.OpenReadAsync(key, default));
        await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteAsync(key, default));
    }
    [Fact]
    public async Task OversizeLeavesNoCommittedOrTemporaryFile()
    {
        var store = new DevelopmentPrivateFileStore(root);
        await Assert.ThrowsAsync<PrivateFileTooLargeException>(() => store.PutAsync(new MemoryStream(new byte[ResumeLimits.MaxBytes + 1]), default));
        Assert.Empty(Directory.EnumerateFiles(root));
    }
    [Fact]
    public async Task CancellationLeavesNoCommittedOrTemporaryFile()
    {
        var store = new DevelopmentPrivateFileStore(root); using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.PutAsync(new MemoryStream(new byte[100]), cancelled.Token));
        Assert.Empty(Directory.EnumerateFiles(root));
    }
    public void Dispose()
    {
        // Only this instance's generated, verified temporary directory is removed.
        var expectedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "proofpath-storage-tests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(root).StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
