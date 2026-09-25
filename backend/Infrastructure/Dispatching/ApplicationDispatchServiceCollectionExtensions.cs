using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Dispatching;

/// <summary>
/// Registers the single in-process dispatcher for backend command and query handlers.
/// </summary>
public static class ApplicationDispatchServiceCollectionExtensions
{
    /// <summary>
    /// Registers MediatR handlers from the known Infrastructure handler assembly.
    /// </summary>
    public static IServiceCollection AddApplicationDispatch(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(
                typeof(ApplicationDispatchServiceCollectionExtensions).Assembly));

        return services;
    }
}
