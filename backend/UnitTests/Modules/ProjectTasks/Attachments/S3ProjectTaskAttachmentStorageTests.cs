using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Infrastructure.Services;
using Microsoft.Extensions.Options;
using Moq;
using Shared.Settings;

namespace UnitTests.Modules.ProjectTasks.Attachments;

public sealed class S3ProjectTaskAttachmentStorageTests
{
    private const string BucketName = "private-attachments";

    [Fact]
    public async Task Save_open_delete_and_inventory_use_the_configured_private_bucket()
    {
        var client = new Mock<IAmazonS3>();
        var storedFileName = $"{Guid.NewGuid():N}.txt";
        PutObjectRequest? putRequest = null;
        client.Setup(s3 => s3.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((request, _) => putRequest = request)
            .ReturnsAsync(new PutObjectResponse());
        client.Setup(s3 => s3.GetObjectAsync(BucketName, storedFileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse
            {
                ResponseStream = new MemoryStream("attachment content"u8.ToArray())
            });
        client.Setup(s3 => s3.DeleteObjectAsync(BucketName, storedFileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse());
        client.Setup(s3 => s3.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response
            {
                S3Objects =
                [
                    new S3Object { Key = storedFileName },
                    new S3Object { Key = "unmanaged-object" }
                ]
            });
        var storage = CreateStorage(client.Object);

        await using (var input = new MemoryStream("attachment content"u8.ToArray()))
        {
            await storage.SaveAsync(input, storedFileName);
        }

        Assert.NotNull(putRequest);
        Assert.Equal(BucketName, putRequest.BucketName);
        Assert.Equal(storedFileName, putRequest.Key);

        await using (var output = await storage.OpenReadAsync(storedFileName))
        {
            Assert.NotNull(output);
            using var reader = new StreamReader(output!);
            Assert.Equal("attachment content", await reader.ReadToEndAsync());
        }

        await storage.DeleteAsync(storedFileName);
        var inventory = new List<string>();
        await foreach (var item in storage.EnumerateStoredFileNamesAsync())
        {
            inventory.Add(item);
        }

        Assert.Equal([storedFileName], inventory);
        client.Verify(s3 => s3.DeleteObjectAsync(BucketName, storedFileName, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Open_returns_null_when_the_object_does_not_exist()
    {
        var client = new Mock<IAmazonS3>();
        var storedFileName = $"{Guid.NewGuid():N}.txt";
        client.Setup(s3 => s3.GetObjectAsync(BucketName, storedFileName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("Missing") { StatusCode = HttpStatusCode.NotFound });
        var storage = CreateStorage(client.Object);

        Assert.Null(await storage.OpenReadAsync(storedFileName));
    }

    [Theory]
    [InlineData("../00000000000000000000000000000000.txt")]
    [InlineData("..\\00000000000000000000000000000000.txt")]
    [InlineData("attachment.txt")]
    [InlineData("00000000000000000000000000000000.exe")]
    [InlineData("00000000000000000000000000000000.txt:alternate")]
    public async Task Storage_operations_reject_paths_and_names_not_generated_by_the_application(
        string storedFileName)
    {
        var storage = CreateStorage(Mock.Of<IAmazonS3>());

        await Assert.ThrowsAsync<ArgumentException>(
            () => storage.SaveAsync(new MemoryStream("content"u8.ToArray()), storedFileName));
        await Assert.ThrowsAsync<ArgumentException>(
            () => storage.OpenReadAsync(storedFileName));
        await Assert.ThrowsAsync<ArgumentException>(
            () => storage.DeleteAsync(storedFileName));
    }

    private static S3ProjectTaskAttachmentStorage CreateStorage(IAmazonS3 client)
        => new(client, Options.Create(new AttachmentSettings { S3BucketName = BucketName }));
}