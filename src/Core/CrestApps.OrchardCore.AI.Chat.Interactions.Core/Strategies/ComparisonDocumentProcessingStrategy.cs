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
    public override Task ProcessAsync(DocumentProcessingContext context)
    {
        if (!string.Equals(context.IntentResult?.Intent, DocumentIntents.CompareDocuments, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
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
            var prefix = $"The following is the content of {context.Documents.Count} documents that the user wants to compare. Each document is separated by '---':";
            context.Result.AddContext(documentContent, prefix, usedVectorSearch: false);
        }

        return Task.CompletedTask;
    }
}
