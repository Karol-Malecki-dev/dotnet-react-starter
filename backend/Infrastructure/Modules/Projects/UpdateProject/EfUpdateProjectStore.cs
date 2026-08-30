using Application.Modules.Projects.UpdateProject;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.UpdateProject;

/// <summary>
/// EF Core persistence adapter for the update-project slice.
/// </summary>
public sealed class EfUpdateProjectStore : IUpdateProjectStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfUpdateProjectStore(ApplicationDbContext dbContext)
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
