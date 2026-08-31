using Application.Modules.Notifications.GetUnreadCount;
using Application.Modules.Projects.GetProjectDashboard;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace IntegrationTests;

public sealed class ModuleArchitectureIntegrationTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory = new();

    [Fact]
    public void Every_module_handler_is_registered_in_dependency_injection()
    {
        using var scope = _factory.Services.CreateScope();
        var applicationAssembly = typeof(IGetProjectDashboardHandler).Assembly;
        var handlerContracts = applicationAssembly.ExportedTypes
            .Where(type => type.IsInterface
                && type.Name.EndsWith("Handler", StringComparison.Ordinal)
                && (type.Namespace?.StartsWith(
                        "Application.Modules.Projects",
                        StringComparison.Ordinal) == true
                    || type.Namespace?.StartsWith(
                        "Application.Modules.ProjectTasks",
                        StringComparison.Ordinal) == true
                    || type.Namespace?.StartsWith(
                        "Application.Modules.Notifications",
                        StringComparison.Ordinal) == true))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();
        var failures = new List<string>();

        foreach (var contract in handlerContracts)
        {
            try
            {
                _ = scope.ServiceProvider.GetRequiredService(contract);
            }
            catch (Exception exception)
            {
                failures.Add($"{contract.FullName}: {exception.GetBaseException().Message}");
            }
        }

        Assert.NotEmpty(handlerContracts);
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
}
