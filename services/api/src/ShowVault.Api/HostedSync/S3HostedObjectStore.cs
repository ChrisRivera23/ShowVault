using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace ShowVault.Api.HostedSync;

public sealed class S3HostedObjectStore(
    IAmazonS3 client,
    IOptions<HostedSyncOptions> options) : IHostedObjectStore
{
    private readonly string _bucket = options.Value.S3.Bucket;

    public async Task<byte[]?> ReadAsync(
        string key,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetObjectAsync(
                new GetObjectRequest { BucketName = _bucket, Key = key },
                cancellationToken);
            if (response.ContentLength < 0 || response.ContentLength > maximumBytes)
            {
                throw new HostedSyncConflictException("A hosted object is oversized.");
            }
            await using var memory = new MemoryStream((int)response.ContentLength);
            await response.ResponseStream.CopyToAsync(memory, cancellationToken);
            if (memory.Length != response.ContentLength)
            {
                throw new HostedSyncConflictException("A hosted object is incomplete.");
            }
            return memory.ToArray();
        }
        catch (AmazonS3Exception exception) when (
            exception.StatusCode == HttpStatusCode.NotFound ||
            exception.ErrorCode is "NoSuchKey" or "NotFound")
        {
            return null;
        }
        catch (AmazonS3Exception exception)
        {
            throw Unavailable(exception);
        }
    }

    public async Task<bool> PutIfAbsentAsync(
        string key,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        try
        {
            var stream = new MemoryStream(bytes.ToArray(), writable: false);
            using (stream)
            {
                await client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = _bucket,
                    Key = key,
                    InputStream = stream,
                    AutoCloseStream = false,
                    IfNoneMatch = "*"
                }, cancellationToken);
            }
            return true;
        }
        catch (AmazonS3Exception exception) when (
            exception.StatusCode == HttpStatusCode.PreconditionFailed ||
            exception.ErrorCode is "PreconditionFailed" or "ConditionalRequestConflict")
        {
            return false;
        }
        catch (AmazonS3Exception exception)
        {
            throw Unavailable(exception);
        }
    }

    public async Task<IReadOnlyList<HostedObjectInfo>> ListAsync(
        string prefix,
        CancellationToken cancellationToken)
    {
        try
        {
            var results = new List<HostedObjectInfo>();
            string? continuationToken = null;
            do
            {
                var response = await client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = _bucket,
                    Prefix = prefix,
                    ContinuationToken = continuationToken
                }, cancellationToken);
                results.AddRange((response.S3Objects ?? []).Select(item =>
                    new HostedObjectInfo(item.Key, item.Size ?? -1)));
                if (response.IsTruncated == true &&
                    (string.IsNullOrWhiteSpace(response.NextContinuationToken) ||
                     response.NextContinuationToken == continuationToken))
                {
                    throw new HostedSyncUnavailableException(
                        "Hosted object storage returned an incomplete listing.");
                }
                continuationToken = response.IsTruncated == true
                    ? response.NextContinuationToken
                    : null;
            } while (continuationToken is not null);
            return results;
        }
        catch (AmazonS3Exception exception)
        {
            throw Unavailable(exception);
        }
    }

    public async Task CheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        try
        {
            await client.GetBucketLocationAsync(
                new GetBucketLocationRequest { BucketName = _bucket },
                cancellationToken);
        }
        catch (AmazonS3Exception exception)
        {
            throw Unavailable(exception);
        }
    }

    private static HostedSyncUnavailableException Unavailable(AmazonS3Exception exception) =>
        new($"Hosted object storage is unavailable ({exception.StatusCode}).");
}
