using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Core.Strategies;
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

    /// <summary>
    /// Adds the core prompt routing services including intent detection, routing, and strategy provider.
    /// </summary>
    public static IServiceCollection AddPromptRoutingServices(this IServiceCollection services)
    {
        // Register the keyword-based intent detector as a concrete service (used as fallback)
        services.AddScoped<KeywordPromptIntentDetector>();

        // Register the AI-based intent detector as the primary implementation
        services.AddScoped<IPromptIntentDetector, AIPromptIntentDetector>();

        // Register the strategy provider
        services.AddScoped<IPromptProcessingStrategyProvider, DefaultPromptProcessingStrategyProvider>();

        // Register the routing service
        services.AddScoped<IPromptRouter, DefaultPromptRouter>();

        return services;
    }

    /// <summary>
    /// Registers a document processing intent that will be recognized by the AI intent detector.
    /// Returns a builder to fluently configure the intent's strategies and behavior.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="intentName">The unique name of the intent (e.g., "DocumentQnA").</param>
    /// <param name="description">A description of when this intent should be detected, used by the AI classifier.</param>
    /// <returns>A builder for further intent configuration.</returns>
    /// <remarks>
    /// Only intents registered via this method will be recognized by the AI intent detector.
    /// Use the returned builder to attach strategies:
    /// <code>
    /// services.AddPromptProcessingIntent("MyIntent", "description")
    ///     .WithStrategy&lt;MyStrategy&gt;();
    ///
    /// services.AddPromptProcessingIntent("MySecondPhaseIntent", "description")
    ///     .WithSecondPhaseStrategy&lt;MyResolver&gt;();
    ///
    /// services.AddPromptProcessingIntent("MyHeavyIntent", "description")
    ///     .AsHeavy()
    ///     .WithStrategy&lt;MyHeavyStrategy&gt;();
    /// </code>
    /// </remarks>
    public static PromptProcessingIntentBuilder AddPromptProcessingIntent(
        this IServiceCollection services,
        string intentName,
        string description)
    {
        ArgumentException.ThrowIfNullOrEmpty(intentName);
        ArgumentException.ThrowIfNullOrEmpty(description);

        services.Configure<PromptProcessingOptions>(options =>
        {
            options.InternalIntents.TryAdd(intentName, description);
        });

        return new PromptProcessingIntentBuilder(services, intentName);
    }

    /// <summary>
    /// Adds a document processing strategy to the service collection.
    /// Strategies are called in sequence and each decides whether to handle the request.
    /// Uses <see cref="Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(IServiceCollection, ServiceDescriptor)"/>
    /// to prevent duplicate registrations when multiple modules register the same strategy.
    /// </summary>
    /// <typeparam name="TStrategy">The strategy type.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddPromptProcessingStrategy<TStrategy>(this IServiceCollection services)
        where TStrategy : class, IPromptProcessingStrategy
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPromptProcessingStrategy, TStrategy>());
        return services;
    }

    /// <summary>
    /// Adds the default prompt processing strategies with their intent registrations.
    /// </summary>
    public static IServiceCollection AddDefaultPromptProcessingStrategies(this IServiceCollection services)
    {
        services.AddPromptProcessingIntent(
            DocumentIntents.GenerateImage,
            "The user requests creation of a new image from a text description. Detect when the prompt asks for visuals, illustrations, diagrams, or artwork and capture any optional parameters (style, size, aspect ratio, color palette, level of detail, or composition). The output should be an image-generation task consisting of a refined prompt and metadata suitable for calling an image-generation service.")
            .WithStrategy<ImageGenerationDocumentProcessingStrategy>();

        services.AddPromptProcessingIntent(
            DocumentIntents.GenerateImageWithHistory,
            "Trigger when the user requests the creation of an image, diagram, or visual that is based on information, data, or discussion from prior chat messages. Detect references to previous conversation, earlier outputs, or chat-based data that should influence the visual. This intent is strictly for generating images that depend on chat history, including summaries, illustrations, or artwork derived from earlier messages, but does not include charts or graphs.");

        services.AddPromptProcessingIntent(
            DocumentIntents.GenerateChart,
            "The user wants to create a chart, graph, or data visualization such as bar chart, line chart, pie chart, scatter plot, or histogram. The AI model already receives conversation history, so this intent handles both explicit data in the prompt and references to data from earlier messages.")
            .WithStrategy<ChartGenerationDocumentProcessingStrategy>();

        return services;
    }
}
