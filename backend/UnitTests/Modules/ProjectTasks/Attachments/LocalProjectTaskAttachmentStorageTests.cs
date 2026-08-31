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
            var storedFileName = "attachment.bin";
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
}
