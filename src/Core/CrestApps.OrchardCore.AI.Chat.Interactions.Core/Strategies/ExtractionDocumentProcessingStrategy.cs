using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;

/// <summary>
/// Strategy for handling structured data extraction requests.
/// Provides full document content with extraction-focused context.
/// </summary>
public sealed class ExtractionDocumentProcessingStrategy : DocumentProcessingStrategyBase
{
    private const int MaxContextLength = 50000;

    /// <inheritdoc />
    public override int Order => 100;

    /// <inheritdoc />
    public override bool CanHandle(DocumentIntent intent)
    {
        return intent == DocumentIntent.ExtractStructuredData;
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

        var prefix = "The following is the content of the attached document(s). The user wants to extract structured data or specific information from this content:";

        return Task.FromResult(DocumentProcessingResult.Success(
            documentContent,
            prefix,
            usedVectorSearch: false));
    }
}
