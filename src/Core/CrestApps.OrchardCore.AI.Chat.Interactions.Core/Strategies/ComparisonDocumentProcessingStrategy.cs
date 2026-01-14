using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;

/// <summary>
/// Strategy for handling document comparison requests.
/// Provides full content of all documents for comparison analysis.
/// </summary>
public sealed class ComparisonDocumentProcessingStrategy : DocumentProcessingStrategyBase
{
    private const int MaxContextLength = 60000;

    /// <inheritdoc />
    public override int Order => 100;

    /// <inheritdoc />
    public override bool CanHandle(DocumentIntent intent)
    {
        return intent == DocumentIntent.CompareDocuments;
    }

    /// <inheritdoc />
    public override Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingContext context)
    {
        var documentContent = GetCombinedDocumentText(context, MaxContextLength);

        if (string.IsNullOrWhiteSpace(documentContent))
        {
            return Task.FromResult(DocumentProcessingResult.Success(
                GetDocumentMetadata(context),
                "The following documents are attached for comparison (but could not be read):"));
        }

        var prefix = $"The following is the content of {context.Documents.Count} documents that the user wants to compare. Each document is separated by '---':";

        return Task.FromResult(DocumentProcessingResult.Success(
            documentContent,
            prefix,
            usedVectorSearch: false));
    }
}
