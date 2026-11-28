using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Lists the calls AI voice agents are holding right now, as the live dashboard's AI calls panel does, limited to the
/// calls of the campaigns the caller supervises (see <see cref="LiveCallTargets"/>).
/// </summary>
internal sealed class ListLiveAIVoiceCallsTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listLiveAIVoiceCalls";

    // The live dashboard reads at most this many live calls.
    private const int MaxLiveCalls = 200;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "customerNumber": {
          "type": "string",
          "description": "Only the call with this customer number (at least its last four digits)."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "Calls per page, 20 when not given, at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the automated AI voice calls in progress with contact, number, AI profile, direction and duration. Only calls of campaigns whose queue the caller supervises are listed; an AI call with no campaign is not. Customer numbers are shown by their last four digits; search by number with customerNumber.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.MonitorContactCenter;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var supervisorId = ContactCenterOperationsSupport.GetUserId(user);

        if (supervisorId is null)
        {
            return NotAllowedMessage;
        }

        var reader = new ContactCenterToolArguments(arguments);
        var (page, pageSize) = ReadPaging(reader);
        var number = reader.GetString("customerNumber");
        var calls = new List<LiveAIVoiceCall>();

        foreach (var call in await LiveAIVoiceCalls.ListAsync(services.GetRequiredService<ISession>(), MaxLiveCalls, cancellationToken))
        {
            if ((number is null || ContactCenterOperationsSupport.MatchesNumber(call.Activity.PreferredDestination, number)) &&
                await LiveCallTargets.IsAICallInScopeAsync(services, user, supervisorId, call.Activity, cancellationToken))
            {
                calls.Add(call);
            }
        }

        var (items, total, truncated) = ContactCenterSetupLookups.Page(calls, page, pageSize);
        var now = services.GetRequiredService<IClock>().UtcNow;
        var localClock = services.GetRequiredService<ILocalClock>();
        var profiles = services.GetService<IAIProfileManager>();
        var contents = services.GetService<IContentManager>();
        var profileNames = new Dictionary<string, string>(StringComparer.Ordinal);
        var rows = new List<object>();

        foreach (var (activity, liveCall) in items)
        {
            var contact = string.IsNullOrEmpty(activity.ContactContentItemId) || contents is null
                ? null
                : await contents.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

            rows.Add(new
            {
                activityId = activity.ItemId,
                contactId = activity.ContactContentItemId,
                contactName = contact?.DisplayText,
                customerNumberEnding = ContactCenterOperationsSupport.Ending(activity.PreferredDestination),
                aiProfile = await GetProfileNameAsync(profiles, profileNames, activity.AIProfileId),
                campaignId = activity.CampaignId,
                direction = string.Equals(activity.Source, ActivitySources.Inbound, StringComparison.Ordinal) ? "Inbound" : "Outbound",
                answered = await ToLocalAsync(localClock, liveCall.AnsweredUtc),
                durationSeconds = (int)Math.Max(0, (now - liveCall.AnsweredUtc).TotalSeconds),
                realtime = liveCall.IsRealtime,
            });
        }

        return Json(new
        {
            items = rows,
            total,
            page,
            pageSize,
            truncated,
            canIntervene = await IsAuthorizedAsync(services, user, ContactCenterPermissions.InterveneInCalls),
        });
    }

    private static async Task<string> GetProfileNameAsync(IAIProfileManager profiles, Dictionary<string, string> cache, string profileId)
    {
        if (string.IsNullOrEmpty(profileId) || profiles is null)
        {
            return null;
        }

        if (!cache.TryGetValue(profileId, out var name))
        {
            var profile = await profiles.FindByIdAsync(profileId);
            cache[profileId] = name = profile?.DisplayText ?? profile?.Name;
        }

        return name;
    }
}
