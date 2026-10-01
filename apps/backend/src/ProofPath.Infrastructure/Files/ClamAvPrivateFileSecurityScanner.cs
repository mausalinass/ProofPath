using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using ProofPath.Application.FileSecurity;

namespace ProofPath.Infrastructure.Files;

public sealed class ClamAvPrivateFileSecurityScanner(IConfiguration configuration) : IPrivateFileSecurityScanner
{
    private const int ChunkSize = 64 * 1024;
    private readonly string host = configuration["FileSecurity:ClamAvHost"]
        ?? throw new InvalidOperationException("Configure FileSecurity:ClamAvHost.");
    private readonly int port = configuration.GetValue("FileSecurity:ClamAvPort", 3310);
    private readonly TimeSpan timeout = TimeSpan.FromSeconds(configuration.GetValue("FileSecurity:TimeoutSeconds", 30));

    public async Task ScanAsync(ReadOnlyMemory<byte> content, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, deadline.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync("zINSTREAM\0"u8.ToArray(), deadline.Token);

            var header = new byte[4];
            for (var offset = 0; offset < content.Length; offset += ChunkSize)
            {
                var length = Math.Min(ChunkSize, content.Length - offset);
                BinaryPrimitives.WriteUInt32BigEndian(header, checked((uint)length));
                await stream.WriteAsync(header, deadline.Token);
                await stream.WriteAsync(content.Slice(offset, length), deadline.Token);
            }
            Array.Clear(header);
            await stream.WriteAsync(header, deadline.Token);
            await stream.FlushAsync(deadline.Token);

            var responseBuffer = new byte[4096];
            var bytesRead = await stream.ReadAsync(responseBuffer, deadline.Token);
            var response = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead).TrimEnd('\0', '\r', '\n');
            if (response.EndsWith("OK", StringComparison.OrdinalIgnoreCase)) return;
            if (response.Contains("FOUND", StringComparison.OrdinalIgnoreCase))
            {
                var marker = response.LastIndexOf(" FOUND", StringComparison.OrdinalIgnoreCase);
                var separator = response.IndexOf(':');
                var signature = separator >= 0 && marker > separator ? response[(separator + 1)..marker].Trim() : "detected";
                throw new UnsafePrivateFileException(signature);
            }
            throw new PrivateFileSecurityUnavailableException($"ClamAV returned an unexpected response: {response}");
        }
        catch (UnsafePrivateFileException) { throw; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new PrivateFileSecurityUnavailableException("Malware scanning timed out.");
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            throw new PrivateFileSecurityUnavailableException("Malware scanning is unavailable.", exception);
        }
    }
}

public sealed class DevelopmentPrivateFileSecurityScanner : IPrivateFileSecurityScanner
{
    public Task ScanAsync(ReadOnlyMemory<byte> content, CancellationToken ct) => Task.CompletedTask;
}
