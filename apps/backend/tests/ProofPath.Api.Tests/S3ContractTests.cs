using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using ProofPath.Infrastructure.Files;

namespace ProofPath.Api.Tests;

public sealed class S3ContractTests
{
    private sealed class FakeS3() : AmazonS3Client(new BasicAWSCredentials("fixture", "fixture"), RegionEndpoint.USEast1)
    {
        public bool Private { get; set; } = true;
        public PutObjectRequest? Put { get; private set; }
        public List<DeleteObjectRequest> Deleted { get; } = [];
        public override Task<GetPublicAccessBlockResponse> GetPublicAccessBlockAsync(GetPublicAccessBlockRequest request, CancellationToken ct = default) => Task.FromResult(new GetPublicAccessBlockResponse
        { PublicAccessBlockConfiguration = new PublicAccessBlockConfiguration { BlockPublicAcls = Private, BlockPublicPolicy = Private, IgnorePublicAcls = Private, RestrictPublicBuckets = Private } });
        public override Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken ct = default)
        { Put = request; return Task.FromResult(new PutObjectResponse()); }
        public override Task<ListVersionsResponse> ListVersionsAsync(ListVersionsRequest request, CancellationToken ct = default) => Task.FromResult(new ListVersionsResponse
        {
            IsTruncated = request.KeyMarker is null,
            NextKeyMarker = request.Prefix, NextVersionIdMarker = "v1",
            Versions = request.KeyMarker is null ? [new S3ObjectVersion { Key = request.Prefix, VersionId = "v1" }, new S3ObjectVersion { Key = request.Prefix + "other", VersionId = "foreign" }]
                : [new S3ObjectVersion { Key = request.Prefix, VersionId = "marker", IsDeleteMarker = true }]
        });
        public override Task<DeleteObjectResponse> DeleteObjectAsync(DeleteObjectRequest request, CancellationToken ct = default)
        { Deleted.Add(request); return Task.FromResult(new DeleteObjectResponse()); }
    }
    [Fact] public async Task UploadRequiresPrivateBucketAndUsesEncryptionOpaqueKeyAndCreateOnly()
    {
        using var s3 = new FakeS3(); var store = new S3PrivateFileStore(s3, "fixture-private-bucket");
        var result = await store.PutAsync(new MemoryStream([1, 2, 3]), default);
        Assert.Equal("resumes/" + result.Key, s3.Put!.Key); Assert.Equal("*", s3.Put.IfNoneMatch);
        Assert.Equal(ServerSideEncryptionMethod.AES256, s3.Put.ServerSideEncryptionMethod);
        s3.Private = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PutAsync(new MemoryStream([1]), default));
    }
    [Fact] public async Task DeletionIncludesAllVersionsAndMarkersButNeverPrefixNeighbors()
    {
        using var s3 = new FakeS3(); var store = new S3PrivateFileStore(s3, "fixture-private-bucket");
        var key = Guid.NewGuid().ToString("N"); await store.DeleteAsync(key, default);
        Assert.Equal(2, s3.Deleted.Count); Assert.All(s3.Deleted, item => Assert.Equal("resumes/" + key, item.Key));
        Assert.Equal(new[] { "v1", "marker" }, s3.Deleted.Select(item => item.VersionId));
    }
}
