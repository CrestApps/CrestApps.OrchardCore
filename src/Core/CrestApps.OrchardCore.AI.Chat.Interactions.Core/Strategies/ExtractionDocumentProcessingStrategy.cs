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
    public override Task ProcessAsync(IntentProcessingContext context)
    {
        if (!CanHandle(context, DocumentIntents.ExtractStructuredData) ||
            context.Interaction.Documents is null ||
            context.Interaction.Documents.Count == 0)
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
            var prefix = "The following is the content of the attached document(s). The user wants to extract structured data or specific information from this content:";
            context.Result.AddContext(documentContent, prefix, usedVectorSearch: false);
        }

        return Task.CompletedTask;
    }
}
