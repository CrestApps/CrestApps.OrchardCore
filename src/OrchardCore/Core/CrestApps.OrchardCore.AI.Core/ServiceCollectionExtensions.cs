using CrestApps.AI;
using CrestApps.AI.Deployments;
using CrestApps.AI.Models;
using CrestApps.AI.Profiles;
using CrestApps.AI.Services;
using CrestApps.AI.Speech;
using CrestApps.Infrastructure.Indexing;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Core;
using CrestApps.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;

namespace CrestApps.OrchardCore.AI.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAICoreServices(this IServiceCollection services)
    {
        services
            .AddCrestAppsAI()
            .AddCatalogs()
            .AddCatalogManagers()
            .AddScoped<ISpeechVoiceResolver, DefaultSpeechVoiceResolver>()
            .AddScoped<IAIProfileStore, DefaultAIProfileStore>()
            .AddScoped<ICatalog<AIProfile>>(sp => sp.GetRequiredService<IAIProfileStore>())
            .AddScoped<INamedCatalog<AIProfile>>(sp => sp.GetRequiredService<IAIProfileStore>())
            .AddScoped<DefaultSpeechVoicePresenter>()
            .AddScoped<AIProviderConnectionStore>()
            .AddScoped<ICatalog<AIProviderConnection>>(sp => sp.GetRequiredService<AIProviderConnectionStore>())
            .AddScoped<INamedCatalog<AIProviderConnection>>(sp => sp.GetRequiredService<AIProviderConnectionStore>())
            .AddScoped<IAIProfileManager, DefaultAIProfileManager>()
            .AddScoped<ICatalogEntryHandler<AIProfile>, AIProfileHandler>();

        services
            .AddScoped<IAuthorizationHandler, AIProfileAuthorizationHandler>()
            .AddScoped<IAuthorizationHandler, AIToolAuthorizationHandler>()
            .Configure<StoreCollectionOptions>(o => o.Collections.Add(AIConstants.AICollectionName));

        return services;
    }

    public static IServiceCollection AddAIDeploymentServices(this IServiceCollection services)
    {
        services
            .AddScoped<DefaultAIDeploymentStore>()
            .AddScoped<IAIDeploymentStore>(sp => sp.GetRequiredService<DefaultAIDeploymentStore>())
            .AddScoped<ConfigurationAIDeploymentCatalog>()
            .AddScoped<ICatalog<AIDeployment>>(sp => sp.GetRequiredService<ConfigurationAIDeploymentCatalog>())
            .AddScoped<INamedCatalog<AIDeployment>>(sp => sp.GetRequiredService<ConfigurationAIDeploymentCatalog>())
            .AddScoped<INamedSourceCatalog<AIDeployment>>(sp => sp.GetRequiredService<ConfigurationAIDeploymentCatalog>())
            .AddScoped<IAIDeploymentManager, CrestApps.AI.Services.DefaultAIDeploymentManager>()
            .AddScoped<ICatalogEntryHandler<AIDeployment>, AIDeploymentHandler>();

        return services;
    }

    public static IServiceCollection AddAIDataSourceServices(this IServiceCollection services)
    {
        services
            .AddScoped<ICatalog<AIDataSource>, DefaultAIDataSourceStore>()
            .AddScoped<ICatalogManager<AIDataSource>, DefaultAIDataSourceManager>()
            .AddScoped<ICatalogEntryHandler<AIDataSource>, AIDataSourceHandler>();

        return services;
    }

    public static IServiceCollection AddOrchardCoreIndexingAdapters(this IServiceCollection services, string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        services.TryAddKeyedScoped<ISearchIndexManager, OrchardCoreSearchIndexManager>(providerName);
        services.TryAddKeyedScoped<ISearchDocumentManager, OrchardCoreSearchDocumentManager>(providerName);

        return services;
    }
}
