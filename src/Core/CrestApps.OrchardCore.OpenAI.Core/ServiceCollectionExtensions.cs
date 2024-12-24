using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using CrestApps.OrchardCore.OpenAI.Core.Handlers;
using CrestApps.OrchardCore.OpenAI.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.OpenAI.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAIChatProfileServices(this IServiceCollection services)
    {
        services.AddScoped<IAIChatProfileManager, DefaultAIChatProfileManager>();
        services.AddScoped<IAIChatProfileHandler, AIChatProfileHandler>();
        services.AddPermissionProvider<OpenAIPermissionProvider>();

        return services;
    }

    public static IServiceCollection AddAIChatProfileSource<T>(this IServiceCollection services, string key)
         where T : class, IAIChatProfileSource
    {
        ArgumentNullException.ThrowIfNull(key);

        services.AddKeyedScoped<IAIChatProfileSource, T>(key);

        return services;
    }
}
