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

public static class ServiceCollectionExtensions
{
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

    public static IServiceCollection AddAIDeploymentServices(this IServiceCollection services)
    {
        services
            .AddScoped<IAIDeploymentManager, SiteSettingsAIDeploymentManager>()
            .AddScoped<ICatalogEntryHandler<AIDeployment>, AIDeploymentHandler>();

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
            .AddScoped<DefaultAIDataSourceStore>()
            .AddScoped<IAIDataSourceStore>(sp => sp.GetRequiredService<DefaultAIDataSourceStore>())
            .AddScoped<ICatalog<AIDataSource>>(sp => sp.GetRequiredService<DefaultAIDataSourceStore>())
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

    /// <summary>
    /// Replaces core CrestApps.Core.AI configuration-backed services that inject
    /// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> (host-level) with
    /// factory-based registrations that provide <see cref="IShellConfiguration"/>
    /// (tenant-level) instead, so they can read from App_Data/appsettings.json.
    /// </summary>
    private static void ReplaceConfigurationServices(this IServiceCollection services)
    {
        ReplaceService<IPostConfigureOptions<AIProviderOptions>, ConfigurationAIProviderConnectionsOptionsConfiguration>(
            services,
            ServiceLifetime.Transient,
            static sp => new ConfigurationAIProviderConnectionsOptionsConfiguration(
                sp.GetRequiredService<IShellConfiguration>(),
                sp.GetRequiredService<IOptions<AIProviderConnectionCatalogOptions>>(),
                sp.GetRequiredService<ILogger<ConfigurationAIProviderConnectionsOptionsConfiguration>>()));

        ReplaceService<INamedSourceCatalogSource<AIDeployment>, ConfigurationAIDeploymentSource>(
            services,
            ServiceLifetime.Scoped,
            static sp => new ConfigurationAIDeploymentSource(
                sp.GetRequiredService<IShellConfiguration>(),
                sp.GetRequiredService<IOptions<AIOptions>>(),
                sp.GetRequiredService<IOptions<AIDeploymentCatalogOptions>>(),
                sp.GetRequiredService<ILogger<ConfigurationAIDeploymentSource>>()));

        ReplaceService<INamedSourceCatalogSource<AIProviderConnection>, ConfigurationAIProviderConnectionSource>(
            services,
            ServiceLifetime.Scoped,
            static sp => new ConfigurationAIProviderConnectionSource(
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
