using Application.Modules.Projects.CreateProject;
using Domain.Entities;
using Infrastructure.Data;

namespace Infrastructure.Modules.Projects.CreateProject;

/// <summary>
/// EF Core persistence adapter for the create-project slice.
/// </summary>
public sealed class EfCreateProjectStore : ICreateProjectStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfCreateProjectStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void AddProject(Project project)
        => _dbContext.Projects.Add(project);

    public void AddActivity(ProjectActivity activity)
        => _dbContext.ProjectActivities.Add(activity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
