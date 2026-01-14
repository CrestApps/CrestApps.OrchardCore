using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;

/// <summary>
/// Strategy for handling general chat with document reference.
/// Provides document metadata and limited content for general reference.
/// This is typically the fallback strategy when no other strategy handles the intent.
/// </summary>
public sealed class GeneralReferenceDocumentProcessingStrategy : DocumentProcessingStrategyBase
{
    private const int MaxContextLength = 30000;

    /// <inheritdoc />
    public override Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingContext context)
    {
        if (!string.Equals(context.IntentResult?.Intent, DocumentIntents.GeneralChatWithReference, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(DocumentProcessingResult.NotHandled());
        }

        var documentContent = GetCombinedDocumentText(context, MaxContextLength);

        if (string.IsNullOrWhiteSpace(documentContent))
        {
            return Task.FromResult(DocumentProcessingResult.Success(
                GetDocumentMetadata(context),
                "The following documents are attached for reference:"));
        }

        var prefix = "The following documents are attached for reference. Use this information if relevant to the user's request:";

        return Task.FromResult(DocumentProcessingResult.Success(
            documentContent,
            prefix,
            usedVectorSearch: false));
    }
}
