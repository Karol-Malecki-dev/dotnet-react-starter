using Application.Features.Projects;
using Application.Modules.Projects.GetProjectDetails;
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
    public void Application_dispatch_registers_the_query_handler_and_sender()
    {
        var services = new ServiceCollection();
        services.AddApplicationDispatch();
        services.AddSingleton<IGetProjectDetailsStore>(new Mock<IGetProjectDetailsStore>().Object);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISender>());
        Assert.NotNull(scope.ServiceProvider
            .GetRequiredService<IRequestHandler<GetProjectDetailsQuery, ProjectOperationResult<ProjectView>>>());
    }

    [Fact]
    public void Get_project_details_controller_dispatches_through_ISender()
    {
        var constructor = typeof(GetProjectDetailsController)
            .GetConstructors()
            .Single();

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
