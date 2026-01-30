using System.Text;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;

/// <summary>
/// Base class for document processing strategies that provides common functionality.
/// </summary>
public abstract class DocumentProcessingStrategyBase : IPromptProcessingStrategy
{
    /// <inheritdoc />
    public abstract Task ProcessAsync(IntentProcessingContext context);

    /// <summary>
    /// Gets the combined text content from all documents.
    /// </summary>
    protected static string GetCombinedDocumentText(IntentProcessingContext context, int? maxLength = null)
    {
        if (context.Documents == null || context.Documents.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var totalLength = 0;

        foreach (var doc in context.Documents)
        {
            if (string.IsNullOrWhiteSpace(doc.Text))
            {
                continue;
            }

            if (maxLength.HasValue && totalLength >= maxLength.Value)
            {
                break;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("---");
                builder.AppendLine();
            }

            // Add document header
            if (!string.IsNullOrEmpty(doc.FileName))
            {
                builder.AppendLine($"[Document: {doc.FileName}]");
            }

            var textToAdd = doc.Text;
            if (maxLength.HasValue)
            {
                var remainingSpace = maxLength.Value - totalLength;
                if (remainingSpace <= 0)
                {
                    break;
                }

                if (textToAdd.Length > remainingSpace)
                {
                    textToAdd = textToAdd.Substring(0, remainingSpace);
                }
            }

            builder.Append(textToAdd);
            totalLength += textToAdd.Length;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Gets document metadata for context.
    /// </summary>
    protected static string GetDocumentMetadata(IntentProcessingContext context)
    {
        // Use interaction.Documents for metadata (file info) since that's always available
        if (context.Interaction?.Documents == null || context.Interaction.Documents.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Attached documents:");

        foreach (var doc in context.Interaction.Documents)
        {
            builder.AppendLine($"- {doc.FileName ?? "Unknown"} ({FormatFileSize(doc.FileSize)}, {doc.ContentType ?? "unknown type"})");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Checks if there are documents attached to the interaction.
    /// </summary>
    protected static bool HasDocuments(IntentProcessingContext context)
        => context.Interaction?.Documents != null && context.Interaction.Documents.Count > 0;

    /// <summary>
    /// Checks if full document content has been loaded into the context.
    /// </summary>
    protected static bool HasDocumentContent(IntentProcessingContext context)
        => context.Documents != null && context.Documents.Count > 0;

    protected static bool CanHandle(IntentProcessingContext context, string intent)
        => string.Equals(context.Result?.Intent, intent, StringComparison.OrdinalIgnoreCase);

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }
        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:F1} KB";
        }
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
