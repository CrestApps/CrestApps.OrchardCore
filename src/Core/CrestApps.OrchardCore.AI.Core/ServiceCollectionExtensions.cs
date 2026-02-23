using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Orchestration;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Core.Tools;
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
            .AddScoped<ICatalog<AIDataSource>, DefaultAIDataSourceStore>()
            .AddScoped<ICatalogManager<AIDataSource>, DefaultAIDataSourceManager>()
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

    public static IServiceCollection AddDocumentTextExtractor<T>(this IServiceCollection services, params ExtractorExtension[] supportedExtensions)
        where T : class, IDocumentTextExtractor
    {
        services.Configure<ChatDocumentsOptions>(options =>
        {
            foreach (var extension in supportedExtensions)
            {
                options.Add(extension);
            }
        });

        services.AddScoped<IDocumentTextExtractor, T>();

        return services;
    }

    /// <summary>
    /// Adds the orchestration services including the default progressive tool orchestrator,
    /// tool registry, orchestration context builder, and orchestrator resolver.
    /// </summary>
    public static IServiceCollection AddOrchestrationServices(this IServiceCollection services)
    {
        services.AddOptions<OrchestratorOptions>();
        services.AddOptions<DefaultOrchestratorOptions>();

        // Register the shared tokenizer used by the tool registry and orchestrator.
        services.TryAddSingleton<ITextTokenizer, LuceneTextTokenizer>();

        // Register the orchestration context builder and core handlers.
        services.AddScoped<IOrchestrationContextBuilder, DefaultOrchestrationContextBuilder>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOrchestrationContextBuilderHandler, CompletionContextOrchestrationHandler>());

        // Register the preemptive search query provider (shared by DataSource and Document RAG handlers).
        services.AddScoped<PreemptiveSearchQueryProvider>();

        // Register the preemptive RAG coordinator that dispatches to all IPreemptiveRagHandler implementations.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOrchestrationContextBuilderHandler, PreemptiveRagOrchestrationHandler>());

        // Register the tool registry and default providers.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IToolRegistryProvider, LocalToolRegistryProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IToolRegistryProvider, SystemToolRegistryProvider>());
        services.AddScoped<IToolRegistry, DefaultToolRegistry>();

        // Register the default orchestrator.
        services.AddOrchestrator<DefaultOrchestrator>(DefaultOrchestrator.OrchestratorName)
            .WithTitle("Default Orchestrator");

        // Register the resolver.
        services.AddScoped<IOrchestratorResolver, DefaultOrchestratorResolver>();

        // Register content generation system tools.
        services.AddAITool<GenerateImageTool>(GenerateImageTool.TheName)
            .WithTitle("Generate Image")
            .WithDescription("Generates an image from a text description using an AI image generation model.")
            .WithPurpose(AIToolPurposes.ContentGeneration);

        services.AddAITool<GenerateChartTool>(GenerateChartTool.TheName)
            .WithTitle("Generate Chart")
            .WithDescription("Generates a Chart.js configuration from a data description.")
            .WithPurpose(AIToolPurposes.ContentGeneration);

        return services;
    }

    /// <summary>
    /// Registers an orchestrator implementation with the given name.
    /// Returns a builder for fluent configuration (e.g., setting a display title).
    /// </summary>
    /// <typeparam name="TOrchestrator">The orchestrator type implementing <see cref="IOrchestrator"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The unique name for this orchestrator.</param>
    /// <returns>A builder for further configuration of this orchestrator.</returns>
    /// <example>
    /// <code>
    /// services.AddOrchestrator&lt;ProgressiveToolOrchestrator&gt;("default")
    ///     .WithTitle("Progressive Tool Orchestrator");
    /// services.AddOrchestrator&lt;CopilotOrchestrator&gt;("copilot")
    ///     .WithTitle("GitHub Copilot Orchestrator");
    /// </code>
    /// </example>
    public static OrchestratorBuilder<TOrchestrator> AddOrchestrator<TOrchestrator>(this IServiceCollection services, string name)
        where TOrchestrator : class, IOrchestrator
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        services.TryAddScoped<TOrchestrator>();

        var entry = new OrchestratorEntry
        {
            Type = typeof(TOrchestrator),
        };

        services.Configure<OrchestratorOptions>(options =>
        {
            options.Orchestrators[name] = entry;
        });

        return new OrchestratorBuilder<TOrchestrator>(entry);
    }
}
