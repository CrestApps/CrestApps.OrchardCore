using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
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
    /// Adds a document processing strategy and registers it for a specific intent.
    /// </summary>
    /// <typeparam name="TStrategy">The strategy type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="intent">The intent name this strategy handles.</param>
    public static IServiceCollection AddDocumentProcessingStrategy<TStrategy>(this IServiceCollection services, string intent)
        where TStrategy : class, IDocumentProcessingStrategy
    {
        ArgumentException.ThrowIfNullOrEmpty(intent);

        services.AddScoped<IDocumentProcessingStrategy, TStrategy>();
        services.Configure<ChatInteractionDocumentOptions>(options =>
        {
            options.AddStrategy<TStrategy>(intent);
        });

        return services;
    }

    /// <summary>
    /// Adds the default document processing strategies.
    /// </summary>
    public static IServiceCollection AddDefaultDocumentProcessingStrategies(this IServiceCollection services)
    {
        services
            .AddDocumentProcessingStrategy<SummarizationDocumentProcessingStrategy>(DocumentIntents.SummarizeDocument)
            .AddDocumentProcessingStrategy<TabularAnalysisDocumentProcessingStrategy>(DocumentIntents.AnalyzeTabularData)
            .AddDocumentProcessingStrategy<ExtractionDocumentProcessingStrategy>(DocumentIntents.ExtractStructuredData)
            .AddDocumentProcessingStrategy<ComparisonDocumentProcessingStrategy>(DocumentIntents.CompareDocuments)
            .AddDocumentProcessingStrategy<TransformationDocumentProcessingStrategy>(DocumentIntents.TransformFormat)
            .AddDocumentProcessingStrategy<GeneralReferenceDocumentProcessingStrategy>(DocumentIntents.GeneralChatWithReference);

        return services;
    }
}
