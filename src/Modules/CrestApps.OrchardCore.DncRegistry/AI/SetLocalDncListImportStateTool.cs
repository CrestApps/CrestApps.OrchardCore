using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DncRegistry.AI;

/// <summary>
/// Starts or resumes the import of a local do-not-call list now, or pauses a running import, as the Process now and
/// Pause import actions of the Local DNC Registry page do, with the same state checks.
/// </summary>
internal sealed class SetLocalDncListImportStateTool : DncToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setLocalDncListImportState";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "listId": {
          "type": "string",
          "description": "The list, by id (see listLocalDncLists)."
        },
        "action": {
          "type": "string",
          "enum": ["process", "pause"],
          "description": "process starts or resumes the import of a pending, paused or failed list now; pause stops a running import so it can be resumed later."
        }
      },
      "required": ["listId", "action"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Starts or resumes processing of a queued, paused or failed local do-not-call list now, or pauses a running import. A list screens its numbers only once its import has completed, so a paused list screens none.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => DncRegistryPermissions.ManageDncRegistrySettings;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(Arguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var listId = arguments.GetString("listId");
        var action = arguments.GetString("action")?.ToLowerInvariant();

        if (listId is null || action is not ("process" or "pause"))
        {
            return "Nothing was changed: give the listId and an action, process or pause.";
        }

        var manager = services.GetRequiredService<ILocalDncListManager>();
        var list = await manager.FindByIdAsync(listId, cancellationToken);

        if (list is null)
        {
            return $"Nothing was changed: no local do-not-call list has the id '{listId}'. Use listLocalDncLists to find it.";
        }

        string note;

        if (action == "process")
        {
            if (list.Status is LocalDncListStatus.Completed or LocalDncListStatus.Deleting)
            {
                return $"Nothing was changed: the list '{list.Name}' is {list.Status} and cannot be processed.";
            }

            // The new status is committed at once, so the import that runs after the turn sees it.
            await manager.ResumeImportAsync(listId, cancellationToken);

            note = LocalDncListJobs.QueueImport(listId)
                ? "The import was resumed and continues in the background when this reply ends."
                : "The import was resumed; the background task continues it within about 20 minutes.";
        }
        else
        {
            if (list.Status != LocalDncListStatus.Processing)
            {
                return $"Nothing was changed: the list '{list.Name}' is {list.Status}; only a list being processed can be paused.";
            }

            await manager.PauseImportAsync(listId, cancellationToken);

            note = "The import was paused. A list screens its numbers only once its import has completed, so none of this list's numbers are screened until it is resumed with the process action and finishes.";
        }

        list = await manager.FindByIdAsync(listId, cancellationToken) ?? list;

        return Json(new
        {
            list = await ListLocalDncListsTool.DescribeAsync(list, services.GetService<ILocalClock>()),
            note,
        });
    }
}
