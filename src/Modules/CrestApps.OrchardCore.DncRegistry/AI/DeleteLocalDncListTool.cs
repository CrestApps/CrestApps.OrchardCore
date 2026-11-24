using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DncRegistry.AI;

/// <summary>
/// Deletes a local do-not-call list and all its numbers in the background, as the Delete action of the Local DNC
/// Registry page does: the list is marked as being deleted at once and its numbers are removed after the turn.
/// </summary>
internal sealed class DeleteLocalDncListTool : DncToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteLocalDncList";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "listId": {
          "type": "string",
          "description": "The list to delete, by id (see listLocalDncLists)."
        }
      },
      "required": ["listId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a local do-not-call list and all its numbers in the background. Those numbers can be dialed again afterwards, so this weakens compliance screening and cannot be undone.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => DncRegistryPermissions.ManageDncRegistrySettings;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(Arguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var listId = arguments.GetString("listId");

        if (listId is null)
        {
            return "Nothing was deleted: give the listId.";
        }

        var manager = services.GetRequiredService<ILocalDncListManager>();
        var list = await manager.FindByIdAsync(listId, cancellationToken);

        if (list is null)
        {
            return $"Nothing was deleted: no local do-not-call list has the id '{listId}'. Use listLocalDncLists to find it.";
        }

        if (list.Status == LocalDncListStatus.Deleting)
        {
            return $"The list '{list.Name}' is already being deleted.";
        }

        await manager.MarkAsDeletingAsync(listId, cancellationToken);

        var queued = LocalDncListJobs.QueueDelete(listId);

        return Json(new
        {
            deleting = new
            {
                id = list.ListId,
                name = list.Name,
                countryCode = list.CountryCode,
                phoneNumberCount = list.PhoneNumberCount,
            },
            note = queued
                ? "The list is marked as being deleted and its numbers are no longer screened from now on; it and its numbers are removed in the background when this reply ends."
                : "The list is marked as being deleted and its numbers are no longer screened from now on; the background task removes it and its numbers within about 20 minutes.",
        });
    }
}
