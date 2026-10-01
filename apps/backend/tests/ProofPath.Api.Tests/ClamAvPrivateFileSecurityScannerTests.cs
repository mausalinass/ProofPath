using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using ProofPath.Application.FileSecurity;
using ProofPath.Infrastructure.Files;

namespace ProofPath.Api.Tests;

public sealed class ClamAvPrivateFileSecurityScannerTests
{
    [Theory]
    [InlineData("stream: OK\0", false)]
    [InlineData("stream: Eicar-Signature FOUND\0", true)]
    public async Task StreamsContentAndHonorsClamAvVerdict(string reply, bool infected)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var command = new byte[10];
            await stream.ReadExactlyAsync(command);
            Assert.Equal("zINSTREAM\0", Encoding.ASCII.GetString(command));
            var header = new byte[4];
            var received = new MemoryStream();
            while (true)
            {
                await stream.ReadExactlyAsync(header);
                var length = BinaryPrimitives.ReadUInt32BigEndian(header);
                if (length == 0) break;
                var chunk = new byte[length];
                await stream.ReadExactlyAsync(chunk);
                await received.WriteAsync(chunk);
            }
            Assert.Equal("resume", Encoding.UTF8.GetString(received.ToArray()));
            await stream.WriteAsync(Encoding.UTF8.GetBytes(reply));
        });
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileSecurity:ClamAvHost"] = "127.0.0.1",
                ["FileSecurity:ClamAvPort"] = port.ToString(),
                ["FileSecurity:TimeoutSeconds"] = "5"
            }).Build();
            var scanner = new ClamAvPrivateFileSecurityScanner(configuration);
            if (infected)
                await Assert.ThrowsAsync<UnsafePrivateFileException>(() => scanner.ScanAsync("resume"u8.ToArray(), default));
            else
                await scanner.ScanAsync("resume"u8.ToArray(), default);
            await server;
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task FailsClosedWhenScannerIsUnavailable()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FileSecurity:ClamAvHost"] = "127.0.0.1",
            ["FileSecurity:ClamAvPort"] = "1",
            ["FileSecurity:TimeoutSeconds"] = "1"
        }).Build();
        await Assert.ThrowsAsync<PrivateFileSecurityUnavailableException>(() =>
            new ClamAvPrivateFileSecurityScanner(configuration).ScanAsync("resume"u8.ToArray(), default));
    }
}
