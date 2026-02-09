using CrestApps.OrchardCore.AI.Core.Strategies;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;

/// <summary>
/// Strategy for handling general chat with document reference.
/// Provides document metadata and limited content for general reference.
/// Handles the <see cref="DocumentIntents.GeneralChatWithReference"/> intent.
/// </summary>
public sealed class GeneralReferenceDocumentProcessingStrategy : DocumentProcessingStrategyBase
{
    private const int MaxContextLength = 30000;

    private readonly IChatInteractionDocumentStore _chatInteractionDocumentStore;

    public GeneralReferenceDocumentProcessingStrategy(IChatInteractionDocumentStore chatInteractionDocumentStore)
    {
        _chatInteractionDocumentStore = chatInteractionDocumentStore;
    }

    /// <inheritdoc />
    public override async Task ProcessAsync(IntentProcessingContext context, CancellationToken cancellationToken = default)
    {
        if (!CanHandle(context, DocumentIntents.GeneralChatWithReference) || !HasDocuments(context))
        {
            return;
        }

        // Load full documents if not already loaded
        if (!HasDocumentContent(context))
        {
            var documentIds = context.DocumentInfos.Select(d => d.DocumentId);
            context.Documents = (await _chatInteractionDocumentStore.GetAsync(documentIds)).ToList();
        }

        var documentContent = GetCombinedDocumentText(context, MaxContextLength);

        if (string.IsNullOrWhiteSpace(documentContent))
        {
            context.Result.AddContext(
                GetDocumentMetadata(context),
                "The following documents are attached for reference:");
        }
        else
        {
            var prefix = "The following documents are attached for reference. Use this information if relevant to the user's request:";
            context.Result.AddContext(documentContent, prefix, usedVectorSearch: false);
        }
    }
}
