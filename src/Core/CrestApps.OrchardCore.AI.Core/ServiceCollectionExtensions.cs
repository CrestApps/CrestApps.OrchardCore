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

    public static IServiceCollection AddAIProfileServices(this IServiceCollection services)
    {
        services
            .AddScoped<IAIProfileStore, DefaultAIProfileStore>()
            .AddScoped<IAIProfileManager, DefaultAIProfileManager>()
            .AddScoped<IAIProfileManagerSession, DefaultAIProfileManagerSession>()
            .AddScoped<IAIProfileHandler, AIProfileHandler>()
            .AddScoped<IAIChatSessionManager, DefaultAIChatSessionManager>();

        services
            .AddPermissionProvider<AIPermissionsProvider>()
            .AddScoped<IAuthorizationHandler, AIProfileAuthenticationHandler>()
            .Configure<StoreCollectionOptions>(o => o.Collections.Add(AIConstants.CollectionName));

        return services;
    }

    public static IServiceCollection AddAIProfileSource<TSource>(this IServiceCollection services, string sourceKey)
         where TSource : class, IAIProfileSource
    {
        ArgumentNullException.ThrowIfNull(sourceKey);

        services
            .AddScoped<TSource>()
            .AddScoped<IAIProfileSource>(sp => sp.GetService<TSource>())
            .AddKeyedScoped<IAIProfileSource>(sourceKey, (sp, key) => sp.GetService<TSource>());

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

    public static IServiceCollection AddAICompletionService<TService>(this IServiceCollection services, string sourceKey)
        where TService : class, IAICompletionService
    {
        ArgumentNullException.ThrowIfNull(sourceKey);

        services.TryAddScoped<TService>();
        services.TryAddScoped<IAICompletionService>(sp => sp.GetService<TService>());
        services.AddKeyedScoped<IAICompletionService>(sourceKey, (sp, key) => sp.GetService<TService>());

        return services;
    }

    public static IServiceCollection AddAITool<TTool>(this IServiceCollection services)
        where TTool : AITool
    {
        services.AddTransient<AITool, TTool>();

        return services;
    }
}
