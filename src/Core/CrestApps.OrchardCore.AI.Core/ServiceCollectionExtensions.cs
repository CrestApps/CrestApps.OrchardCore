using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAIDeploymentServices(this IServiceCollection services)
    {
        services
            .AddScoped<IAIDeploymentStore, DefaultAIDeploymentStore>()
            .AddScoped<IAIDeploymentManager, DefaultAIDeploymentManager>()
            .AddScoped<IAIDeploymentHandler, AIDeploymentHandler>()
            .AddPermissionProvider<AIDeploymentProvider>();

        return services;
    }

    public static IServiceCollection AddAIChatProfileServices(this IServiceCollection services)
    {
        services
            .AddScoped<IAIChatProfileStore, DefaultAIChatProfileStore>()
            .AddScoped<IAIChatProfileManager, DefaultAIChatProfileManager>()
            .AddScoped<IAIChatProfileManagerSession, DefaultAIChatProfileManagerSession>()
            .AddScoped<IAIChatProfileHandler, AIChatProfileHandler>()
            .AddScoped<IAIChatSessionManager, DefaultAIChatSessionManager>();

        services
            .AddPermissionProvider<AIChatPermissionsProvider>()
            .AddScoped<IAuthorizationHandler, AIChatProfileAuthenticationHandler>()
            .Configure<StoreCollectionOptions>(o => o.Collections.Add(AIConstants.CollectionName));

        return services;
    }

    public static IServiceCollection AddAIChatProfileSource<TSource>(this IServiceCollection services, string sourceKey)
         where TSource : class, IAIChatProfileSource
    {
        ArgumentNullException.ThrowIfNull(sourceKey);

        services
            .AddScoped<TSource>()
            .AddScoped<IAIChatProfileSource>(sp => sp.GetService<TSource>())
            .AddKeyedScoped<IAIChatProfileSource>(sourceKey, (sp, key) => sp.GetService<TSource>());

        return services;
    }

    public static IServiceCollection AddAIDeploymentProvider<TProvider>(this IServiceCollection services, string providerKey)
        where TProvider : class, IAIDeploymentProvider
    {
        ArgumentNullException.ThrowIfNull(providerKey);

        services
            .AddScoped<TProvider>()
            .AddScoped<IAIDeploymentProvider>(sp => sp.GetService<TProvider>())
            .AddKeyedScoped<IAIDeploymentProvider>(providerKey, (sp, key) => sp.GetService<TProvider>());

        return services;
    }

    public static IServiceCollection AddAIChatCompletionService<TService>(this IServiceCollection services, string sourceKey)
        where TService : class, IAIChatCompletionService
    {
        ArgumentNullException.ThrowIfNull(sourceKey);

        services.TryAddScoped<TService>();
        services.TryAddScoped<IAIChatCompletionService>(sp => sp.GetService<TService>());
        services.AddKeyedScoped<IAIChatCompletionService>(sourceKey, (sp, key) => sp.GetService<TService>());

        return services;
    }

    public static IServiceCollection AddAITool<TTool>(this IServiceCollection services)
        where TTool : AITool
    {
        services.AddTransient<AITool, TTool>();

        return services;
    }
}
