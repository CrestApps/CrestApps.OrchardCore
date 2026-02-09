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

        // Register intents for the default strategies (including heavy intent)
        services
            .AddPromptProcessingIntent(
                DocumentIntents.SummarizeDocument,
                "The user wants a summary, overview, brief, key points, or outline of document content.")
            .AddPromptProcessingIntent(
                DocumentIntents.AnalyzeTabularData,
                "The user wants to perform calculations, aggregations, statistics, or data analysis on tabular data (CSV, Excel, etc.).")
            .AddHeavyPromptProcessingIntent(
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
