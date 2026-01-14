using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core;

/// <summary>
/// Extension methods for registering document processing services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the core document processing services including intent detection and strategy provider.
    /// </summary>
    public static IServiceCollection AddDocumentProcessingServices(this IServiceCollection services)
    {
        // Register the intent detector
        services.TryAddScoped<IDocumentIntentDetector, DefaultDocumentIntentDetector>();

        // Register the strategy provider
        services.TryAddScoped<IDocumentProcessingStrategyProvider, DefaultDocumentProcessingStrategyProvider>();

        return services;
    }

    /// <summary>
    /// Adds a document processing strategy to the service collection.
    /// Strategies are called in sequence and each decides whether to handle the request.
    /// </summary>
    /// <typeparam name="TStrategy">The strategy type.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddDocumentProcessingStrategy<TStrategy>(this IServiceCollection services)
        where TStrategy : class, IDocumentProcessingStrategy
    {
        services.AddScoped<IDocumentProcessingStrategy, TStrategy>();
        return services;
    }

    /// <summary>
    /// Adds the default document processing strategies.
    /// </summary>
    public static IServiceCollection AddDefaultDocumentProcessingStrategies(this IServiceCollection services)
    {
        services
            .AddDocumentProcessingStrategy<SummarizationDocumentProcessingStrategy>()
            .AddDocumentProcessingStrategy<TabularAnalysisDocumentProcessingStrategy>()
            .AddDocumentProcessingStrategy<ExtractionDocumentProcessingStrategy>()
            .AddDocumentProcessingStrategy<ComparisonDocumentProcessingStrategy>()
            .AddDocumentProcessingStrategy<TransformationDocumentProcessingStrategy>()
            .AddDocumentProcessingStrategy<GeneralReferenceDocumentProcessingStrategy>();

        return services;
    }
}
