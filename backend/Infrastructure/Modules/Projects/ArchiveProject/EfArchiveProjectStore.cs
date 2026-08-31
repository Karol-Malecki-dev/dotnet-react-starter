using Application.Modules.Projects.ArchiveProject;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.ArchiveProject;

/// <summary>
/// EF Core persistence adapter for the archive-project slice.
/// </summary>
public sealed class EfArchiveProjectStore : IArchiveProjectStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfArchiveProjectStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Project?> GetOwnedProjectAsync(
        Guid ownerId,
        Guid projectId,
        CancellationToken cancellationToken = default)
        => _dbContext.Projects
            .FirstOrDefaultAsync(
                project => project.Id == projectId && project.OwnerId == ownerId,
                cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public void ClearChangeTracker()
        => _dbContext.ChangeTracker.Clear();
}
