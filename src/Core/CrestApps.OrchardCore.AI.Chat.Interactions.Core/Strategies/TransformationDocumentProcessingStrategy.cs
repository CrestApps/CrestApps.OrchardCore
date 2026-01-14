using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;

/// <summary>
/// Strategy for handling document transformation/reformatting requests.
/// Provides full document content with transformation-focused context.
/// </summary>
public sealed class TransformationDocumentProcessingStrategy : DocumentProcessingStrategyBase
{
    private const int MaxContextLength = 50000;

    /// <inheritdoc />
    public override Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingContext context)
    {
        if (!string.Equals(context.IntentResult?.Intent, DocumentIntents.TransformFormat, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(DocumentProcessingResult.NotHandled());
        }

        var documentContent = GetCombinedDocumentText(context, MaxContextLength);

        if (string.IsNullOrWhiteSpace(documentContent))
        {
            return Task.FromResult(DocumentProcessingResult.Success(
                GetDocumentMetadata(context),
                "The following documents are attached (but could not be read):"));
        }

        var prefix = "The following is the content of the attached document(s). The user wants to transform or reformat this content:";

        return Task.FromResult(DocumentProcessingResult.Success(
            documentContent,
            prefix,
            usedVectorSearch: false));
    }
}
