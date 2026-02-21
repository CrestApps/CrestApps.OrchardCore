using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Tools;
using CrestApps.OrchardCore.AI.Core.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core;

/// <summary>
/// Extension methods for registering document processing services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the default document processing system tools and supporting services.
    /// </summary>
    public static IServiceCollection AddDefaultDocumentProcessingServices(this IServiceCollection services)
    {
        // Register the tabular batch processor (used by heavy processing tools)
        services.TryAddScoped<ITabularBatchProcessor, TabularBatchProcessor>();

        // Register the tabular batch result cache (uses IDistributedCache)
        services.TryAddSingleton<ITabularBatchResultCache, TabularBatchResultCache>();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOrchestrationContextBuilderHandler, DocumentOrchestrationHandler>());

        // Register document system tools (available when documents are attached).
        services.AddAITool<ListDocumentsTool>(ListDocumentsTool.TheName)
            .WithTitle("List Documents")
            .WithDescription("Lists all documents attached to the current chat session.")
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
