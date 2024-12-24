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
        services.AddScoped<IAIChatProfileStore, DefaultAIChatProfileStore>();
        services.AddScoped<IAIChatProfileManager, DefaultAIChatProfileManager>();
        services.AddScoped<IAIChatProfileHandler, AIChatProfileHandler>();
        services.AddPermissionProvider<OpenAIPermissionProvider>();

        return services;
    }

    public static IServiceCollection AddAIChatProfileSource<TSource>(this IServiceCollection services, string sourceKey)
         where TSource : class, IAIChatProfileSource
    {
        ArgumentNullException.ThrowIfNull(sourceKey);

        services.AddScoped<TSource>();
        services.AddScoped<IAIChatProfileSource>(sp => sp.GetService<TSource>());
        services.AddKeyedScoped<IAIChatProfileSource>(sourceKey, (sp, key) => sp.GetService<TSource>());

        return services;
    }
}
