using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAICoreServices(this IServiceCollection services)
    {
        services
            .AddScoped<IAIProfileStore, DefaultAIProfileStore>()
            .AddScoped<IAICompletionService, DefaultAICompletionService>()
            .AddScoped<IAIProfileManager, DefaultAIProfileManager>()
            .AddScoped<IAIProfileManagerSession, DefaultAIProfileManagerSession>()
            .AddScoped<IModelHandler<AIProfile>, AIProfileHandler>();

        services
            .AddPermissionProvider<AIPermissionsProvider>()
            .AddScoped<IAuthorizationHandler, AIProfileAuthenticationHandler>()
            .Configure<StoreCollectionOptions>(o => o.Collections.Add(AIConstants.CollectionName));

        services
            .AddScoped<IAIToolInstanceStore, DefaultAIToolInstanceStore>()
            .AddScoped<IAIToolInstanceManager, DefaultAIToolInstanceManager>()
            .AddScoped<IModelHandler<AIToolInstance>, AIToolInstanceHandler>();

        return services;
    }

    public static IServiceCollection AddAIDeploymentServices(this IServiceCollection services)
    {
        services
            .AddScoped<IAIDeploymentStore, DefaultAIDeploymentStore>()
            .AddScoped<IAIDeploymentManager, DefaultAIDeploymentManager>()
            .AddScoped<IModelHandler<AIDeployment>, AIDeploymentHandler>()
            .AddPermissionProvider<AIDeploymentProvider>();

        return services;
    }

    public static IServiceCollection AddAIProfile<TClient>(this IServiceCollection services, string implementationName, string providerName, Action<AIProfileProviderEntry> configure = null)
        where TClient : class, IAICompletionClient
    {
        return services
            .Configure<AICompletionOptions>(o =>
            {
                o.AddProfileSource(implementationName, providerName, configure);
            })
            .AddAICompletionClient<TClient>(implementationName);
    }

    public static IServiceCollection AddAIDeploymentProvider(this IServiceCollection services, string providerName, Action<AIDeploymentProviderEntry> configure = null)
    {
        services
            .Configure<AICompletionOptions>(o =>
            {
                o.AddDeploymentProvider(providerName, configure);
            });

        return services;
    }

    public static IServiceCollection AddAICompletionClient<TClient>(this IServiceCollection services, string clientName)
        where TClient : class, IAICompletionClient
    {
        services.Configure<AICompletionOptions>(o =>
        {
            o.AddClient<TClient>(clientName);
        });
        services.TryAddScoped<TClient>();
        services.AddScoped<IAICompletionClient>(sp => sp.GetService<TClient>()); ;

        return services;
    }
}
