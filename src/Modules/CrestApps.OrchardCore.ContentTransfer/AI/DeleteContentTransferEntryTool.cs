using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.ContentTransfer.AI;

/// <summary>
/// Deletes an import or export entry and its stored file in the background, as the Delete and Remove actions of the
/// Import and Export lists do: the entry is marked as deleting, which stops a running import, and the entry manager
/// deletes it and its file after the chat's turn. It needs <c>DeleteContentTransferEntries</c> for every entry and, for
/// an export, that the entry is the caller's own, which the single Delete action of the lists does not check.
/// </summary>
public sealed class DeleteContentTransferEntryTool : ContentTransferAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteContentTransferEntry";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "entryId": {
          "type": "string",
          "description": "The ID of the import or export entry to delete, from listContentTransferEntries."
        }
      },
      "required": ["entryId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes an import or export entry and its stored file in the background; a running import stops. Content items already imported are not removed. An export can only be deleted by the user who made it. It cannot be undone.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, ContentTransferPermissions.DeleteContentTransferEntries))
        {
            return NotAllowedMessage;
        }

        var entryId = ReadString(arguments, "entryId");

        if (entryId is null)
        {
            return "Nothing was deleted: give the ID of the entry in entryId.";
        }

        var entry = await FindEntryAsync(services, entryId, cancellationToken);

        // Another user's export is not shown, as on the export dashboard.
        if (entry is null || (entry.Direction == ContentTransferDirection.Export && !string.Equals(entry.Owner, GetUserId(user), StringComparison.Ordinal)))
        {
            return $"Nothing was deleted: no content transfer entry the user can delete has the ID '{entryId}'. Call listContentTransferEntries to find it.";
        }

        // An entry already marked as deleting is queued again, as the list's Delete action does: nothing else sweeps those
        // entries, so a deletion that failed would otherwise leave it for good. Deleting an entry already gone does nothing.
        // The deletion is queued before the entry is marked, so an entry is never left marked as deleting with nothing to
        // delete it: nothing else sweeps those entries.
        if (ShellScope.Current is null)
        {
            return $"Nothing was deleted: the deletion could not be queued here. A person can delete the entry from the list: {GetAdminPath(services, entry.Direction == ContentTransferDirection.Import ? "List" : "ExportDashboard")}";
        }

        var id = entry.EntryId;

        DeferredWork.Add("contenttransfer_delete", scope => scope.ServiceProvider.GetRequiredService<IContentTransferEntryManager>().DeleteAsync(id));

        await services.GetRequiredService<IContentTransferEntryManager>().MarkAsDeletingAsync(id, cancellationToken);

        return Json(new
        {
            entryId = id,
            direction = entry.Direction.ToString(),
            contentType = entry.ContentType,
            fileName = entry.UploadedFileName,
            queued = true,
            message = "The entry is marked as deleting and is deleted with its file in the background after this reply. Content items already imported are not removed.",
        });
    }
}
