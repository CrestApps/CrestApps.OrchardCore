using CrestApps.AI.Chat.Handlers;
using CrestApps.AI.Chat.Services;
using CrestApps.AI.Chat.Tools;
using CrestApps.AI.Handlers;
using CrestApps.AI.Models;
using CrestApps.AI.Prompting.Extensions;
using CrestApps.Services;
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
        // Register AI templates embedded in this assembly.
        services.AddAITemplatesFromAssembly(typeof(ServiceCollectionExtensions).Assembly);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAICompletionContextBuilderHandler, ChatInteractionCompletionContextBuilderHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<ChatInteraction>, ChatInteractionEntryHandler>());

        return services;
    }

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
