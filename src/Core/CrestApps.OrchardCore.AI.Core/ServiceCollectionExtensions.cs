using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;

namespace CrestApps.OrchardCore.AI.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAICoreServices(this IServiceCollection services)
    {
        services
            .AddCatalogs()
            .AddCatalogManagers()
            .AddScoped<IAIClientFactory, DefaultAIClientFactory>()
            .AddScoped<INamedCatalog<AIProfile>, DefaultAIProfileStore>()
            .AddScoped<AIProviderConnectionStore>()
            .AddScoped<ICatalog<AIProviderConnection>>(sp => sp.GetRequiredService<AIProviderConnectionStore>())
            .AddScoped<INamedCatalog<AIProviderConnection>>(sp => sp.GetRequiredService<AIProviderConnectionStore>())
            .AddScoped<IAICompletionService, DefaultAICompletionService>()
            .AddScoped<IAICompletionContextBuilder, DefaultAICompletionContextBuilder>()
            .AddScoped<IAIProfileManager, DefaultAIProfileManager>()
            .AddScoped<ICatalogEntryHandler<AIProfile>, AIProfileHandler>();

        services
            .AddScoped<IAuthorizationHandler, AIProfileAuthorizationHandler>()
            .AddScoped<IAuthorizationHandler, AIToolAuthorizationHandler>()
            .Configure<StoreCollectionOptions>(o => o.Collections.Add(AIConstants.CollectionName));

        services
            .AddScoped<ICatalogEntryHandler<AIToolInstance>, AIToolInstanceHandler>();

        return services;
    }

    public static IServiceCollection AddAIDeploymentServices(this IServiceCollection services)
    {
        services
            .AddScoped<DefaultAIDeploymentStore>()
            .AddScoped<ICatalog<AIDeployment>>(sp => sp.GetRequiredService<DefaultAIDeploymentStore>())
            .AddScoped<INamedCatalog<AIDeployment>>(sp => sp.GetRequiredService<DefaultAIDeploymentStore>())
            .AddScoped<INamedSourceCatalog<AIDeployment>>(sp => sp.GetRequiredService<DefaultAIDeploymentStore>())
            .AddScoped<IAIDeploymentManager, DefaultAIDeploymentManager>()
            .AddScoped<ICatalogEntryHandler<AIDeployment>, AIDeploymentHandler>();

        return services;
    }

    public static IServiceCollection AddAIDataSourceServices(this IServiceCollection services)
    {
        services
            .AddScoped<IAIDataSourceStore, DefaultAIDataSourceStore>()
            .AddScoped<IAIDataSourceManager, DefaultAIDataSourceManager>()
            .AddScoped<ICatalogEntryHandler<AIDataSource>, AIDataSourceHandler>();

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

    public static IServiceCollection AddAIDataSource(this IServiceCollection services, string profileSource, string type, Action<AIDataSourceOptionsEntry> configure = null)
    {
        services
            .Configure<AIOptions>(o =>
            {
                o.AddDataSource(profileSource, type, configure);
            });

        return services;
    }

    public static IServiceCollection AddDocumentTextExtractor<T>(this IServiceCollection services, params ExtractorExtension[] supportedExtensions)
        where T : class, IDocumentTextExtractor
    {
        services.Configure<ChatInteractionsOptions>(options =>
        {
            foreach (var extension in supportedExtensions)
            {
                options.Add(extension);
            }
        });

        services.AddScoped<IDocumentTextExtractor, T>();

        return services;
    }
}
