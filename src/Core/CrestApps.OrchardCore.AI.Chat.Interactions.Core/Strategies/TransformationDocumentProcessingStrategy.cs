using CrestApps.OrchardCore.AI.Core.Strategies;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;

/// <summary>
/// Strategy for handling document transformation/reformatting requests.
/// Provides full document content with transformation-focused context.
/// </summary>
public sealed class TransformationDocumentProcessingStrategy : DocumentProcessingStrategyBase
{
    private const int MaxContextLength = 50000;

    private readonly IChatInteractionDocumentStore _chatInteractionDocumentStore;

    public TransformationDocumentProcessingStrategy(IChatInteractionDocumentStore chatInteractionDocumentStore)
    {
        _chatInteractionDocumentStore = chatInteractionDocumentStore;
    }

    /// <inheritdoc />
    public override async Task ProcessAsync(IntentProcessingContext context, CancellationToken cancellationToken = default)
    {
        if (!CanHandle(context, DocumentIntents.TransformFormat) || !HasDocuments(context))
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
                "The following documents are attached (but could not be read):");
        }
        else
        {
            var prefix = "The following is the content of the attached document(s). The user wants to transform or reformat this content:";
            context.Result.AddContext(documentContent, prefix, usedVectorSearch: false);
        }
    }
}
