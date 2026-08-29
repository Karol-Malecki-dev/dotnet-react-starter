namespace Application.Features.Projects;

/// <summary>
/// Represents a transaction boundary shared by multi-step project workflows.
/// </summary>
public interface IProjectTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}