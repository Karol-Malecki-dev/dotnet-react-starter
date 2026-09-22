using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Infrastructure.Data;

namespace IntegrationTests;

public sealed record V6BenchmarkFixture(
    Guid OwnerId,
    Guid ProjectId,
    int TaskCount,
    int NoiseProjectCount,
    int LabelCount,
    int ActivityCount);

public static class V6BenchmarkFixtureSeeder
{
    public const int DefaultTaskCount = 1_000;
    public const int DefaultNoiseProjectCount = 20;
    public const int LabelsPerTask = 2;
    private const int ActivityEveryNthTask = 5;

    public static async Task<V6BenchmarkFixture> SeedAsync(
        ApplicationDbContext dbContext,
        int taskCount = DefaultTaskCount,
        int noiseProjectCount = DefaultNoiseProjectCount)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        if (taskCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(taskCount), "Task count must be positive.");
        }

        if (noiseProjectCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(noiseProjectCount), "Noise project count cannot be negative.");
        }

        var ownerId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;
        var owner = User.Create(
            EmailAddress.Create($"v6-query-plan-owner-{ownerId:N}@example.com"),
            DisplayName.Create("V6 Query Plan Owner"),
            UserRole.User,
            isActive: true,
            isEmailConfirmed: true,
            id: ownerId,
            createdAt: DateTime.UtcNow);
        var benchmarkProject = Project.Create(ownerId, $"V6 benchmark {Guid.NewGuid():N}");
        var noiseProjects = Enumerable
            .Range(0, noiseProjectCount)
            .Select(index => Project.Create(ownerId, $"V6 noise project {index} {Guid.NewGuid():N}"))
            .ToList();

        dbContext.Users.Add(owner);
        dbContext.Projects.Add(benchmarkProject);
        dbContext.Projects.AddRange(noiseProjects);

        var tasks = new List<ProjectTask>(taskCount);
        for (var index = 0; index < taskCount; index++)
        {
            var dueDate = (index % 5) switch
            {
                0 => (DateTime?)today.AddDays(-(index % 31)),
                1 => today.AddDays(index % 8),
                _ => null
            };
            var task = ProjectTask.Create(
                benchmarkProject.Id,
                $"V6 benchmark task {index:D5}",
                "Synthetic V6 query-plan fixture task.",
                (ProjectTaskPriority)(index % 3 + 1),
                dueDate,
                assignedUserId: null,
                createdByUserId: ownerId,
                labels: new[] { $"bucket-{index % 10}", index % 2 == 0 ? "even" : "odd" });

            if (index % 7 == 0)
            {
                task.ChangeStatus(ProjectTaskStatus.Done);
            }
            else if (index % 3 == 0)
            {
                task.ChangeStatus(ProjectTaskStatus.InProgress);
            }

            tasks.Add(task);
        }

        dbContext.ProjectTasks.AddRange(tasks);
        dbContext.ProjectActivities.AddRange(
            tasks
                .Where((_, index) => index % ActivityEveryNthTask == 0)
                .Select(task => new ProjectActivity
                {
                    ProjectId = benchmarkProject.Id,
                    ActorUserId = ownerId,
                    ProjectTaskId = task.Id,
                    Type = "TaskUpdated",
                    Description = "Synthetic V6 query-plan fixture activity.",
                    CreatedAt = DateTime.UtcNow
                }));

        await dbContext.SaveChangesAsync();

        return new V6BenchmarkFixture(
            ownerId,
            benchmarkProject.Id,
            taskCount,
            noiseProjectCount,
            taskCount * LabelsPerTask,
            (taskCount + ActivityEveryNthTask - 1) / ActivityEveryNthTask);
    }
}
