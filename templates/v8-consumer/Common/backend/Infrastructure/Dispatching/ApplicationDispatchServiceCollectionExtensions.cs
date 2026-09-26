using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Dispatching;

public static class ApplicationDispatchServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationDispatch(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(ApplicationDispatchServiceCollectionExtensions).Assembly));

        return services;
    }
}
