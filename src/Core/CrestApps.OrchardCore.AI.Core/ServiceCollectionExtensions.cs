using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Markdown;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Services;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Services;
using Fluid;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Data;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Provides extension methods for service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the AI core services.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddAICoreServices(this IServiceCollection services)
    {
        services.AddCrestAppsCore(crestApps => crestApps
            .AddAISuite(ai => ai
                .AddMarkdown()
            )
        );

        // In Orchard Core's multi-tenant architecture, IConfiguration resolves to the
        // host configuration (project-root appsettings.json), not the tenant's ShellConfiguration
        // (which includes App_Data/appsettings.json and per-tenant overrides). The core
        // CrestApps.Core.AI library registers services that read AI settings from IConfiguration,
        // so they never see the tenant's config. Replace them with factory-based registrations
        // that provide IShellConfiguration (the tenant configuration) instead.
        services.ReplaceConfigurationServices();

        services
            .AddCatalogManagers()
            .AddScoped<ISearchIndexProfileStore, OrchardCoreSearchIndexProfileStore>()
            .AddScoped<IAIProfileStore, DefaultAIProfileStore>()
            .AddScoped<ICatalog<AIProfile>>(sp => sp.GetRequiredService<IAIProfileStore>())
            .AddScoped<INamedCatalog<AIProfile>>(sp => sp.GetRequiredService<IAIProfileStore>())
            .AddScoped<DefaultSpeechVoicePresenter>()
            .AddScoped<AIProviderConnectionStore>()
            .AddScoped<IAIProfileManager, DefaultAIProfileManager>()
            .AddScoped<ICatalogEntryHandler<AIProfile>, AIProfileHandler>();

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

    /// <summary>
    /// Adds the AI deployment services.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddAIDeploymentServices(this IServiceCollection services)
    {
        services
            .AddScoped<IAIDeploymentManager, SiteSettingsAIDeploymentManager>()
            .AddScoped<ICatalogEntryHandler<AIDeployment>, AIDeploymentHandler>();

        return services;
    }

    /// <summary>
    /// Adds the AI data source services.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddAIDataSourceServices(this IServiceCollection services)
    {
        services
            .AddScoped<DefaultAIDataSourceStore>()
            .AddScoped<IAIDataSourceStore>(sp => sp.GetRequiredService<DefaultAIDataSourceStore>())
            .AddScoped<ICatalog<AIDataSource>>(sp => sp.GetRequiredService<DefaultAIDataSourceStore>())
            .AddScoped<ICatalogManager<AIDataSource>, DefaultAIDataSourceManager>()
            .AddScoped<ICatalogEntryHandler<AIDataSource>, AIDataSourceHandler>();

        return services;
    }

    /// <summary>
    /// Adds the orchard core indexing adapters.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="providerName">The provider name.</param>
    public static IServiceCollection AddOrchardCoreIndexingAdapters(this IServiceCollection services, string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        services.TryAddKeyedScoped<ISearchIndexManager, OrchardCoreSearchIndexManager>(providerName);
        services.TryAddKeyedScoped<ISearchDocumentManager, OrchardCoreSearchDocumentManager>(providerName);

        return services;
    }

    /// <summary>
    /// Replaces core CrestApps.Core.AI configuration-backed services that inject
    /// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> (host-level) with
    /// factory-based registrations that provide <see cref="IShellConfiguration"/>
    /// (tenant-level) instead, so they can read from App_Data/appsettings.json.
    /// </summary>
    private static void ReplaceConfigurationServices(this IServiceCollection services)
    {
        services.Configure<AIProviderConnectionCatalogOptions>(o =>
        {
            // This code will be removed in the v3. We'll keep it now for backward compatibility.
            o.ProviderSections.Add("CrestApps_AI:Providers");
        });

        ReplaceService<INamedSourceCatalogSource<AIDeployment>, ConfigurationAIDeploymentSource>(
            services,
            ServiceLifetime.Scoped,
            static sp => ActivatorUtilities.CreateInstance<ConfigurationAIDeploymentSource>(
                sp,
                sp.GetRequiredService<IShellConfiguration>(),
                sp.GetRequiredService<IOptions<AIOptions>>(),
                sp.GetRequiredService<IOptions<AIDeploymentCatalogOptions>>(),
                sp.GetRequiredService<ILogger<ConfigurationAIDeploymentSource>>()));

        ReplaceService<INamedSourceCatalogSource<AIProviderConnection>, ConfigurationAIProviderConnectionSource>(
            services,
            ServiceLifetime.Scoped,
            static sp => ActivatorUtilities.CreateInstance<ConfigurationAIProviderConnectionSource>(
                sp,
                sp.GetRequiredService<IShellConfiguration>(),
                sp.GetRequiredService<IOptions<AIProviderConnectionCatalogOptions>>(),
                sp.GetRequiredService<ILogger<ConfigurationAIProviderConnectionSource>>()));
    }

    private static void ReplaceService<TService, TImplementation>(
        IServiceCollection services,
        ServiceLifetime lifetime,
        Func<IServiceProvider, TService> factory)
        where TService : class
        where TImplementation : class, TService
    {
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(TService) &&
            d.ImplementationType == typeof(TImplementation));

        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        services.Add(new ServiceDescriptor(typeof(TService), (sp) => factory(sp), lifetime));
    }
}
