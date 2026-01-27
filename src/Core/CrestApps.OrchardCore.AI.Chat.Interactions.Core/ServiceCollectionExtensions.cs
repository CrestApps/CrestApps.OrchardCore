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
    public static IServiceCollection AddPromptRoutingServices(this IServiceCollection services)
    {
        // Register the keyword-based intent detector as a concrete service (used as fallback)
        services.TryAddScoped<KeywordPromptIntentDetector>();

        // Register the AI-based intent detector as the primary implementation
        services.TryAddScoped<IPromptIntentDetector, AIPromptIntentDetector>();

        // Register the strategy provider
        services.TryAddScoped<IPromptProcessingStrategyProvider, DefaultPromptProcessingStrategyProvider>();

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
    /// Each intent should have a corresponding strategy registered via <see cref="AddPromptProcessingStrategy{TStrategy}"/>.
    /// </remarks>
    public static IServiceCollection AddPromptProcessingIntent(
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

        return services;
    }

    /// <summary>
    /// Adds a document processing strategy to the service collection.
    /// Strategies are called in sequence and each decides whether to handle the request.
    /// </summary>
    /// <typeparam name="TStrategy">The strategy type.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddPromptProcessingStrategy<TStrategy>(this IServiceCollection services)
        where TStrategy : class, IPromptProcessingStrategy
    {
        services.AddScoped<IPromptProcessingStrategy, TStrategy>();
        return services;
    }

    /// <summary>
    /// Adds the default document processing strategies with their intent registrations.
    /// </summary>
    public static IServiceCollection AddDefaultPromptProcessingStrategies(this IServiceCollection services)
    {
        // Register intents for the default strategies
        services
            .AddPromptProcessingIntent(
                DocumentIntents.GenerateImage,
                "The user requests creation of a new image from a text description. Detect when the prompt asks for visuals, illustrations, diagrams, or artwork and capture any optional parameters (style, size, aspect ratio, color palette, level of detail, or composition). The output should be an image-generation task consisting of a refined prompt and metadata suitable for calling an image-generation service.")
            .AddPromptProcessingIntent(
                DocumentIntents.GenerateImageWithHistory,
                "Trigger when the user requests the creation of an image, diagram, or visual that is based on information, data, or discussion from prior chat messages. Detect references to previous conversation, earlier outputs, or chat-based data that should influence the visual. This intent is strictly for generating images that depend on chat history, including summaries, illustrations, or artwork derived from earlier messages, but does not include charts or graphs.")
            .AddPromptProcessingIntent(
                DocumentIntents.GenerateChart,
                "The user wants to create a chart, graph, or data visualization such as bar chart, line chart, pie chart, scatter plot, or histogram. The AI model already receives conversation history, so this intent handles both explicit data in the prompt and references to data from earlier messages.");

        // Register the strategies
        services
            .AddPromptProcessingStrategy<ImageGenerationDocumentProcessingStrategy>()
            .AddPromptProcessingStrategy<ChartGenerationDocumentProcessingStrategy>();

        return services;
    }

    /// <summary>
    /// Adds the default document prompt processing strategies with their intent registrations.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddDefaultDocumentPromptProcessingStrategies(this IServiceCollection services)
    {
        // Register intents for the default strategies
        services
            .AddPromptProcessingIntent(
                DocumentIntents.SummarizeDocument,
                "The user wants a summary, overview, brief, key points, or outline of document content.")
            .AddPromptProcessingIntent(
                DocumentIntents.AnalyzeTabularData,
                "The user wants to perform calculations, aggregations, statistics, or data analysis on tabular data (CSV, Excel, etc.).")
            .AddPromptProcessingIntent(
                DocumentIntents.AnalyzeTabularDataByRow,
                "The user wants row-by-row analysis or extraction from tabular data (CSV/Excel), returning one result per row (e.g., classify each record, extract fields, detect escalations, or produce per-row outputs based on transcript/text columns).")
            .AddPromptProcessingIntent(
                DocumentIntents.ExtractStructuredData,
                "The user wants to extract specific data, parse content into structured formats (JSON, schema), or pull out entities.")
            .AddPromptProcessingIntent(
                DocumentIntents.CompareDocuments,
                "The user wants to compare, contrast, find differences, or analyze similarities between multiple documents.")
            .AddPromptProcessingIntent(
                DocumentIntents.TransformFormat,
                "The user wants to convert, transform, reformat content into another representation (tables, bullet points, different format).")
            .AddPromptProcessingIntent(
                DocumentIntents.GeneralChatWithReference,
                "General conversation that may reference documents but doesn't fit other categories.");

        // Register the strategies
        services
            .AddPromptProcessingStrategy<SummarizationDocumentProcessingStrategy>()
            .AddPromptProcessingStrategy<TabularAnalysisDocumentProcessingStrategy>()
            .AddPromptProcessingStrategy<RowLevelTabularAnalysisDocumentProcessingStrategy>()
            .AddPromptProcessingStrategy<ExtractionDocumentProcessingStrategy>()
            .AddPromptProcessingStrategy<ComparisonDocumentProcessingStrategy>()
            .AddPromptProcessingStrategy<TransformationDocumentProcessingStrategy>()
            .AddPromptProcessingStrategy<ImageGenerationDocumentProcessingStrategy>();

        return services;
    }
}
