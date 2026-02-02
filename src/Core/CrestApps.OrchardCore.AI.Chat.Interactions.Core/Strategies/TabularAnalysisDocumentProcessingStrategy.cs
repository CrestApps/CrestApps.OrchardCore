using System.Text;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;

/// <summary>
/// Strategy for handling tabular data analysis (CSV, Excel, etc.).
/// Parses structured data and provides it in a format suitable for analysis.
/// </summary>
public sealed class TabularAnalysisDocumentProcessingStrategy : DocumentProcessingStrategyBase
{
    private static readonly string[] _tabularExtensions = [".csv", ".tsv", ".xlsx", ".xls"];

    private readonly IChatInteractionDocumentStore _chatInteractionDocumentStore;

    // Maximum rows to include in context
    private const int MaxRows = 100;

    public TabularAnalysisDocumentProcessingStrategy(IChatInteractionDocumentStore chatInteractionDocumentStore)
    {
        _chatInteractionDocumentStore = chatInteractionDocumentStore;
    }

    /// <inheritdoc />
    public override async Task ProcessAsync(IntentProcessingContext context)
    {
        if (!CanHandle(context, DocumentIntents.AnalyzeTabularData) ||
            context.Interaction.Documents is null ||
            context.Interaction.Documents.Count == 0)
        {
            return;
        }

        var tabularDocuments = await GetTabularDocumentsAsync(context.Interaction.Documents);

        if (tabularDocuments.Count == 0)
        {
            // Fallback to full document content if no tabular files found
            var allContent = GetCombinedDocumentText(context);
            context.Result.AddContext(
                allContent,
                "The following is the content of the attached documents for analysis:",
                usedVectorSearch: false);

            return;
        }

        var builder = new StringBuilder();
        var processedCount = 0;

        foreach (var doc in tabularDocuments)
        {
            if (string.IsNullOrWhiteSpace(doc.Text))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("---");
                builder.AppendLine();
            }

            builder.AppendLine($"[Data from: {doc.FileName ?? "Unknown file"}]");

            // For CSV/TSV content, try to limit rows if it's very large
            var content = LimitTabularRows(doc.Text, MaxRows);
            builder.Append(content);
            processedCount++;
        }

        if (builder.Length == 0)
        {
            context.Result.AddContext(
                GetDocumentMetadata(context),
                "Tabular files are attached but could not be read:",
                usedVectorSearch: false);

            return;
        }

        var prefix = processedCount == 1
            ? "The following is tabular data from the attached file for analysis. The data is in a structured format (e.g., CSV/TSV):"
            : $"The following is tabular data from {processedCount} attached files for analysis:";

        context.Result.AddContext(builder.ToString(), prefix, usedVectorSearch: false);
    }

    private async Task<IReadOnlyCollection<ChatInteractionDocument>> GetTabularDocumentsAsync(IList<ChatInteractionDocumentInfo> documents)
    {
        if (documents == null || documents.Count == 0)
        {
            return [];
        }

        var tabularDocumentIds = documents.Where(doc => IsTabularFile(doc.FileName)).Select(doc => doc.DocumentId);

        return await _chatInteractionDocumentStore.GetAsync(tabularDocumentIds);
    }

    private static bool IsTabularFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        foreach (var ext in _tabularExtensions)
        {
            if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string LimitTabularRows(string content, int maxRows)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        var lines = content.Split('\n');
        if (lines.Length <= maxRows + 1) // +1 for header row
        {
            return content;
        }

        // Take header + first N rows + truncation message
        // lines.Length > maxRows + 1, so we have at least maxRows + 2 elements
        var limitedLines = new string[maxRows + 2]; // +1 for header, +1 for truncation message
        for (var i = 0; i <= maxRows && i < lines.Length; i++)
        {
            limitedLines[i] = lines[i];
        }
        limitedLines[maxRows + 1] = $"... (truncated, showing first {maxRows} of {lines.Length - 1} data rows)";

        return string.Join('\n', limitedLines);
    }
}
