using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;

/// <summary>
/// Strategy for handling general chat with document reference.
/// Provides document metadata and limited content for general reference.
/// Handles the <see cref="DocumentIntents.GeneralChatWithReference"/> intent.
/// </summary>
public sealed class GeneralReferenceDocumentProcessingStrategy : DocumentProcessingStrategyBase
{
    private const int MaxContextLength = 30000;

    /// <inheritdoc />
    public override Task ProcessAsync(IntentProcessingContext context)
    {
        if (!CanHandle(context, DocumentIntents.GeneralChatWithReference) ||
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
                "The following documents are attached for reference:");
        }
        else
        {
            var prefix = "The following documents are attached for reference. Use this information if relevant to the user's request:";
            context.Result.AddContext(documentContent, prefix, usedVectorSearch: false);
        }

        return Task.CompletedTask;
    }
}
