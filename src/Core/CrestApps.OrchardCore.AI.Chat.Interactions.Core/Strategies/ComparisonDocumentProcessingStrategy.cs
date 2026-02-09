using CrestApps.OrchardCore.AI.Core.Strategies;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;

/// <summary>
/// Strategy for handling document comparison requests.
/// Provides full content of all documents for comparison analysis.
/// </summary>
public sealed class ComparisonDocumentProcessingStrategy : DocumentProcessingStrategyBase
{
    private const int MaxContextLength = 60000;

    private readonly IChatInteractionDocumentStore _chatInteractionDocumentStore;

    public ComparisonDocumentProcessingStrategy(IChatInteractionDocumentStore chatInteractionDocumentStore)
    {
        _chatInteractionDocumentStore = chatInteractionDocumentStore;
    }

    /// <inheritdoc />
    public override async Task ProcessAsync(IntentProcessingContext context, CancellationToken cancellationToken = default)
    {
        if (!CanHandle(context, DocumentIntents.CompareDocuments) || !HasDocuments(context))
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
                "The following documents are attached for comparison (but could not be read):");
        }
        else
        {
            var prefix = $"The following is the content of {context.DocumentInfos.Count} documents that the user wants to compare. Each document is separated by '---':";
            context.Result.AddContext(documentContent, prefix, usedVectorSearch: false);
        }
    }
}
