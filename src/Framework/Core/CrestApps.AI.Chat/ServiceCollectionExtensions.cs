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
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.AI.Chat;

/// <summary>
/// Extension methods for registering chat and document processing services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the default chat notification sender and built-in notification action handlers.
    /// The sender dispatches notifications to keyed <see cref="IChatNotificationTransport"/>
    /// implementations, which must be registered separately by each host (OrchardCore, MVC, etc.).
    /// </summary>
    public static IServiceCollection AddChatNotificationServices(this IServiceCollection services)
    {
        services.TryAddScoped<IChatNotificationSender, DefaultChatNotificationSender>();
        services.TryAddKeyedScoped<IChatNotificationActionHandler, CancelTransferNotificationActionHandler>(ChatNotificationActionNames.CancelTransfer);
        services.TryAddKeyedScoped<IChatNotificationActionHandler, EndSessionNotificationActionHandler>(ChatNotificationActionNames.EndSession);

        return services;
    }

    /// <summary>
    /// Configures standard hub options (timeouts, message sizes) for a chat hub.
    /// Call this for each concrete hub type that handles AI chat traffic.
    /// </summary>
    public static IServiceCollection ConfigureChatHubOptions<THub>(this IServiceCollection services) where THub : Hub
    {
        services.Configure<HubOptions<THub>>(options =>
        {
            // Allow long-running operations (e.g., multi-step MCP tool calls)
            // without the server dropping the connection prematurely.
            options.ClientTimeoutInterval = TimeSpan.FromMinutes(10);
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);

            // Allow larger messages for audio transcription payloads.
            options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
        });

        return services;
    }

    /// <summary>
    /// Adds the default chat interaction handlers.
    /// </summary>
    public static IServiceCollection AddChatInteractionServices(this IServiceCollection services)
    {
        services.AddChatNotificationServices();

        // Register templates embedded in this assembly.
        services.AddTemplatesFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAICompletionContextBuilderHandler, ChatInteractionCompletionContextBuilderHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IChatInteractionSettingsHandler, DataSourceChatInteractionSettingsHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IChatInteractionSettingsHandler, PromptTemplateChatInteractionSettingsHandler>());
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
