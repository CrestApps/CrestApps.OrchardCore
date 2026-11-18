using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContentTransfer.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.ContentTransfer.AI;

/// <summary>
/// Returns one import or export entry with its progress and failed rows. An import needs the import permission for its
/// content type, as the import's pause, resume and error download actions do; an export must be the caller's own, as
/// the export dashboard and its download do. A completed export links to its download, which checks the same again.
/// </summary>
public sealed class GetContentTransferEntryTool : ContentTransferAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getContentTransferEntry";

    /// <summary>
    /// The most failed rows the result lists with their messages.
    /// </summary>
    public const int MaxErrors = 50;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "entryId": {
          "type": "string",
          "description": "The ID of the import or export entry, from listContentTransferEntries."
        }
      },
      "required": ["entryId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one import or export entry with its progress: total rows, processed, imported, failed rows with their error messages (the first 50), and the download link of a completed export. Use it to report progress or explain failures.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var entryId = ReadString(arguments, "entryId");

        if (entryId is null)
        {
            return "Give the ID of the entry in entryId.";
        }

        var entry = await FindEntryAsync(services, entryId, cancellationToken);

        if (entry is null)
        {
            return $"No content transfer entry has the ID '{entryId}'. Call listContentTransferEntries to find it.";
        }

        if (entry.Direction == ContentTransferDirection.Import)
        {
            if (!await IsAuthorizedAsync(services, user, ContentTransferPermissions.ImportContentFromFile, entry.ContentType))
            {
                return NotAllowedMessage;
            }
        }
        else
        {
            if (!await IsAuthorizedAsync(services, user, ContentTransferPermissions.ExportContentFromFile))
            {
                return NotAllowedMessage;
            }

            // Another user's export is not shown, as on the export dashboard.
            if (!string.Equals(entry.Owner, GetUserId(user), StringComparison.Ordinal))
            {
                return $"No content transfer entry has the ID '{entryId}'. Call listContentTransferEntries to find it.";
            }
        }

        entry.TryGet<ImportFileProcessStatsPart>(out var stats);

        var errorRows = stats?.Errors?.OrderBy(row => row).ToArray() ?? [];
        var categoryByRow = new Dictionary<int, string>();

        foreach (var (category, rows) in stats?.ErrorCategories ?? new Dictionary<string, HashSet<int>>())
        {
            foreach (var row in rows ?? [])
            {
                categoryByRow[row] = category;
            }
        }

        var errors = errorRows
            .Take(MaxErrors)
            .Select(row => new
            {
                row,
                message = stats.ErrorMessages?.GetValueOrDefault(row) ?? categoryByRow.GetValueOrDefault(row),
            })
            .ToArray();

        var entryValues = new RouteValueDictionary { ["entryId"] = entry.EntryId };
        var isImport = entry.Direction == ContentTransferDirection.Import;

        return Json(new
        {
            entryId = entry.EntryId,
            direction = entry.Direction.ToString(),
            contentType = entry.ContentType,
            fileName = entry.UploadedFileName,
            status = entry.Status.NormalizeImportStatus().ToString(),
            author = entry.Author,
            created = await ToLocalAsync(services, entry.CreatedUtc),
            lastProgress = await ToLocalAsync(services, entry.ProcessSaveUtc),
            completed = await ToLocalAsync(services, entry.CompletedUtc),
            error = entry.Error,
            progress = stats is null ? null : new
            {
                totalRows = stats.TotalRecords,
                processedRows = stats.TotalProcessed,
                importedRows = stats.ImportedCount,
                failedRows = errorRows.Length,
                currentRow = stats.CurrentRow,
            },
            errorCategories = stats?.ErrorCategories?.ToDictionary(category => category.Key, category => category.Value?.Count ?? 0),
            errors,
            errorsTruncated = errorRows.Length > errors.Length,
            canResume = isImport && entry.Status.CanResumeImport(),
            canPause = isImport && entry.Status == ContentTransferEntryStatus.Processing,
            downloadPath = !isImport && entry.Status == ContentTransferEntryStatus.Completed
                ? GetAdminPath(services, "DownloadExportFile", entryValues)
                : null,
            errorsDownloadPath = isImport && errorRows.Length > 0
                ? GetAdminPath(services, "DownloadErrors", entryValues)
                : null,
        });
    }
}
