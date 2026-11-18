using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContentTransfer.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.ContentTransfer.AI;

/// <summary>
/// Lists bulk import entries, as the Content › Import list does, or the caller's own export entries, as the export
/// dashboard does, through the same query service and filter terms.
/// </summary>
public sealed class ListContentTransferEntriesTool : ContentTransferAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listContentTransferEntries";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "direction": {
          "type": "string",
          "enum": ["Import", "Export"],
          "description": "Import lists every bulk import; Export lists the user's own exports. Defaults to Import."
        },
        "status": {
          "type": "string",
          "enum": ["Pending", "Processing", "Completed", "CompletedWithErrors", "Paused", "Deleting", "Failed"],
          "description": "Only the entries with this status."
        },
        "page": {
          "type": "integer",
          "description": "The page to return, from 1. Defaults to 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "How many entries a page holds, from 1 to 50. Defaults to 20."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists bulk import entries, or the user's own export entries, with content type, file name, status, progress, who started them and when, filtered by status, newest first, with paging.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var directionText = ReadString(arguments, "direction");
        var direction = ContentTransferDirection.Import;

        if (directionText is not null && (int.TryParse(directionText, out _) || !Enum.TryParse(directionText, ignoreCase: true, out direction)))
        {
            return "direction must be Import or Export.";
        }

        var statusText = ReadString(arguments, "status");

        if (statusText is not null && (int.TryParse(statusText, out _) || !Enum.TryParse<ContentTransferEntryStatus>(statusText, ignoreCase: true, out _)))
        {
            return "status must be Pending, Processing, Completed, CompletedWithErrors, Paused, Deleting or Failed.";
        }

        // The import list asks for "List content transfer entries"; the export list shows the user's own exports to
        // anyone who may export.
        var permission = direction == ContentTransferDirection.Import
            ? ContentTransferPermissions.ListContentTransferEntries
            : ContentTransferPermissions.ExportContentFromFile;

        if (!await IsAuthorizedAsync(services, user, permission))
        {
            return NotAllowedMessage;
        }

        var (page, pageSize) = ReadPaging(arguments);
        var parser = services.GetRequiredService<IContentTransferEntryAdminListFilterParser>();
        var options = new ListContentTransferEntryOptions
        {
            Direction = direction,
            Owner = direction == ContentTransferDirection.Export ? GetUserId(user) : null,
            FilterResult = parser.Parse(statusText is null ? string.Empty : $"status:{statusText}"),
        };

        if (direction == ContentTransferDirection.Export && string.IsNullOrEmpty(options.Owner))
        {
            return NotAllowedMessage;
        }

        var result = await services.GetRequiredService<IContentTransferEntryAdminListQueryService>().QueryAsync(page, pageSize, options, updater: null);
        var items = new List<object>();

        foreach (var entry in result.Entries ?? [])
        {
            items.Add(await DescribeAsync(services, entry));
        }

        return Json(new
        {
            items,
            total = result.TotalCount,
            page,
            pageSize,
            truncated = page * pageSize < result.TotalCount,
        });
    }

    private static async Task<object> DescribeAsync(IServiceProvider services, ContentTransferEntry entry)
    {
        entry.TryGet<ImportFileProcessStatsPart>(out var stats);

        return new
        {
            entryId = entry.EntryId,
            direction = entry.Direction.ToString(),
            contentType = entry.ContentType,
            fileName = entry.UploadedFileName,
            status = entry.Status.NormalizeImportStatus().ToString(),
            author = entry.Author,
            created = await ToLocalAsync(services, entry.CreatedUtc),
            completed = await ToLocalAsync(services, entry.CompletedUtc),
            totalRows = stats?.TotalRecords,
            processedRows = stats?.TotalProcessed,
            importedRows = stats?.ImportedCount,
            failedRows = stats?.Errors?.Count,
            error = entry.Error,
        };
    }
}
