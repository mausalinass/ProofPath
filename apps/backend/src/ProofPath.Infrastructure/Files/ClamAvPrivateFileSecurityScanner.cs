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
            var received = 0;
            var terminator = -1;
            while (received < responseBuffer.Length && terminator < 0)
            {
                var read = await stream.ReadAsync(responseBuffer.AsMemory(received), deadline.Token);
                if (read == 0) break;
                terminator = Array.IndexOf(responseBuffer, (byte)0, received, read);
                received += read;
            }
            if (terminator < 0) throw new PrivateFileSecurityUnavailableException("ClamAV did not return a complete verdict.");
            var response = Encoding.UTF8.GetString(responseBuffer, 0, terminator);
            if (response.Equals("stream: OK", StringComparison.Ordinal)) return;
            if (response.StartsWith("stream: ", StringComparison.Ordinal) && response.EndsWith(" FOUND", StringComparison.Ordinal))
                throw new UnsafePrivateFileException(response["stream: ".Length..^" FOUND".Length]);
            throw new PrivateFileSecurityUnavailableException("ClamAV returned an unexpected verdict.");
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
