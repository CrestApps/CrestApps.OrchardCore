using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Models;
using Cysharp.Text;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Tools;

/// <summary>
/// System tool that reads and parses tabular data (CSV, TSV, Excel) from a document.
/// Returns formatted rows for the LLM to analyze.
/// </summary>
public sealed class ReadTabularDataTool : AIFunction
{
    public const string TheName = SystemToolNames.ReadTabularData;

    private static readonly string[] _tabularExtensions = [".csv", ".tsv", ".xlsx", ".xls"];
    private const int DefaultMaxRows = 100;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "document_id": {
              "type": "string",
              "description": "The unique identifier of the tabular document to read."
            },
            "max_rows": {
              "type": "integer",
              "description": "Maximum number of data rows to return. Defaults to 100."
            }
          },
          "required": ["document_id"],
          "additionalProperties": false
        }
        """);

    public override string Name => TheName;

    public override string Description => "Reads tabular data (CSV, TSV, or Excel) from a document, returning formatted rows suitable for analysis.";

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } =
        new Dictionary<string, object>()
        {
            ["Strict"] = false,
        };

    protected override async ValueTask<object> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        if (!arguments.TryGetFirstString("document_id", out var documentId))
        {
            return "Unable to find a 'document_id' argument in the arguments parameter.";
        }

        var maxRows = arguments.GetFirstValueOrDefault("max_rows", DefaultMaxRows);

        if (maxRows <= 0)
        {
            maxRows = DefaultMaxRows;
        }

        var executionContext = AIInvocationScope.Current?.ToolExecutionContext;

        string referenceId = null;
        HashSet<string> validReferenceIds = null;

        if (executionContext?.Resource is ChatInteraction interaction)
        {
            referenceId = interaction.ItemId;
        }
        else if (executionContext?.Resource is AIProfile profile)
        {
            referenceId = profile.ItemId;
            validReferenceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                profile.ItemId,
            };

            // Documents could be attached at the profile or the session level.
            if (AIInvocationScope.Current?.Items.TryGetValue(nameof(AIChatSession), out var sessionObj) == true &&
                sessionObj is AIChatSession session &&
                session.Documents is { Count: > 0 })
            {
                validReferenceIds.Add(session.SessionId);
            }
        }

        if (string.IsNullOrEmpty(referenceId))
        {
            return "Document access requires an active chat interaction session or AI profile.";
        }

        var documentStore = arguments.Services.GetService<IAIDocumentStore>();

        if (documentStore is null)
        {
            return "Document store is not available.";
        }

        // Query only documents belonging to this resource to prevent cross-session access.
        var document = await documentStore.FindByIdAsync(documentId);

        if (document is null ||
            (validReferenceIds is not null ? !validReferenceIds.Contains(document.ReferenceId) : document.ReferenceId != referenceId))
        {
            return $"Document with ID '{documentId}' was not found.";
        }

        if (string.IsNullOrWhiteSpace(document.Text))
        {
            return $"Document '{document.FileName}' has no extractable text content.";
        }

        if (!IsTabularFile(document.FileName))
        {
            return $"Document '{document.FileName}' is not a recognized tabular format. Use 'read_document' instead.";
        }

        var content = LimitTabularRows(document.Text, maxRows);

        return $"[Tabular data from: {document.FileName}]\n\n{content}";
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

        // +1 for header row.
        if (lines.Length <= maxRows + 1)
        {
            return content;
        }

        using var builder = ZString.CreateStringBuilder();

        for (var i = 0; i <= maxRows && i < lines.Length; i++)
        {
            builder.AppendLine(lines[i]);
        }

        builder.Append("... (truncated, showing first ");
        builder.Append(maxRows);
        builder.Append(" of ");
        builder.Append(lines.Length - 1);
        builder.AppendLine(" data rows)");

        return builder.ToString();
    }
}
