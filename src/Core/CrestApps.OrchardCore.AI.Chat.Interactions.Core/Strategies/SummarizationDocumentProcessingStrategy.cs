using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;

/// <summary>
/// Strategy for handling document summarization requests.
/// Bypasses vector search and provides full document content for summarization.
/// </summary>
public sealed class SummarizationDocumentProcessingStrategy : DocumentProcessingStrategyBase
{
    // Maximum characters to include in context to avoid token limits
    private const int MaxContextLength = 50000;

    private readonly IChatInteractionDocumentStore _chatInteractionDocumentStore;

    public SummarizationDocumentProcessingStrategy(IChatInteractionDocumentStore chatInteractionDocumentStore)
    {
        _chatInteractionDocumentStore = chatInteractionDocumentStore;
    }

    /// <inheritdoc />
    public override async Task ProcessAsync(IntentProcessingContext context)
    {
        if (!CanHandle(context, DocumentIntents.SummarizeDocument) || !HasDocuments(context))
        {
            return;
        }

        // Load full documents if not already loaded
        if (!HasDocumentContent(context))
        {
            var documentIds = context.Interaction.Documents.Select(d => d.DocumentId);
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
            var prefix = context.Interaction.Documents.Count == 1
                ? "The following is the content of the attached document that the user wants summarized:"
                : $"The following is the content of {context.Interaction.Documents.Count} attached documents that the user wants summarized:";
            context.Result.AddContext(documentContent, prefix, usedVectorSearch: false);
        }
    }
}
