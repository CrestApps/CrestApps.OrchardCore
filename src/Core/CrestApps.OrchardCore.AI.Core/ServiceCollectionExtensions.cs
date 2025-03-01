using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Services;
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
            .AddCoreModelServices()
            .AddScoped<INamedModelStore<AIProfile>, DefaultAIProfileStore>()
            .AddScoped<INamedModelStore<AIProviderConnection>, AIProviderConnectionStore>()
            .AddScoped<IAICompletionService, DefaultAICompletionService>()
            .AddScoped<IAIProfileManager, DefaultAIProfileManager>()
            .AddScoped<IAIProfileManagerSession, DefaultAIProfileManagerSession>()
            .AddScoped<IModelHandler<AIProfile>, AIProfileHandler>();

        services
            .AddPermissionProvider<AIPermissionsProvider>()
            .AddScoped<IAuthorizationHandler, AIProfileAuthenticationHandler>()
            .Configure<StoreCollectionOptions>(o => o.Collections.Add(AIConstants.CollectionName));

        services
            .AddScoped<IModelHandler<AIToolInstance>, AIToolInstanceHandler>();

        return services;
    }

    public static IServiceCollection AddAIDeploymentServices(this IServiceCollection services)
    {
        services
            .AddScoped<INamedModelStore<AIDeployment>, DefaultAIDeploymentStore>()
            .AddScoped<IAIDeploymentManager, DefaultAIDeploymentManager>()
            .AddScoped<IModelHandler<AIDeployment>, AIDeploymentHandler>()
            .AddPermissionProvider<AIDeploymentProvider>();

        return services;
    }

    public static IServiceCollection AddAIProfile<TClient>(this IServiceCollection services, string implementationName, string providerName, Action<AIProfileProviderEntry> configure = null)
        where TClient : class, IAICompletionClient
    {
        return services
            .Configure<AIOptions>(o =>
            {
                o.AddProfileSource(implementationName, providerName, configure);
            })
            .AddAICompletionClient<TClient>(implementationName);
    }

    public static IServiceCollection AddAIDeploymentProvider(this IServiceCollection services, string providerName, Action<AIDeploymentProviderEntry> configure = null)
    {
        services
            .Configure<AIOptions>(o =>
            {
                o.AddDeploymentProvider(providerName, configure);
            });

        return services;
    }

    public static IServiceCollection AddAICompletionClient<TClient>(this IServiceCollection services, string clientName)
        where TClient : class, IAICompletionClient
    {
        services.Configure<AIOptions>(o =>
        {
            o.AddClient<TClient>(clientName);
        });

        services.TryAddScoped<TClient>();
        services.AddScoped<IAICompletionClient>(sp => sp.GetService<TClient>());

        return services;
    }

    public static IServiceCollection AddAIConnectionSource(this IServiceCollection services, string providerName, Action<AIProviderConnectionOptionsEntry> configure = null)
    {
        services.Configure<AIOptions>(o =>
        {
            o.AddConnectionSource(providerName, configure);
        });

        return services;
    }
}
