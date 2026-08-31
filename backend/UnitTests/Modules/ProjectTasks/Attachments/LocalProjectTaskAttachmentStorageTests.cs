using Infrastructure.Services;
using Microsoft.Extensions.Hosting;
using Moq;
using System.Text;

namespace UnitTests.Modules.ProjectTasks.Attachments;

public sealed class LocalProjectTaskAttachmentStorageTests
{
    [Fact]
    public async Task Save_open_and_delete_round_trip_uses_the_configured_content_root()
    {
        var directory = Directory.CreateTempSubdirectory("task-attachment-storage-");
        try
        {
            var environment = new Mock<IHostEnvironment>();
            environment.SetupGet(host => host.ContentRootPath).Returns(directory.FullName);
            var storage = new LocalProjectTaskAttachmentStorage(environment.Object);
            var storedFileName = $"{Guid.NewGuid():N}.txt";
            var content = Encoding.UTF8.GetBytes("attachment content");

            await using (var input = new MemoryStream(content))
            {
                await storage.SaveAsync(input, storedFileName);
            }

            var path = Path.Combine(directory.FullName, "uploads", "task-attachments", storedFileName);
            Assert.True(File.Exists(path));

            await using (var output = await storage.OpenReadAsync(storedFileName))
            {
                Assert.NotNull(output);
                using var reader = new StreamReader(output!);
                Assert.Equal("attachment content", await reader.ReadToEndAsync());
            }

            await storage.DeleteAsync(storedFileName);
            Assert.False(File.Exists(path));
            await storage.DeleteAsync(storedFileName);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
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
        var directory = Directory.CreateTempSubdirectory("task-attachment-storage-");
        try
        {
            var environment = new Mock<IHostEnvironment>();
            environment.SetupGet(host => host.ContentRootPath).Returns(directory.FullName);
            var storage = new LocalProjectTaskAttachmentStorage(environment.Object);

            await Assert.ThrowsAsync<ArgumentException>(
                () => storage.SaveAsync(new MemoryStream("content"u8.ToArray()), storedFileName));
            await Assert.ThrowsAsync<ArgumentException>(
                () => storage.OpenReadAsync(storedFileName));
            await Assert.ThrowsAsync<ArgumentException>(
                () => storage.DeleteAsync(storedFileName));
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }
}
