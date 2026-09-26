using Application.Modules.Notifications.GetUnreadCount;
using Application.Modules.Projects.GetProjectDashboard;
using Application.Modules.Projects.GetProjectDetails;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using System.Reflection;

namespace IntegrationTests;

public sealed class ModuleArchitectureIntegrationTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory = new();

    [Fact]
    public void Every_module_request_handler_is_registered_in_dependency_injection()
    {
        using var scope = _factory.Services.CreateScope();
        var applicationAssembly = typeof(GetProjectDashboardQuery).Assembly;
        var requestTypes = applicationAssembly.ExportedTypes
            .Where(type => !type.IsAbstract
                && type.IsClass
                && IsModuleType(type)
                && type.GetInterfaces().Any(interfaceType => IsClosedGenericOf(
                    interfaceType,
                    typeof(IRequest<>))))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();
        var failures = new List<string>();

        foreach (var requestType in requestTypes)
        {
            var requestInterface = requestType.GetInterfaces()
                .Single(interfaceType => IsClosedGenericOf(interfaceType, typeof(IRequest<>)));
            var responseType = requestInterface.GetGenericArguments()[0];
            var handlerContract = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
            var handlers = scope.ServiceProvider.GetServices(handlerContract).ToArray();

            if (handlers.Length != 1)
            {
                failures.Add($"{handlerContract.FullName}: expected one registration, found {handlers.Length}");
            }
        }

        Assert.NotEmpty(requestTypes);
        Assert.Empty(failures);
    }

    [Fact]
    public void Attribute_routed_endpoints_do_not_duplicate_http_method_and_route()
    {
        var descriptorProvider = _factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>();
        var endpoints = descriptorProvider.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .SelectMany(ToEndpointKeys)
            .ToList();
        var duplicates = endpoints
            .GroupBy(endpoint => endpoint.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(item => item.Action))}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void Module_controllers_and_handlers_do_not_depend_directly_on_application_db_context()
    {
        var apiAssembly = typeof(Program).Assembly;
        var infrastructureAssembly = typeof(Infrastructure.Modules.Projects.ProjectsModule).Assembly;
        var moduleControllers = apiAssembly.ExportedTypes
            .Where(type => !type.IsAbstract
                && typeof(ControllerBase).IsAssignableFrom(type)
                && type.Namespace?.StartsWith("API.Modules.", StringComparison.Ordinal) == true);
        var moduleHandlers = infrastructureAssembly.ExportedTypes
            .Where(type => !type.IsAbstract
                && type.Name.EndsWith("Handler", StringComparison.Ordinal)
                && type.Namespace?.StartsWith("Infrastructure.Modules.", StringComparison.Ordinal) == true);
        var violations = moduleControllers
            .Concat(moduleHandlers)
            .Where(DependsDirectlyOnDbContext)
            .Select(type => type.FullName!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(violations);
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private static IEnumerable<(string Key, string Action)> ToEndpointKeys(
        ControllerActionDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.AttributeRouteInfo?.Template))
        {
            return [];
        }

        var methods = descriptor.ActionConstraints?
            .OfType<HttpMethodActionConstraint>()
            .SelectMany(constraint => constraint.HttpMethods)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (methods is null || methods.Count == 0)
        {
            methods = ["*"];
        }

        var route = descriptor.AttributeRouteInfo.Template.Trim('/');
        var action = $"{descriptor.ControllerTypeInfo.FullName}.{descriptor.ActionName}";
        return methods.Select(method => ($"{method.ToUpperInvariant()} {route}", action));
    }

    private static bool DependsDirectlyOnDbContext(Type type)
    {
        var constructorDependency = type
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(ApplicationDbContext));
        var fieldDependency = type
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Any(field => field.FieldType == typeof(ApplicationDbContext));

        return constructorDependency || fieldDependency;
    }

    private static bool IsModuleType(Type type)
        => type.Namespace?.StartsWith("Application.Modules.Projects", StringComparison.Ordinal) == true
            || type.Namespace?.StartsWith("Application.Modules.ProjectTasks", StringComparison.Ordinal) == true
            || type.Namespace?.StartsWith("Application.Modules.Notifications", StringComparison.Ordinal) == true;

    private static bool IsClosedGenericOf(Type type, Type genericTypeDefinition)
        => type.IsGenericType
            && type.GetGenericTypeDefinition() == genericTypeDefinition;
}
