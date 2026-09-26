using Application.Modules.StarterHealth;
using Infrastructure.Dispatching;
using Infrastructure.Modules.StarterHealth;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests.Architecture;

public sealed class StarterArchitectureTests
{
    [Fact]
    public void Every_application_request_has_exactly_one_infrastructure_handler()
    {
        var requestTypes = typeof(GetStarterHealthQuery).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract
                && type.IsClass
                && type.GetInterfaces().Any(interfaceType => IsClosedGenericOf(
                    interfaceType,
                    typeof(IRequest<>))))
            .ToArray();

        Assert.NotEmpty(requestTypes);

        foreach (var requestType in requestTypes)
        {
            var requestInterface = requestType.GetInterfaces()
                .Single(interfaceType => IsClosedGenericOf(interfaceType, typeof(IRequest<>)));
            var responseType = requestInterface.GetGenericArguments()[0];
            var handlerContract = typeof(IRequestHandler<,>)
                .MakeGenericType(requestType, responseType);
            var handlers = typeof(GetStarterHealthHandler).Assembly
                .GetTypes()
                .Where(type => !type.IsAbstract && handlerContract.IsAssignableFrom(type))
                .ToArray();

            Assert.Single(handlers);
        }
    }

    [Fact]
    public void Composition_root_registers_sender_and_all_handlers()
    {
        var services = new ServiceCollection();
        services.AddApplicationDispatch();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISender>());

        var requestTypes = typeof(GetStarterHealthQuery).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract
                && type.IsClass
                && type.GetInterfaces().Any(interfaceType => IsClosedGenericOf(
                    interfaceType,
                    typeof(IRequest<>))))
            .ToArray();

        foreach (var requestType in requestTypes)
        {
            var requestInterface = requestType.GetInterfaces()
                .Single(interfaceType => IsClosedGenericOf(interfaceType, typeof(IRequest<>)));
            var handlerContract = typeof(IRequestHandler<,>)
                .MakeGenericType(requestType, requestInterface.GetGenericArguments()[0]);

            Assert.NotNull(scope.ServiceProvider.GetRequiredService(handlerContract));
        }
    }

    [Fact]
    public void Module_controllers_dispatch_through_ISender()
    {
        var controllers = typeof(Program).Assembly.ExportedTypes
            .Where(type => !type.IsAbstract
                && typeof(ControllerBase).IsAssignableFrom(type)
                && type.Namespace?.StartsWith("API.Modules.", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.NotEmpty(controllers);

        foreach (var controller in controllers)
        {
            var constructor = controller.GetConstructors().Single();

            Assert.Contains(
                constructor.GetParameters(),
                parameter => parameter.ParameterType == typeof(ISender));
        }
    }

    [Fact]
    public void Domain_does_not_reference_MediatR()
    {
        var mediatRAssemblyName = typeof(IRequest).Assembly.GetName().Name;

        Assert.DoesNotContain(
            typeof(global::Domain.AssemblyMarker).Assembly.GetReferencedAssemblies(),
            reference => reference.Name == mediatRAssemblyName);
    }

    private static bool IsClosedGenericOf(Type type, Type genericTypeDefinition)
        => type.IsGenericType
            && type.GetGenericTypeDefinition() == genericTypeDefinition;
}
