using Amazon.S3;
using Amazon.S3.Model;
using Application.Modules.ProjectTasks.Attachments;
using Microsoft.Extensions.Options;
using Shared.Settings;

namespace Infrastructure.Services;

/// <summary>Stores private attachment objects in AWS S3 or an S3-compatible service.</summary>
public sealed class S3ProjectTaskAttachmentStorage : IProjectTaskAttachmentStorage, IProjectTaskAttachmentStorageInventory
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.Ordinal)
    {
        ".pdf",
        ".png",
        ".jpg",
        ".jpeg",
        ".docx",
        ".xlsx",
        ".txt"
    };

    private readonly IAmazonS3 _client;
    private readonly string _bucketName;

    public S3ProjectTaskAttachmentStorage(IAmazonS3 client, IOptions<AttachmentSettings> settings)
    {
        _client = client;
        _bucketName = settings.Value.S3BucketName;
    }

    public async Task SaveAsync(Stream content, string storedFileName, CancellationToken cancellationToken = default)
    {
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = ValidateKey(storedFileName),
            InputStream = content,
            AutoCloseStream = false
        }, cancellationToken);
    }

    public async Task<Stream?> OpenReadAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _client.GetObjectAsync(
                _bucketName,
                ValidateKey(storedFileName),
                cancellationToken);
            var content = new MemoryStream();
            await response.ResponseStream.CopyToAsync(content, cancellationToken);
            content.Position = 0;
            return content;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task DeleteAsync(string storedFileName, CancellationToken cancellationToken = default)
        => _client.DeleteObjectAsync(_bucketName, ValidateKey(storedFileName), cancellationToken);

    public async IAsyncEnumerable<string> EnumerateStoredFileNamesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? continuationToken = null;
        do
        {
            var response = await _client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucketName,
                ContinuationToken = continuationToken
            }, cancellationToken);

            foreach (var item in response.S3Objects)
            {
                if (IsGeneratedFileName(item.Key))
                {
                    yield return item.Key;
                }
            }

            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (continuationToken is not null);
    }

    private static string ValidateKey(string storedFileName)
    {
        if (!IsGeneratedFileName(storedFileName))
        {
            throw new ArgumentException("Invalid stored file name", nameof(storedFileName));
        }

        return storedFileName;
    }

    private static bool IsGeneratedFileName(string storedFileName)
    {
        var extension = Path.GetExtension(storedFileName);
        var identifier = Path.GetFileNameWithoutExtension(storedFileName);
        return !string.IsNullOrWhiteSpace(storedFileName)
            && Path.GetFileName(storedFileName) == storedFileName
            && Guid.TryParseExact(identifier, "N", out _)
            && AllowedExtensions.Contains(extension);
    }
}