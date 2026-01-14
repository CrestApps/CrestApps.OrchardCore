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
    private static readonly string[] TabularExtensions = [".csv", ".tsv", ".xlsx", ".xls"];

    // Maximum rows to include in context
    private const int MaxRows = 100;

    /// <inheritdoc />
    public override Task<DocumentProcessingResult> ProcessAsync(DocumentProcessingContext context)
    {
        if (!string.Equals(context.IntentResult?.Intent, DocumentIntents.AnalyzeTabularData, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(DocumentProcessingResult.NotHandled());
        }

        var tabularDocuments = GetTabularDocuments(context.Documents);

        if (tabularDocuments.Count == 0)
        {
            // Fallback to full document content if no tabular files found
            var allContent = GetCombinedDocumentText(context);
            return Task.FromResult(DocumentProcessingResult.Success(
                allContent,
                "The following is the content of the attached documents for analysis:",
                usedVectorSearch: false));
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
            return Task.FromResult(DocumentProcessingResult.Success(
                GetDocumentMetadata(context),
                "Tabular files are attached but could not be read:",
                usedVectorSearch: false));
        }

        var prefix = processedCount == 1
            ? "The following is tabular data from the attached file for analysis. The data is in a structured format (e.g., CSV/TSV):"
            : $"The following is tabular data from {processedCount} attached files for analysis:";

        return Task.FromResult(DocumentProcessingResult.Success(
            builder.ToString(),
            prefix,
            usedVectorSearch: false));
    }

    private static List<ChatInteractionDocument> GetTabularDocuments(IList<ChatInteractionDocument> documents)
    {
        var result = new List<ChatInteractionDocument>();

        if (documents == null || documents.Count == 0)
        {
            return result;
        }

        foreach (var doc in documents)
        {
            if (IsTabularFile(doc.FileName))
            {
                result.Add(doc);
            }
        }

        return result;
    }

    private static bool IsTabularFile(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        foreach (var ext in TabularExtensions)
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
