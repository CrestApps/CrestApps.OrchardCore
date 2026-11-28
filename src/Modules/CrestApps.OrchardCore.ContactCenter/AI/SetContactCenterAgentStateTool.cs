using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Changes a Contact Center agent's state. Without an agent it acts on the caller, as the agent workspace and soft phone
/// do through <see cref="IAgentPresenceManager"/>: sign in to queues and campaigns, leave some, change presence or sign
/// out. With an agent it sets that agent's state or signs the agent out, as the live dashboard's agent state action does
/// through <see cref="IContactCenterSupervisorInterventionService"/>, which authorizes it against the queues the caller
/// supervises and records it under the caller's name.
/// </summary>
internal sealed class SetContactCenterAgentStateTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "setContactCenterAgentState";

    // The states the live dashboard (and the intervention service) can give another agent.
    private static readonly AgentPresenceStatus[] _supervisorStatuses =
    [
        AgentPresenceStatus.Available,
        AgentPresenceStatus.Away,
        AgentPresenceStatus.Break,
    ];

    private static readonly AgentPresenceStatus[] _statuses =
    [
        AgentPresenceStatus.Available,
        AgentPresenceStatus.Break,
        AgentPresenceStatus.Away,
        AgentPresenceStatus.DoNotDisturb,
        AgentPresenceStatus.Meeting,
        AgentPresenceStatus.Training,
        AgentPresenceStatus.AfterHoursUnavailable,
    ];

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "agentId": {
          "type": "string",
          "description": "Another agent, by agent id or user name, for a supervisor. Leave it out to change your own state. For another agent only setPresence (Available, Away or Break) and signOut are possible."
        },
        "action": {
          "type": "string",
          "enum": ["signIn", "leave", "setPresence", "signOut"],
          "description": "signIn joins the queues and campaigns given: while signed in they are added to the ones you are in and your presence stays; while offline you are signed in to exactly these and made available. leave drops the queues and campaigns given and keeps the others. setPresence changes the presence. signOut leaves every queue and campaign and goes offline. signIn and leave are for your own state only."
        },
        "queues": {
          "type": "array",
          "items": { "type": "string" },
          "description": "Queues, by id or name, for signIn or leave."
        },
        "campaigns": {
          "type": "array",
          "items": { "type": "string" },
          "description": "Campaigns, by id or name, for signIn or leave."
        },
        "status": {
          "type": "string",
          "enum": ["Available", "Break", "Away", "DoNotDisturb", "Meeting", "Training", "AfterHoursUnavailable"],
          "description": "The presence to set, for setPresence. Another agent can only be set to Available, Away or Break. A change asked for during a call or wrap-up applies when that work ends."
        },
        "reason": {
          "type": "string",
          "description": "Why, for setPresence: a reason code id or name (see listRecords with the kind agentStateReasonCode), or free text, for example lunch or coaching."
        }
      },
      "required": ["action"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Changes a Contact Center agent's state. Without agentId it acts on the calling user: sign in to queues and campaigns, leave some, change their own presence (available, break, away and so on) with a reason, or sign out; signing in means calls are offered to their soft phone, and entitlement rules can refuse a queue. With agentId a supervisor sets another agent to available, away or break, or signs the agent out; only agents of the queues the caller supervises, and when the agent is on a call the state applies once that work ends.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    /// <remarks>
    /// The permission depends on whose state changes: the caller's own needs Sign into queues; another agent's needs
    /// Intervene in calls and Monitor contact center, as the live dashboard's agent state action does.
    /// </remarks>
    protected override Permission Permission => null;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = ContactCenterOperationsSupport.GetUserId(user);

        if (userId is null)
        {
            return NotAllowedMessage;
        }

        var reader = new ContactCenterToolArguments(arguments);
        var action = reader.GetString("action");
        var agentText = reader.GetString("agentId");

        if (agentText is not null)
        {
            return await SetOtherAgentStateAsync(services, user, userId, reader, agentText, action, cancellationToken);
        }

        if (!await IsAuthorizedAsync(services, user, ContactCenterPermissions.SignIntoQueues))
        {
            return NotAllowedMessage;
        }

        var presenceManager = services.GetRequiredService<IAgentPresenceManager>();
        var profile = await services.GetRequiredService<IAgentProfileManager>().FindByUserIdAsync(userId, cancellationToken);

        try
        {
            switch (action?.ToLowerInvariant())
            {
                case "signin":
                case "leave":
                    return await ChangeMembershipsAsync(services, presenceManager, reader, profile, userId, join: string.Equals(action, "signIn", StringComparison.OrdinalIgnoreCase), cancellationToken);

                case "setpresence":
                    return await SetPresenceAsync(services, presenceManager, reader, profile, userId, cancellationToken);

                case "signout":
                    if (profile is null)
                    {
                        return "Nothing was changed: you have no agent profile, so you are not signed in.";
                    }

                    await presenceManager.SignOutAsync(userId, cancellationToken);

                    return Json(new
                    {
                        changed = "You were signed out of every queue and campaign and are offline.",
                        state = await GetMyAgentWorkStateTool.DescribeAsync(services, userId, cancellationToken),
                    });

                default:
                    return "Nothing was changed: action must be one of: signIn, leave, setPresence, signOut.";
            }
        }
        catch (AgentEntitlementDeniedException)
        {
            return "Nothing was changed: you are not allowed to sign in to any of the queues or campaigns given. Ask a manager to add them to your agent entitlements.";
        }
        catch (InvalidOperationException)
        {
            // The presence manager refuses while another change to the same agent holds the agent's lock.
            return "Nothing was changed: your agent state is being updated by something else right now. Try again in a moment.";
        }
    }

    internal static string Describe(AgentProfile agent)
        => agent.DisplayName ?? agent.UserName ?? agent.ItemId;

    private static async Task<object> SetOtherAgentStateAsync(
        IServiceProvider services,
        ClaimsPrincipal user,
        string supervisorId,
        ContactCenterToolArguments reader,
        string agentText,
        string action,
        CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, ContactCenterPermissions.InterveneInCalls) ||
            !await IsAuthorizedAsync(services, user, ContactCenterPermissions.MonitorContactCenter))
        {
            return NotAllowedMessage;
        }

        var signOut = string.Equals(action, "signOut", StringComparison.OrdinalIgnoreCase);

        if (!signOut && !string.Equals(action, "setPresence", StringComparison.OrdinalIgnoreCase))
        {
            return "Nothing was changed: for another agent the action must be setPresence or signOut. Only the agent can sign in to or leave queues.";
        }

        AgentPresenceStatus? status = null;

        if (!signOut &&
            (!reader.TryReadChoice("status", _supervisorStatuses, out status) || status is null))
        {
            return reader.Errors.Count > 0
                ? $"Nothing was changed: {string.Join(" ", reader.Errors)}"
                : $"Nothing was changed: give the status, one of: {string.Join(", ", _supervisorStatuses)}.";
        }

        var interventions = services.GetService<IContactCenterSupervisorInterventionService>();

        if (interventions is null)
        {
            return "Nothing was changed: changing another agent's state needs the Contact Center supervision feature, which is not enabled.";
        }

        var agent = await LiveCallTargets.FindAgentAsync(services, agentText, cancellationToken);

        if (agent is null)
        {
            return $"Nothing was changed: no agent has the id or user name '{agentText}'.";
        }

        var result = signOut
            ? await interventions.SignOutAgentAsync(agent.ItemId, supervisorId, user, cancellationToken)
            : await interventions.SetAgentStateAsync(agent.ItemId, supervisorId, user, status.GetValueOrDefault(), reader.GetString("reason"), cancellationToken);

        return ContactCenterOperationsSupport.Outcome(result, signOut
            ? $"{Describe(agent)} was signed out."
            : $"{Describe(agent)} was set to {status.GetValueOrDefault()}.");
    }

    private static async Task<object> ChangeMembershipsAsync(
        IServiceProvider services,
        IAgentPresenceManager presenceManager,
        ContactCenterToolArguments reader,
        AgentProfile profile,
        string userId,
        bool join,
        CancellationToken cancellationToken)
    {
        var (queueIds, campaignIds, unknown) = await ResolveAsync(services, reader, cancellationToken);

        if (reader.Errors.Count > 0)
        {
            return $"Nothing was changed: {string.Join(" ", reader.Errors)}";
        }

        if (unknown.Count > 0)
        {
            return $"Nothing was changed: no queue or campaign has the id or name {string.Join(", ", unknown.Select(value => $"'{value}'"))}. Use listRecords with the kind contactCenterQueue to find a queue.";
        }

        if (queueIds.Count == 0 && campaignIds.Count == 0)
        {
            return join
                ? "Nothing was changed: give at least one queue or campaign to sign in to."
                : "Nothing was changed: give at least one queue or campaign to leave.";
        }

        // The queues the agent is in, without the queue each joined campaign is routed under.
        var currentQueues = (profile?.QueueIds ?? [])
            .Where(queueId => ContactCenterConstants.CampaignQueue.GetCampaignId(queueId) is null)
            .ToList();
        var currentCampaigns = (profile?.CampaignIds ?? []).ToList();
        var signedIn = profile is not null &&
            profile.PresenceStatus != AgentPresenceStatus.Offline &&
            (currentQueues.Count > 0 || currentCampaigns.Count > 0);

        AgentProfile updated;
        string changed;

        if (join)
        {
            if (signedIn)
            {
                // Signing in again would replace the memberships and make the agent available; joining more queues
                // while signed in keeps the others and the presence, as the workspace's queue picker does.
                updated = await presenceManager.UpdateMembershipsAsync(
                    userId,
                    currentQueues.Union(queueIds, StringComparer.OrdinalIgnoreCase).ToList(),
                    currentCampaigns.Union(campaignIds, StringComparer.OrdinalIgnoreCase).ToList(),
                    cancellationToken);
                changed = "The queues and campaigns were added to the ones you are signed in to; your presence did not change.";
            }
            else
            {
                updated = await presenceManager.SignInAsync(userId, queueIds, campaignIds, cancellationToken);
                changed = "You were signed in.";
            }
        }
        else
        {
            if (!signedIn)
            {
                return "Nothing was changed: you are not signed in to any queue or campaign.";
            }

            var remainingQueues = currentQueues.Except(queueIds, StringComparer.OrdinalIgnoreCase).ToList();
            var remainingCampaigns = currentCampaigns.Except(campaignIds, StringComparer.OrdinalIgnoreCase).ToList();

            if (remainingQueues.Count == 0 && remainingCampaigns.Count == 0)
            {
                return "Nothing was changed: that would leave you in no queue or campaign. Use the signOut action to sign out.";
            }

            updated = await presenceManager.UpdateMembershipsAsync(userId, remainingQueues, remainingCampaigns, cancellationToken);
            changed = "You left the queues and campaigns given; your presence did not change.";
        }

        if (updated is null)
        {
            return "Nothing was changed: sign in before changing your queues or campaigns.";
        }

        // Entitlements drop the queues and campaigns the agent may not work, and refuse only when none is left.
        var refusedQueues = join
            ? queueIds.Where(queueId => !(updated.QueueIds ?? []).Contains(queueId, StringComparer.OrdinalIgnoreCase)).ToList()
            : [];
        var refusedCampaigns = join
            ? campaignIds.Where(campaignId => !(updated.CampaignIds ?? []).Contains(campaignId, StringComparer.OrdinalIgnoreCase)).ToList()
            : [];

        return Json(new
        {
            changed,
            notAllowed = refusedQueues.Count > 0 || refusedCampaigns.Count > 0
                ? new { queueIds = refusedQueues, campaignIds = refusedCampaigns, why = "Your agent entitlements do not include these, so they were left out." }
                : null,
            state = await GetMyAgentWorkStateTool.DescribeAsync(services, userId, cancellationToken),
        });
    }

    private static async Task<object> SetPresenceAsync(
        IServiceProvider services,
        IAgentPresenceManager presenceManager,
        ContactCenterToolArguments reader,
        AgentProfile profile,
        string userId,
        CancellationToken cancellationToken)
    {
        if (!reader.TryReadChoice<AgentPresenceStatus>("status", _statuses, out var status) || status is null)
        {
            return reader.Errors.Count > 0
                ? $"Nothing was changed: {string.Join(" ", reader.Errors)}"
                : $"Nothing was changed: give the status, one of: {string.Join(", ", _statuses)}.";
        }

        var updated = await presenceManager.SetPresenceAsync(userId, status.Value, reader.GetString("reason"), cancellationToken);
        var deferred = updated?.RequestedPresenceStatus is not null && updated.PresenceStatus != status.Value;
        var signedIn = (updated?.QueueIds?.Count ?? 0) > 0 || (updated?.CampaignIds?.Count ?? 0) > 0;

        return Json(new
        {
            changed = deferred
                ? $"Your presence becomes {status} when your current call or wrap-up ends."
                : $"Your presence is now {status}.",
            note = status == AgentPresenceStatus.Available && !signedIn && profile is not null
                ? "You are not signed in to any queue or campaign, so no work is offered. Use the signIn action."
                : null,
            state = await GetMyAgentWorkStateTool.DescribeAsync(services, userId, cancellationToken),
        });
    }

    // Finds the queues and campaigns the model named, by id or name. Names nobody has are returned as unknown.
    private static async Task<(List<string> QueueIds, List<string> CampaignIds, List<string> Unknown)> ResolveAsync(
        IServiceProvider services,
        ContactCenterToolArguments reader,
        CancellationToken cancellationToken)
    {
        var queueIds = new List<string>();
        var campaignIds = new List<string>();
        var unknown = new List<string>();

        var queueManager = services.GetService<IActivityQueueManager>();

        foreach (var value in reader.GetStringList("queues") ?? [])
        {
            var queue = queueManager is null
                ? null
                : await ContactCenterSetupLookups.FindAsync(queueManager, value, queue => queue.Name, cancellationToken);

            if (queue is null)
            {
                unknown.Add(value);
            }
            else if (!queueIds.Contains(queue.ItemId, StringComparer.OrdinalIgnoreCase))
            {
                queueIds.Add(queue.ItemId);
            }
        }

        var campaignManager = services.GetService<ICatalogManager<OmnichannelCampaign>>();

        foreach (var value in reader.GetStringList("campaigns") ?? [])
        {
            var campaign = campaignManager is null
                ? null
                : await ContactCenterSetupLookups.FindAsync(campaignManager, value, campaign => campaign.DisplayText, cancellationToken);

            if (campaign is null)
            {
                unknown.Add(value);
            }
            else if (!campaignIds.Contains(campaign.ItemId, StringComparer.OrdinalIgnoreCase))
            {
                campaignIds.Add(campaign.ItemId);
            }
        }

        return (queueIds, campaignIds, unknown);
    }
}
