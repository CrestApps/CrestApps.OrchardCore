using System.Text;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;

public sealed class RowLevelTabularAnalysisDocumentProcessingStrategy : DocumentProcessingStrategyBase
{
    private static readonly string[] _tabularExtensions = [".csv", ".tsv", ".xlsx", ".xls"];

    private const int MaxRows = 250;

    public override Task ProcessAsync(IntentProcessingContext context)
    {
        if (!CanHandle(context, DocumentIntents.AnalyzeTabularDataByRow) ||
            context.Interaction.Documents is null ||
            context.Interaction.Documents.Count == 0)
        {
            return Task.CompletedTask;
        }

        var tabularDocuments = GetTabularDocuments(context.Interaction.Documents);

        if (tabularDocuments.Count == 0)
        {
            var allContent = GetCombinedDocumentText(context);
            context.Result.AddContext(
                allContent,
                "The following is the content of the attached documents for row-level analysis:",
                usedVectorSearch: false);
            return Task.CompletedTask;
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

            builder.AppendLine($"[Tabular data from: {doc.FileName ?? "Unknown file"}]");

            var content = LimitTabularRows(doc.Text, MaxRows);
            builder.AppendLine(content);
            processedCount++;
        }

        if (builder.Length == 0)
        {
            context.Result.AddContext(
                GetDocumentMetadata(context),
                "Tabular files are attached but could not be read:",
                usedVectorSearch: false);
            return Task.CompletedTask;
        }

        context.Result.AddContext(
            GetRowLevelInstructions(context.Prompt),
            "Follow these instructions when responding:",
            usedVectorSearch: false);

        var prefix = processedCount == 1
            ? "The following is tabular data for row-by-row processing. Treat each data row as an independent record and preserve verbatim quotes when requested:"
            : $"The following is tabular data from {processedCount} attached files for row-by-row processing:";

        context.Result.AddContext(builder.ToString(), prefix, usedVectorSearch: false);

        return Task.CompletedTask;
    }

    private static string GetRowLevelInstructions(string prompt)
    {
        var builder = new StringBuilder();

        builder.AppendLine("You are performing row-level analysis over tabular data.");
        builder.AppendLine("1) Treat the first row as headers.");
        builder.AppendLine("2) Process each subsequent row independently.");
        builder.AppendLine("3) Output exactly one result per input row.");
        builder.AppendLine("4) Preserve verbatim excerpts when the prompt asks for exact quotes.");
        builder.AppendLine("5) If the requested item does not exist in a row, output \"No Escalation found\" (or equivalent explicitly requested by the user).");
        builder.AppendLine("6) Keep output in a compact tabular format aligned to the input (e.g., CSV/Markdown table). Include a stable row identifier when available (e.g., Eureka ID)." );

        if (!string.IsNullOrWhiteSpace(prompt))
        {
            builder.AppendLine();
            builder.AppendLine("User request:");
            builder.AppendLine(prompt);
        }

        return builder.ToString();
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
        if (lines.Length <= maxRows + 1)
        {
            return content;
        }

        var limitedLines = new string[maxRows + 2];
        for (var i = 0; i <= maxRows && i < lines.Length; i++)
        {
            limitedLines[i] = lines[i];
        }

        limitedLines[maxRows + 1] = $"... (truncated, showing first {maxRows} of {lines.Length - 1} data rows)";
        return string.Join('\n', limitedLines);
    }
}
