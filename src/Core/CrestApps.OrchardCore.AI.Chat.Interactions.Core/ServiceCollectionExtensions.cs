using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Strategies;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core;

/// <summary>
/// Extension methods for registering document processing services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the default document prompt processing strategies with their intent registrations.
    /// </summary>
    public static IServiceCollection AddDefaultDocumentPromptProcessingStrategies(this IServiceCollection services)
    {
        // Register the tabular batch processor (used by heavy processing strategies)
        services.TryAddScoped<ITabularBatchProcessor, TabularBatchProcessor>();

        // Register the tabular batch result cache (uses IDistributedCache)
        services.TryAddSingleton<ITabularBatchResultCache, TabularBatchResultCache>();

        // Register intents with their strategies
        services.AddPromptProcessingIntent(
            DocumentIntents.SummarizeDocument,
            "The user wants a summary, overview, brief, key points, or outline of document content.")
            .WithStrategy<SummarizationDocumentProcessingStrategy>();

        services.AddPromptProcessingIntent(
            DocumentIntents.AnalyzeTabularData,
            "The user wants to perform calculations, aggregations, statistics, or data analysis on tabular data (CSV, Excel, etc.).")
            .WithStrategy<TabularAnalysisDocumentProcessingStrategy>();

        services.AddPromptProcessingIntent(
            DocumentIntents.AnalyzeTabularDataByRow,
            "The user wants row-by-row analysis or extraction from tabular data (CSV/Excel), returning one result per row (e.g., classify each record, extract fields, detect escalations, or produce per-row outputs based on transcript/text columns).")
            .WithHeavyStrategy<RowLevelTabularAnalysisDocumentProcessingStrategy>();

        services.AddPromptProcessingIntent(
            DocumentIntents.ExtractStructuredData,
            "The user wants to extract specific data, parse content into structured formats (JSON, schema), or pull out entities.")
            .WithStrategy<ExtractionDocumentProcessingStrategy>();

        services.AddPromptProcessingIntent(
            DocumentIntents.CompareDocuments,
            "The user wants to compare, contrast, find differences, or analyze similarities between multiple documents.")
            .WithStrategy<ComparisonDocumentProcessingStrategy>();

        services.AddPromptProcessingIntent(
            DocumentIntents.TransformFormat,
            "The user wants to convert, transform, reformat content into another representation (tables, bullet points, different format).")
            .WithStrategy<TransformationDocumentProcessingStrategy>();

        services.AddPromptProcessingIntent(
            DocumentIntents.GeneralChatWithReference,
            "General conversation that may reference documents but doesn't fit other categories.");

        // ImageGenerationDocumentProcessingStrategy is also registered here since it
        // handles document-based image generation in the interactions pipeline.
        services.AddPromptProcessingStrategy<ImageGenerationDocumentProcessingStrategy>();

        return services;
    }
}
