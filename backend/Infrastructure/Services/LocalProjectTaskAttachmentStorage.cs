using Application.Modules.ProjectTasks.Attachments;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Services;

public sealed class LocalProjectTaskAttachmentStorage : IProjectTaskAttachmentStorage
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

    private readonly string _rootPath;

    public LocalProjectTaskAttachmentStorage(IHostEnvironment environment)
    {
        _rootPath = Path.Combine(environment.ContentRootPath, "uploads", "task-attachments");
        Directory.CreateDirectory(_rootPath);
    }

    public async Task SaveAsync(Stream content, string storedFileName, CancellationToken cancellationToken = default)
    {
        await using var output = new FileStream(
            GetPath(storedFileName),
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await content.CopyToAsync(output, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        var path = GetPath(storedFileName);
        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        var path = GetPath(storedFileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetPath(string storedFileName)
    {
        var extension = Path.GetExtension(storedFileName);
        var identifier = Path.GetFileNameWithoutExtension(storedFileName);
        if (string.IsNullOrWhiteSpace(storedFileName)
            || Path.GetFileName(storedFileName) != storedFileName
            || !Guid.TryParseExact(identifier, "N", out _)
            || !AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException("Invalid stored file name", nameof(storedFileName));
        }

        return Path.Combine(_rootPath, storedFileName);
    }
}