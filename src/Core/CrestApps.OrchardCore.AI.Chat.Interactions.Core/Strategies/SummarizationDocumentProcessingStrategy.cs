using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;

/// <summary>
/// Strategy for handling document summarization requests.
/// Bypasses vector search and provides full document content for summarization.
/// </summary>
public sealed class SummarizationDocumentProcessingStrategy : DocumentProcessingStrategyBase
{
    // Maximum characters to include in context to avoid token limits
    private const int MaxContextLength = 50000;

    /// <inheritdoc />
    public override int Order => 100;

    /// <inheritdoc />
    public override bool CanHandle(DocumentIntent intent)
    {
        return intent == DocumentIntent.SummarizeDocument;
    }

    /// <inheritdoc />
    public override Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingContext context)
    {
        var documentContent = GetCombinedDocumentText(context, MaxContextLength);

        if (string.IsNullOrWhiteSpace(documentContent))
        {
            return Task.FromResult(DocumentProcessingResult.Success(
                GetDocumentMetadata(context),
                "The following documents are attached (but could not be read):"));
        }

        var prefix = context.Documents.Count == 1
            ? "The following is the content of the attached document that the user wants summarized:"
            : $"The following is the content of {context.Documents.Count} attached documents that the user wants summarized:";

        return Task.FromResult(DocumentProcessingResult.Success(
            documentContent,
            prefix,
            usedVectorSearch: false));
    }
}
