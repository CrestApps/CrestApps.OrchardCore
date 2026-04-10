using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Markdown;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Services;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AI;
using CrestApps.Core.Data.YesSql.Services;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Core;
using Fluid;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;

namespace CrestApps.OrchardCore.AI.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAICoreServices(this IServiceCollection services)
    {
        services.AddCrestAppsCore(crestApps => crestApps
            .AddAISuite(ai => ai
                .AddMarkdown()
            )
        );

        services
            .AddCatalogs()
            .AddCatalogManagers()
            .AddScoped<ISearchIndexProfileStore, OrchardCoreSearchIndexProfileStore>()
            .AddScoped<IAIProfileStore, DefaultAIProfileStore>()
            .AddScoped<ICatalog<AIProfile>>(sp => sp.GetRequiredService<IAIProfileStore>())
            .AddScoped<INamedCatalog<AIProfile>>(sp => sp.GetRequiredService<IAIProfileStore>())
            .AddScoped<DefaultSpeechVoicePresenter>()
            .AddScoped<AIProviderConnectionStore>()
            // .AddScoped<ConfigurationAIProviderConnectionCatalog>()
            // .AddScoped<ICatalog<AIProviderConnection>>(sp => sp.GetRequiredService<ConfigurationAIProviderConnectionCatalog>())
            // .AddScoped<INamedCatalog<AIProviderConnection>>(sp => sp.GetRequiredService<ConfigurationAIProviderConnectionCatalog>())
            // .AddScoped<INamedSourceCatalog<AIProviderConnection>>(sp => sp.GetRequiredService<ConfigurationAIProviderConnectionCatalog>())
            // 
            // .AddNamedSourceDocumentCatalog<AIProviderConnection,  ConfigurationAIProviderConnectionCatalog>()

            .AddScoped<IAIProfileManager, DefaultAIProfileManager>()
            .AddScoped<ICatalogEntryHandler<AIProfile>, AIProfileHandler>();

        services.AddKeyedScoped<INamedSourceCatalog<AIProviderConnection>, NamedSourceDocumentCatalog<AIProviderConnection, AIProviderConnectionIndex>>(ConfigurationAIProviderConnectionCatalog.PersistedCatalogKey)
            .AddYesSqlNamedSourceDocumentCatalog<AIProviderConnection, AIProviderConnectionIndex, ConfigurationAIProviderConnectionCatalog>();

        services
            .AddScoped<IAuthorizationHandler, AIProfileAuthorizationHandler>()
            .AddScoped<IAuthorizationHandler, AIToolAuthorizationHandler>()
            .Configure<StoreCollectionOptions>(o => o.Collections.Add(AIConstants.AICollectionName));

        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<AIProfile>();
            o.MemberAccessStrategy.Register<AIChatSession>();
            o.MemberAccessStrategy.Register<AIChatSessionPrompt>();
            o.MemberAccessStrategy.Register<ExtractedFieldState>();
            o.MemberAccessStrategy.Register<PostSessionResult>();
            o.MemberAccessStrategy.Register<AICompletionReference>();
            o.MemberAccessStrategy.Register<AIToolDefinitionEntry>();
            o.MemberAccessStrategy.Register<ChatDocumentInfo>();
            o.MemberAccessStrategy.Register<ConversionGoalResult>();
            o.MemberAccessStrategy.Register<AIResponseMessage>();
        });

        return services;
    }

    public static IServiceCollection AddAIDeploymentServices(this IServiceCollection services)
    {
        services
            .AddCatalogs()
            .AddScoped<DefaultAIDeploymentStore>()
            .AddScoped<ConfigurationAIDeploymentCatalog>()
            .AddScoped<ICatalog<AIDeployment>>(sp => sp.GetRequiredService<ConfigurationAIDeploymentCatalog>())
            .AddScoped<INamedCatalog<AIDeployment>>(sp => sp.GetRequiredService<ConfigurationAIDeploymentCatalog>())
            .AddScoped<INamedSourceCatalog<AIDeployment>>(sp => sp.GetRequiredService<ConfigurationAIDeploymentCatalog>())
            .AddScoped<IAIDeploymentManager, SiteSettingsAIDeploymentManager>()
            .AddScoped<ICatalogEntryHandler<AIDeployment>, AIDeploymentHandler>();

        // Register the named source catalog separately with the specific key, since it's consumed directly
        // by the ConfigurationAIDeploymentCatalog and needs to be resolved by that key to store tenant specific deployments.
        services.AddKeyedScoped<INamedSourceCatalog<AIDeployment>, DefaultAIDeploymentStore>(ConfigurationAIDeploymentCatalog.PersistedCatalogKey);

        services.AddKeyedScoped<INamedSourceCatalog<AIDeployment>, NamedSourceDocumentCatalog<AIDeployment, AIDeploymentIndex>>(ConfigurationAIDeploymentCatalog.PersistedCatalogKey)
            .AddYesSqlNamedSourceDocumentCatalog<AIDeployment, AIDeploymentIndex, ConfigurationAIDeploymentCatalog>();

        services
            .Configure<AIDeploymentCatalogOptions>(o =>
            {
                o.DeploymentSections.Add("OrchardCore:CrestApps:AI:Deployments");
            });

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
