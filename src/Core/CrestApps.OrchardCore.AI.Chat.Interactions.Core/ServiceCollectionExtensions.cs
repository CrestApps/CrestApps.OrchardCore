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
        // Register the keyword-based intent detector as a concrete service (used as fallback)
        services.TryAddScoped<KeywordDocumentIntentDetector>();

        // Register the AI-based intent detector as the primary implementation
        services.TryAddScoped<IDocumentIntentDetector, AIDocumentIntentDetector>();

        // Register the strategy provider
        services.TryAddScoped<IDocumentProcessingStrategyProvider, DefaultDocumentProcessingStrategyProvider>();

        return services;
    }

    /// <summary>
    /// Registers a document processing intent that will be recognized by the AI intent detector.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="intentName">The unique name of the intent (e.g., "DocumentQnA").</param>
    /// <param name="description">A description of when this intent should be detected, used by the AI classifier.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Only intents registered via this method will be recognized by the AI intent detector.
    /// Each intent should have a corresponding strategy registered via <see cref="AddDocumentProcessingStrategy{TStrategy}"/>.
    /// </remarks>
    public static IServiceCollection AddDocumentIntent(
        this IServiceCollection services,
        string intentName,
        string description)
    {
        ArgumentException.ThrowIfNullOrEmpty(intentName);
        ArgumentException.ThrowIfNullOrEmpty(description);

        services.Configure<DocumentProcessingOptions>(options =>
        {
            options.InternalIntents.TryAdd(intentName, description);
        });

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
    /// Adds the default document processing strategies with their intent registrations.
    /// </summary>
    public static IServiceCollection AddDefaultDocumentProcessingStrategies(this IServiceCollection services)
    {
        // Register intents for the default strategies
        services
            .AddDocumentIntent(
                DocumentIntents.SummarizeDocument,
                "The user wants a summary, overview, brief, key points, or outline of document content.")
            .AddDocumentIntent(
                DocumentIntents.AnalyzeTabularData,
                "The user wants to perform calculations, aggregations, statistics, or data analysis on tabular data (CSV, Excel, etc.).")
            .AddDocumentIntent(
                DocumentIntents.ExtractStructuredData,
                "The user wants to extract specific data, parse content into structured formats (JSON, schema), or pull out entities.")
            .AddDocumentIntent(
                DocumentIntents.CompareDocuments,
                "The user wants to compare, contrast, find differences, or analyze similarities between multiple documents.")
            .AddDocumentIntent(
                DocumentIntents.TransformFormat,
                "The user wants to convert, transform, reformat content into another representation (tables, bullet points, different format).")
            .AddDocumentIntent(
                DocumentIntents.GenerateImage,
                "The user wants to generate, create, draw, or produce an image, picture, illustration, visual, or artwork based on a text description.")
            .AddDocumentIntent(
                DocumentIntents.GeneralChatWithReference,
                "General conversation that may reference documents but doesn't fit other categories.");

        // Register the strategies
        services
            .AddDocumentProcessingStrategy<SummarizationDocumentProcessingStrategy>()
            .AddDocumentProcessingStrategy<TabularAnalysisDocumentProcessingStrategy>()
            .AddDocumentProcessingStrategy<ExtractionDocumentProcessingStrategy>()
            .AddDocumentProcessingStrategy<ComparisonDocumentProcessingStrategy>()
            .AddDocumentProcessingStrategy<TransformationDocumentProcessingStrategy>()
            .AddDocumentProcessingStrategy<ImageGenerationDocumentProcessingStrategy>()
            .AddDocumentProcessingStrategy<GeneralReferenceDocumentProcessingStrategy>();

        return services;
    }
}
