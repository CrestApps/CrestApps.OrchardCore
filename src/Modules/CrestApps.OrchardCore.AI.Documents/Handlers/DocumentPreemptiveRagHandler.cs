using System.Text;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Documents.Handlers;

/// <summary>
/// Preemptive RAG handler for uploaded documents. Receives pre-extracted search queries
/// from the coordinator, embeds them, searches the document vector index, and appends
/// relevant chunks to the system message.
/// </summary>
internal sealed class DocumentPreemptiveRagHandler : IPreemptiveRagHandler
{
    private readonly IAIClientFactory _aiClientFactory;
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public DocumentPreemptiveRagHandler(
        IAIClientFactory aiClientFactory,
        IIndexProfileStore indexProfileStore,
        ISiteService siteService,
        IServiceProvider serviceProvider,
        ILogger<DocumentPreemptiveRagHandler> logger)
    {
        _aiClientFactory = aiClientFactory;
        _indexProfileStore = indexProfileStore;
        _siteService = siteService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleAsync(PreemptiveRagContext context)
    {
        // Only proceed if documents are attached (either on the orchestration context
        // or on the session via AdditionalProperties, since session documents are
        // populated after BuildingAsync via the configure callback).
        if (context.OrchestrationContext.Documents is not { Count: > 0 } &&
            !HasSessionDocuments(context.OrchestrationContext))
        {
            return;
        }

        var settings = await _siteService.GetSettingsAsync<InteractionDocumentSettings>();

        if (string.IsNullOrEmpty(settings.IndexProfileName))
        {
            return;
        }

        try
        {
            await InjectPreemptiveRagContextAsync(context, settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during document Preemptive RAG injection.");
        }
    }

    private async Task InjectPreemptiveRagContextAsync(
        PreemptiveRagContext context,
        InteractionDocumentSettings settings)
    {
        var indexProfile = await _indexProfileStore.FindByNameAsync(settings.IndexProfileName);

        if (indexProfile == null)
        {
            return;
        }

        var searchService = _serviceProvider.GetKeyedService<IVectorSearchService>(indexProfile.ProviderName);

        if (searchService == null)
        {
            return;
        }

        // Get embedding configuration from the index profile metadata.
        var interactionMetadata = indexProfile.As<ChatInteractionIndexProfileMetadata>();

        if (string.IsNullOrEmpty(interactionMetadata?.EmbeddingProviderName) ||
            string.IsNullOrEmpty(interactionMetadata.EmbeddingConnectionName) ||
            string.IsNullOrEmpty(interactionMetadata.EmbeddingDeploymentName))
        {
            return;
        }

        var embeddingGenerator = await _aiClientFactory.CreateEmbeddingGeneratorAsync(
            interactionMetadata.EmbeddingProviderName,
            interactionMetadata.EmbeddingConnectionName,
            interactionMetadata.EmbeddingDeploymentName);

        if (embeddingGenerator == null)
        {
            return;
        }

        // Generate embeddings for all search queries.
        var embeddings = await embeddingGenerator.GenerateAsync(context.Queries);

        if (embeddings == null || embeddings.Count == 0)
        {
            return;
        }

        // Resolve the resource ID and reference type.
        string resourceId = null;
        string referenceType = null;

        if (context.Resource is ChatInteraction interaction)
        {
            resourceId = interaction.ItemId;
            referenceType = AIConstants.DocumentReferenceTypes.ChatInteraction;
        }
        else if (context.Resource is AIProfile profile)
        {
            resourceId = profile.ItemId;
            referenceType = AIConstants.DocumentReferenceTypes.Profile;

            // If the profile has no documents, check for session documents.
            if (context.OrchestrationContext.CompletionContext?.AdditionalProperties is not null &&
                context.OrchestrationContext.CompletionContext.AdditionalProperties.TryGetValue("Session", out var sessionObj) &&
                sessionObj is AIChatSession session &&
                session.Documents is { Count: > 0 })
            {
                resourceId = session.SessionId;
                referenceType = AIConstants.DocumentReferenceTypes.ChatSession;
            }
        }

        if (string.IsNullOrEmpty(resourceId) || string.IsNullOrEmpty(referenceType))
        {
            return;
        }

        var topN = settings.TopN > 0 ? settings.TopN : 3;

        // Perform vector search for each embedding and combine results.
        var allResults = new List<DocumentChunkSearchResult>();
        var seenChunkKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var embedding in embeddings)
        {
            if (embedding?.Vector == null)
            {
                continue;
            }

            var results = await searchService.SearchAsync(
                indexProfile,
                embedding.Vector.ToArray(),
                resourceId,
                referenceType,
                topN);

            if (results == null)
            {
                continue;
            }

            foreach (var result in results)
            {
                if (result.Chunk == null || string.IsNullOrWhiteSpace(result.Chunk.Text))
                {
                    continue;
                }

                // Deduplicate by chunk index to avoid injecting the same chunk multiple times.
                var chunkKey = $"{result.Chunk.Index}";

                if (seenChunkKeys.Add(chunkKey))
                {
                    allResults.Add(result);
                }
            }
        }

        // Sort by score descending and take top N.
        var finalResults = allResults
            .OrderByDescending(r => r.Score)
            .Take(topN)
            .ToList();

        if (finalResults.Count == 0)
        {
            return;
        }

        var orchestrationContext = context.OrchestrationContext;

        // Build context injection.
        var sb = new StringBuilder();
        sb.AppendLine("\n\n[Uploaded Document Context]");
        sb.AppendLine("The following content was retrieved from the user's uploaded documents via semantic search. Use this information to answer the user's question accurately.");
        sb.AppendLine("If the documents do not contain relevant information, use your general knowledge to answer instead.");

        if (!orchestrationContext.DisableTools)
        {
            sb.AppendLine($"If you need additional context, use the '{SystemToolNames.SearchDocuments}' tool to search for more content in the uploaded documents.");
        }

        foreach (var result in finalResults)
        {
            sb.AppendLine("---");
            sb.AppendLine(result.Chunk.Text);
        }

        orchestrationContext.SystemMessageBuilder.Append(sb);
    }

    private static bool HasSessionDocuments(OrchestrationContext orchestrationContext)
    {
        return orchestrationContext.CompletionContext?.AdditionalProperties is not null &&
            orchestrationContext.CompletionContext.AdditionalProperties.TryGetValue("Session", out var sessionObj) &&
            sessionObj is AIChatSession session &&
            session.Documents is { Count: > 0 };
    }
}
