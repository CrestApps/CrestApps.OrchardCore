using CrestApps.OrchardCore.SignalR.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.SignalR.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSignalRServices(this IServiceCollection services)
    {
        services
            .AddScoped<HubLinkGenerator>();

        return services;
    }
}
