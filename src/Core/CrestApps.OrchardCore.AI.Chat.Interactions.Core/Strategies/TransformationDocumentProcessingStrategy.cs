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
    public override Task ProcessAsync(DocumentProcessingContext context)
    {
        if (!CanHandle(context, DocumentIntents.TransformFormat))
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
            var prefix = "The following is the content of the attached document(s). The user wants to transform or reformat this content:";
            context.Result.AddContext(documentContent, prefix, usedVectorSearch: false);
        }

        return Task.CompletedTask;
    }
}
