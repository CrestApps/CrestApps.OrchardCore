using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Deletes one Contact Center queue, inbound entry point, business-hours calendar or voice media clip, as the Delete
/// action of its admin page does, after checking the permission that page checks. A queue or a business-hours calendar
/// that routing still uses is kept unless the caller says to delete it anyway. These are the records the generic
/// <c>deleteRecord</c> tool cannot delete: queues, entry points and calendars are read-only record kinds, because their
/// editors post lists of objects the editor replay cannot post, and voice media clips are uploaded files. Queue groups,
/// agent state reason codes and dialer profiles are deleted with <c>deleteRecord</c>.
/// </summary>
internal sealed class DeleteContactCenterRecordTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteContactCenterRecord";

    private const string QueueKind = "queue";
    private const string EntryPointKind = "entryPoint";
    private const string BusinessHoursCalendarKind = "businessHoursCalendar";
    private const string VoiceMediaKind = "voiceMedia";

    private static readonly string[] _kinds = [QueueKind, EntryPointKind, BusinessHoursCalendarKind, VoiceMediaKind];

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "kind": {
          "type": "string",
          "enum": ["queue", "entryPoint", "businessHoursCalendar", "voiceMedia"],
          "description": "The kind of record to delete. Queue groups, agent state reason codes and dialer profiles are deleted with deleteRecord."
        },
        "id": {
          "type": "string",
          "description": "The ID of the record: from listRecords (kinds contactCenterQueue, contactCenterEntryPoint, businessHoursCalendar) or, for a voice media clip, from listVoiceMedia."
        },
        "deleteEvenIfUsed": {
          "type": "boolean",
          "description": "Delete a queue or business-hours calendar that queues, entry points or dialer profiles still use. Leave it out to be told what uses it instead."
        }
      },
      "required": ["kind", "id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes one Contact Center queue, inbound entry point, business-hours calendar or voice media clip by kind (queue, entryPoint, businessHoursCalendar, voiceMedia) and id. A queue or calendar still used by routing is kept unless deleteEvenIfUsed is true, since deleting it can leave calls without a destination. This cannot be undone.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    // The permission depends on the kind, so it is checked in the tool.
    /// <inheritdoc/>
    protected override Permission Permission => null;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var reader = new ContactCenterToolArguments(arguments);
        var kind = _kinds.FirstOrDefault(candidate => string.Equals(candidate, reader.GetString("kind"), StringComparison.OrdinalIgnoreCase));
        var permission = GetPermission(kind);

        if (permission is null)
        {
            // Nothing is read before the caller's permission is known, so an unknown kind is refused without a lookup.
            return $"Nothing was deleted: kind must be one of {string.Join(", ", _kinds)}.";
        }

        if (!await IsAuthorizedAsync(services, user, permission))
        {
            return NotAllowedMessage;
        }

        var id = reader.GetString("id");

        if (id is null)
        {
            return "Nothing was deleted: give the ID of the record in id.";
        }

        var deleteEvenIfUsed = reader.GetBool("deleteEvenIfUsed") == true;

        return kind switch
        {
            QueueKind => await DeleteAsync(services.GetRequiredService<IActivityQueueManager>(), id, "queue", queue => queue.Name, deleteEvenIfUsed ? null : FindQueueUsesAsync, services, cancellationToken),
            EntryPointKind => services.GetService<IContactCenterEntryPointManager>() is { } entryPoints
                ? await DeleteAsync(entryPoints, id, "entry point", entryPoint => entryPoint.Name, null, services, cancellationToken)
                : "Nothing was deleted: entry points exist only while the Contact Center Inbound Entry Points feature is enabled.",
            BusinessHoursCalendarKind => services.GetService<INamedCatalogManager<BusinessHoursSchedule>>() is { } calendars
                ? await DeleteAsync(calendars, id, "business-hours calendar", calendar => calendar.Name, deleteEvenIfUsed ? null : FindCalendarUsesAsync, services, cancellationToken)
                : "Nothing was deleted: business-hours calendars exist only while the Business Hours feature is enabled.",

            // The voice media handler also removes the copies the voice providers hold, as the Voice media page does.
            _ => services.GetService<IVoiceMediaItemManager>() is { } voiceMedia
                ? await DeleteAsync(voiceMedia, id, "voice media clip", item => item.Name, null, services, cancellationToken)
                : "Nothing was deleted: the voice media library is not available.",
        };
    }

    private static Permission GetPermission(string kind)
        => kind switch
        {
            QueueKind or EntryPointKind => ContactCenterPermissions.ManageQueues,
            BusinessHoursCalendarKind => BusinessHoursPermissions.ManageBusinessHoursSchedules,
            VoiceMediaKind => ContactCenterPermissions.ManageVoiceMedia,
            _ => null,
        };

    private static async Task<string> DeleteAsync<T>(
        ICatalogManager<T> manager,
        string id,
        string label,
        Func<T, string> name,
        Func<IServiceProvider, string, CancellationToken, Task<List<string>>> findUses,
        IServiceProvider services,
        CancellationToken cancellationToken)
        where T : CatalogItem
    {
        var record = await manager.FindByIdAsync(id, cancellationToken);

        if (record is null)
        {
            return $"Nothing was deleted: no {label} has the ID '{id}'.";
        }

        if (findUses is not null)
        {
            var uses = await findUses(services, id, cancellationToken);

            if (uses.Count > 0)
            {
                return Json(new
                {
                    deleted = false,
                    reason = $"The {label} '{name(record)}' is still used, so it was not deleted. Point these elsewhere first, or call again with deleteEvenIfUsed true.",
                    usedBy = uses,
                });
            }
        }

        if (!await manager.DeleteAsync(record, cancellationToken))
        {
            return $"The {label} '{name(record)}' could not be deleted.";
        }

        return Json(new
        {
            deleted = true,
            kind = label,
            id,
            name = name(record),
        });
    }

    private static async Task<List<string>> FindQueueUsesAsync(IServiceProvider services, string queueId, CancellationToken cancellationToken)
    {
        var uses = new List<string>();

        foreach (var queue in await services.GetRequiredService<IActivityQueueManager>().GetAllAsync(cancellationToken))
        {
            if (queue is null || string.Equals(queue.ItemId, queueId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(queue.OverflowQueueId, queueId, StringComparison.OrdinalIgnoreCase) ||
                (queue.OverflowTargets ?? []).Any(target => string.Equals(target?.QueueId, queueId, StringComparison.OrdinalIgnoreCase)))
            {
                uses.Add($"Queue '{queue.Name}' overflows to it.");
            }
        }

        if (services.GetService<IContactCenterEntryPointManager>() is { } entryPoints)
        {
            foreach (var entryPoint in await entryPoints.GetAllAsync(cancellationToken))
            {
                if (entryPoint is null)
                {
                    continue;
                }

                if (string.Equals(entryPoint.TargetQueueId, queueId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(entryPoint.OverflowQueueId, queueId, StringComparison.OrdinalIgnoreCase))
                {
                    uses.Add($"Entry point '{entryPoint.Name}' routes to it.");
                }
                else if ((entryPoint.IvrFlow?.Nodes ?? [])
                    .SelectMany(node => node?.Options ?? [])
                    .Select(option => option?.Action)
                    .Append(entryPoint.IvrFlow?.FallbackAction)
                    .Any(action => action?.Kind == IvrActionKind.RouteToQueue && string.Equals(action.TargetId, queueId, StringComparison.OrdinalIgnoreCase)))
                {
                    uses.Add($"The phone menu of entry point '{entryPoint.Name}' routes to it.");
                }
            }
        }

        return uses;
    }

    private static async Task<List<string>> FindCalendarUsesAsync(IServiceProvider services, string calendarId, CancellationToken cancellationToken)
    {
        var uses = new List<string>();

        foreach (var queue in await services.GetRequiredService<IActivityQueueManager>().GetAllAsync(cancellationToken))
        {
            if (string.Equals(queue?.BusinessHoursCalendarId, calendarId, StringComparison.OrdinalIgnoreCase))
            {
                uses.Add($"Queue '{queue.Name}' follows it.");
            }
        }

        if (services.GetService<IContactCenterEntryPointManager>() is { } entryPoints)
        {
            foreach (var entryPoint in await entryPoints.GetAllAsync(cancellationToken))
            {
                if (string.Equals(entryPoint?.BusinessHoursCalendarId, calendarId, StringComparison.OrdinalIgnoreCase))
                {
                    uses.Add($"Entry point '{entryPoint.Name}' follows it.");
                }
            }
        }

        if (services.GetService<IDialerProfileManager>() is { } dialerProfiles)
        {
            foreach (var profile in await dialerProfiles.GetAllAsync(cancellationToken))
            {
                if (profile is not null &&
                    (string.Equals(profile.CallingCalendarId, calendarId, StringComparison.OrdinalIgnoreCase) ||
                    (profile.RegionalCallingCalendarIds?.Values ?? []).Any(value => string.Equals(value, calendarId, StringComparison.OrdinalIgnoreCase))))
                {
                    uses.Add($"Dialer profile '{profile.Name}' uses it as a calling calendar.");
                }
            }
        }

        return uses;
    }
}
