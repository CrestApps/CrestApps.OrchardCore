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
    public override Task ProcessAsync(DocumentProcessingContext context)
    {
        if (!CanHandle(context, DocumentIntents.SummarizeDocument))
        {
            return Task.CompletedTask;
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
            var prefix = context.Documents.Count == 1
                ? "The following is the content of the attached document that the user wants summarized:"
                : $"The following is the content of {context.Documents.Count} attached documents that the user wants summarized:";
            context.Result.AddContext(documentContent, prefix, usedVectorSearch: false);
        }

        return Task.CompletedTask;
    }
}
