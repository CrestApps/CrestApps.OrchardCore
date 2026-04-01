using CrestApps.AI.Chat.Handlers;
using CrestApps.AI.Chat.Services;
using CrestApps.AI.Chat.Tools;
using CrestApps.AI.Completions;
using CrestApps.AI.Handlers;
using CrestApps.AI.Models;
using CrestApps.AI.Orchestration;
using CrestApps.AI.Tooling;
using CrestApps.Services;

using CrestApps.Templates.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.AI.Chat;

/// <summary>
/// Extension methods for registering document processing services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the default chat interaction handlers.
    /// </summary>
    public static IServiceCollection AddChatInteractionHandlers(this IServiceCollection services)
    {
        // Register templates embedded in this assembly.

        services.AddTemplatesFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAICompletionContextBuilderHandler, ChatInteractionCompletionContextBuilderHandler>());

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<ChatInteraction>, ChatInteractionEntryHandler>());

        return services;
    }
    /// <summary>
    /// Adds the default document processing system tools and supporting services.
    /// </summary>

    public static IServiceCollection AddDefaultDocumentProcessingServices(this IServiceCollection services)
    {

        services.TryAddScoped<IAIDocumentProcessingService, DefaultAIDocumentProcessingService>();

        // Register the tabular batch processor (used by heavy processing tools)

        services.TryAddScoped<ITabularBatchProcessor, TabularBatchProcessor>();

        // Register the tabular batch result cache (uses IDistributedCache)
        services.TryAddSingleton<ITabularBatchResultCache, TabularBatchResultCache>();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOrchestrationContextBuilderHandler, DocumentOrchestrationHandler>());

        services.AddIngestionDocumentReader<PlainTextIngestionDocumentReader>(
            ".txt",
            new ExtractorExtension(".csv", false),
        ".md",
        ".json",
        ".xml",
        ".html",
        ".htm",
        ".log",
        ".yaml",

        ".yml");
        services.AddIngestionDocumentReader<OpenXmlIngestionDocumentReader>(".docx", new ExtractorExtension(".xlsx", false), ".pptx");
        services.AddIngestionDocumentReader<PdfIngestionDocumentReader>(".pdf");

        // Register document system tools (available when documents are attached).
        services.AddAITool<SearchDocumentsTool>(SearchDocumentsTool.TheName)

            .WithTitle("Search Documents")
            .WithDescription("Searches uploaded or attached documents using semantic vector search.")
            .WithPurpose(AIToolPurposes.DocumentProcessing);

        services.AddAITool<ReadDocumentTool>(ReadDocumentTool.TheName)

            .WithTitle("Read Document")
            .WithDescription("Reads the full text content of a specific document.")
            .WithPurpose(AIToolPurposes.DocumentProcessing);

        services.AddAITool<ReadTabularDataTool>(ReadTabularDataTool.TheName)

            .WithTitle("Read Tabular Data")
            .WithDescription("Reads and parses tabular data (CSV, TSV, Excel) from a document.")
            .WithPurpose(AIToolPurposes.DocumentProcessing);

        return services;
    }
}
