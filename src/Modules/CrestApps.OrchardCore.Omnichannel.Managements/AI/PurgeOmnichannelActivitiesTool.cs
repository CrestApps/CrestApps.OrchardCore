using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Purges activities by ID, as the Purge bulk action of Manage Activities does: only activities a bulk action may
/// change, never one the dialer or an agent is on a call for, and each one checked with Purge activity.
/// </summary>
public sealed class PurgeOmnichannelActivitiesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "purgeOmnichannelActivities";

    /// <summary>
    /// The most activities one call purges.
    /// </summary>
    public const int MaxIds = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "ids": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "maxItems": 50,
          "description": "The IDs of the activities to purge, from listOmnichannelActivities. At most 50 per call."
        }
      },
      "required": ["ids"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Purges activities by id so they are never worked. Cannot be undone; activities on a live call, and activities already completed or purged, are skipped and reported. To purge by a filter, list the matching activities first with listOmnichannelActivities scope all.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AIModuleTools.IsAuthorizedAsync(services, OmnichannelConstants.Permissions.PurgeActivity))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var ids = OmnichannelToolSupport.ReadStrings(arguments, "ids");

        if (ids is null || ids.Length == 0)
        {
            return "Nothing was purged: give the IDs of the activities in ids.";
        }

        if (ids.Length > MaxIds)
        {
            return $"Nothing was purged: at most {MaxIds} activities can be purged in one call.";
        }

        var caller = AIModuleTools.GetCaller(services);
        var authorizationService = services.GetRequiredService<IAuthorizationService>();
        var manager = services.GetRequiredService<IOmnichannelActivityManager>();
        var activities = new List<OmnichannelActivity>();
        var skipped = new List<object>();

        foreach (var id in ids)
        {
            var activity = await manager.FindByIdAsync(id, cancellationToken);

            if (activity is null)
            {
                skipped.Add(new { id, reason = "No activity has this ID." });

                continue;
            }

            // The single purge on a contact's page checks the permission on the activity; the bulk action checks it once.
            // The stricter of the two is applied.
            if (!await authorizationService.AuthorizeAsync(caller, OmnichannelConstants.Permissions.PurgeActivity, activity))
            {
                skipped.Add(new { id, reason = "You are not allowed to purge this activity." });

                continue;
            }

            if (!ActivityBulkRules.IsBulkManageable(activity))
            {
                skipped.Add(new { id, reason = $"The activity is {activity.Status} and cannot be purged." });

                continue;
            }

            activities.Add(activity);
        }

        var candidates = activities.Select(activity => activity.ItemId).ToList();

        await ActivityBulkRules.RemoveDialerWorkInFlightAsync(services.GetServices<IActivityDialerContributor>(), activities, cancellationToken);

        foreach (var id in candidates.Except(activities.Select(activity => activity.ItemId), StringComparer.Ordinal))
        {
            skipped.Add(new { id, reason = "The dialer is dialing it or an agent is on its call right now. Try again once the call ends." });
        }

        var purgedAtUtc = services.GetRequiredService<IClock>().UtcNow;
        var purgedById = caller.FindFirstValue(ClaimTypes.NameIdentifier);
        var purgedByUsername = caller.Identity?.Name;

        foreach (var activity in activities)
        {
            ActivityPurgeHelper.Purge(activity, purgedAtUtc, purgedById, purgedByUsername);
            await manager.UpdateAsync(activity, cancellationToken: cancellationToken);
        }

        return AIModuleTools.Json(new
        {
            purged = activities.Select(activity => activity.ItemId),
            purgedCount = activities.Count,
            skipped,
        });
    }
}
