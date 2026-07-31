namespace Application.Interfaces;

public interface IProjectTaskAttachmentStorage
{
    Task SaveAsync(Stream content, string storedFileName, CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string storedFileName, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storedFileName, CancellationToken cancellationToken = default);
}