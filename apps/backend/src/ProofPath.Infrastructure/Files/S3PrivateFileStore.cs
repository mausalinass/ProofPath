using System.Net;
using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using ProofPath.Application.Files;

namespace ProofPath.Infrastructure.Files;

public sealed class S3PrivateFileStore(IAmazonS3 client, string bucket) : IPrivateFileStore
{
    private static string ObjectKey(string key)
    {
        if (key.Length != 32 || !Guid.TryParseExact(key, "N", out _)) throw new ArgumentException("Invalid private file key.");
        return "resumes/" + key;
    }
    private async Task VerifyPrivateBucket(CancellationToken ct)
    {
        var response = await client.GetPublicAccessBlockAsync(new GetPublicAccessBlockRequest { BucketName = bucket }, ct);
        var policy = response.PublicAccessBlockConfiguration;
        if (policy is null || policy.BlockPublicAcls != true || policy.BlockPublicPolicy != true ||
            policy.IgnorePublicAcls != true || policy.RestrictPublicBuckets != true)
            throw new InvalidOperationException("S3 bucket must block all public access.");
    }
    public Task<StoredPrivateFile> PutAsync(Stream content, CancellationToken ct) => PutAsync(Guid.NewGuid().ToString("N"), content, ct);
    public async Task<StoredPrivateFile> PutAsync(string key, Stream content, CancellationToken ct)
    {
        var objectKey = ObjectKey(key); using var bytes = new MemoryStream(); var buffer = new byte[32768]; int count;
        while ((count = await content.ReadAsync(buffer, ct)) > 0)
        {
            if (bytes.Length + count > ResumeLimits.MaxBytes) throw new PrivateFileTooLargeException();
            bytes.Write(buffer, 0, count);
        }
        await VerifyPrivateBucket(ct); var hash = Convert.ToHexString(SHA256.HashData(bytes.ToArray())); bytes.Position = 0;
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = objectKey,
            InputStream = bytes,
            ContentType = "application/octet-stream",
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
            AutoCloseStream = false,
            IfNoneMatch = "*"
        }, ct);
        return new StoredPrivateFile(key, bytes.Length, hash);
    }
    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        var objectKey = ObjectKey(key); await VerifyPrivateBucket(ct);
        try
        {
            using var response = await client.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = objectKey }, ct);
            var bytes = new MemoryStream();
            try
            {
                var buffer = new byte[32768]; int count;
                while ((count = await response.ResponseStream.ReadAsync(buffer, ct)) > 0)
                {
                    if (bytes.Length + count > ResumeLimits.MaxBytes) throw new PrivateFileTooLargeException();
                    bytes.Write(buffer, 0, count);
                }
                bytes.Position = 0; return bytes;
            }
            catch { bytes.Dispose(); throw; }
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound) { return null; }
    }
    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        var objectKey = ObjectKey(key);
        string? keyMarker = null, versionMarker = null;
        do
        {
            var page = await client.ListVersionsAsync(new ListVersionsRequest
            {
                BucketName = bucket,
                Prefix = objectKey,
                KeyMarker = keyMarker,
                VersionIdMarker = versionMarker
            }, ct);
            foreach (var version in (page.Versions ?? []).Where(version => version.Key == objectKey))
                await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucket, Key = objectKey, VersionId = version.VersionId }, ct);
            if (page.IsTruncated != true) break;
            if (page.NextKeyMarker == keyMarker && page.NextVersionIdMarker == versionMarker) throw new IOException("Invalid storage pagination.");
            keyMarker = page.NextKeyMarker; versionMarker = page.NextVersionIdMarker;
        } while (true);
        // A non-versioned bucket returns its null version in ListVersions. No unversioned DELETE here:
        // that would create a fresh delete marker on versioned buckets after the hard deletion.
    }
}
