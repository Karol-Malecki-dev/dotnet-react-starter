using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.AssignmentNotifications;
using Application.Modules.ProjectTasks.CreateProjectTask;
using Application.Modules.Projects.GetProjectDetails;
using API.Modules.ProjectTasks.CreateProjectTask;
using API.Modules.Projects.GetProjectDetails;
using Infrastructure.Dispatching;
using Infrastructure.Modules.Projects.GetProjectDetails;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace UnitTests.Architecture;

public sealed class MediatRArchitectureTests
{
    [Fact]
    public void Every_application_request_has_exactly_one_infrastructure_handler()
    {
        var requestTypes = typeof(GetProjectDetailsQuery).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract
                && type.IsClass
                && type.GetInterfaces().Any(interfaceType => IsClosedGenericOf(interfaceType, typeof(IRequest<>))))
            .ToArray();

        Assert.NotEmpty(requestTypes);

        foreach (var requestType in requestTypes)
        {
            var requestInterface = requestType.GetInterfaces()
                .Single(interfaceType => IsClosedGenericOf(interfaceType, typeof(IRequest<>)));
            var responseType = requestInterface.GetGenericArguments()[0];
            var handlerInterface = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
            var handlers = typeof(GetProjectDetailsHandler).Assembly
                .GetTypes()
                .Where(type => !type.IsAbstract && handlerInterface.IsAssignableFrom(type))
                .ToArray();

            Assert.Single(handlers);
        }
    }

    [Fact]
    public void Application_dispatch_registers_migrated_handlers_sender_and_telemetry()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddApplicationDispatch();
        services.AddSingleton<IGetProjectDetailsStore>(new Mock<IGetProjectDetailsStore>().Object);
        services.AddSingleton<IProjectTaskAccess>(new Mock<IProjectTaskAccess>().Object);
        services.AddSingleton<IProjectTaskCommandStore>(new Mock<IProjectTaskCommandStore>().Object);
        services.AddSingleton<IProjectTaskAssignmentNotificationWriter>(
            new Mock<IProjectTaskAssignmentNotificationWriter>().Object);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISender>());
        Assert.NotNull(scope.ServiceProvider
            .GetRequiredService<IRequestHandler<GetProjectDetailsQuery, ProjectOperationResult<ProjectView>>>());
        Assert.NotNull(scope.ServiceProvider
            .GetRequiredService<IRequestHandler<CreateProjectTaskCommand, ProjectOperationResult<ProjectTaskView>>>());
        Assert.Contains(
            scope.ServiceProvider.GetServices<IPipelineBehavior<GetProjectDetailsQuery, ProjectOperationResult<ProjectView>>>(),
            behavior => behavior.GetType().IsGenericType
                && behavior.GetType().GetGenericTypeDefinition() == typeof(MediatRTelemetryBehavior<,>));
    }

    [Fact]
    public void Get_project_details_controller_dispatches_through_ISender()
    {
        AssertControllerUsesSender(typeof(GetProjectDetailsController));
    }

    [Fact]
    public void Create_project_task_controller_dispatches_through_ISender()
    {
        AssertControllerUsesSender(typeof(CreateProjectTaskController));
    }

    private static void AssertControllerUsesSender(Type controllerType)
    {
        var constructor = controllerType.GetConstructors().Single();
        Assert.Contains(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(ISender));
    }

    [Fact]
    public void Domain_does_not_reference_MediatR()
    {
        var mediatRAssemblyName = typeof(IRequest).Assembly.GetName().Name;

        Assert.DoesNotContain(
            typeof(global::Domain.Entities.Project).Assembly.GetReferencedAssemblies(),
            reference => reference.Name == mediatRAssemblyName);
    }

    private static bool IsClosedGenericOf(Type type, Type genericTypeDefinition)
        => type.IsGenericType
            && type.GetGenericTypeDefinition() == genericTypeDefinition;
}
