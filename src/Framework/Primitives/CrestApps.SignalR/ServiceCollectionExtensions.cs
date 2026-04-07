using CrestApps.SignalR.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.SignalR;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds CrestApps SignalR services including hub route management.
    /// </summary>
    public static IServiceCollection AddCrestAppsSignalR(this IServiceCollection services, string pathPrefix = "")
    {
        services.AddSingleton(new HubRouteManager(pathPrefix));
        services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            });

        return services;
    }
}
