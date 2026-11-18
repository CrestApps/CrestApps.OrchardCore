using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContentTransfer.AI;

/// <summary>
/// Pauses a running import or resumes a pending, paused or failed one, through the entry manager the Pause and Resume
/// actions of the Import list call, with the import permission for the entry's content type. A resumed import is picked
/// up by the Imported Files Processor, which runs every minute. An import still processing, whether running or stalled,
/// is not resumed here: telling the two apart takes the import's lock, so the Import list does it.
/// </summary>
public sealed class SetContentImportStateTool : ContentTransferAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setContentImportState";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "entryId": {
          "type": "string",
          "description": "The ID of the import entry, from listContentTransferEntries."
        },
        "state": {
          "type": "string",
          "enum": ["Paused", "Resumed"],
          "description": "Paused stops a processing import after its current row; Resumed continues a pending, paused or failed import from its last saved row."
        }
      },
      "required": ["entryId", "state"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Pauses a running import or resumes a pending, paused or failed one. A resumed import continues from its last saved row within about a minute. An import that looks stalled is resumed from the Import list.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var entryId = ReadString(arguments, "entryId");
        var state = ReadString(arguments, "state");

        if (entryId is null)
        {
            return "Nothing was changed: give the ID of the import entry in entryId.";
        }

        var pause = string.Equals(state, "Paused", StringComparison.OrdinalIgnoreCase);

        if (!pause && !string.Equals(state, "Resumed", StringComparison.OrdinalIgnoreCase))
        {
            return "Nothing was changed: state must be Paused or Resumed.";
        }

        var entry = await FindEntryAsync(services, entryId, cancellationToken);

        if (entry is null || entry.Direction != ContentTransferDirection.Import)
        {
            return $"Nothing was changed: no import entry has the ID '{entryId}'. Call listContentTransferEntries to find it.";
        }

        if (!await IsAuthorizedAsync(services, user, ContentTransferPermissions.ImportContentFromFile, entry.ContentType))
        {
            return NotAllowedMessage;
        }

        var manager = services.GetRequiredService<IContentTransferEntryManager>();
        string message;

        if (pause)
        {
            if (entry.Status != ContentTransferEntryStatus.Processing)
            {
                return $"Nothing was changed: only processing imports can be paused, and this one is {entry.Status.NormalizeImportStatus()}.";
            }

            await manager.PauseImportAsync(entry.EntryId, cancellationToken);
            message = "The import was paused. It stops after the row it is working on.";
        }
        else
        {
            if (entry.Status == ContentTransferEntryStatus.Processing)
            {
                return $"Nothing was changed: the import is processing. If it has made no progress for more than 10 minutes, a person can resume it from the Import list, which can tell a running import from a stalled one: {GetAdminPath(services, "List")}";
            }

            if (!entry.Status.CanResumeImport())
            {
                return $"Nothing was changed: only pending, paused or failed imports can be resumed, and this one is {entry.Status.NormalizeImportStatus()}.";
            }

            await manager.ResumeImportAsync(entry.EntryId, cancellationToken);
            message = "The import was resumed and continues from its last saved row: the Imported Files Processor picks it up within about a minute. Call getContentTransferEntry later to follow it.";
        }

        var current = await FindEntryAsync(services, entry.EntryId, cancellationToken) ?? entry;

        return Json(new
        {
            entryId = current.EntryId,
            contentType = current.ContentType,
            fileName = current.UploadedFileName,
            status = current.Status.NormalizeImportStatus().ToString(),
            message,
            listPath = GetAdminPath(services, "List", new RouteValueDictionary()),
        });
    }
}
