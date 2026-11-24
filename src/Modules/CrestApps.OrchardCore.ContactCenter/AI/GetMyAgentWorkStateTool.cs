using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Returns the calling user's own agent state, as the agent workspace shows it: presence, the queues and campaigns they
/// are signed in to, their current interaction and whether wrap-up is pending. It reads the same snapshot the workspace
/// restores itself from (<see cref="IAgentSessionService.BuildSnapshotAsync"/>) and never another user's state.
/// </summary>
internal sealed class GetMyAgentWorkStateTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getMyAgentWorkState";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {},
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns the calling user's own agent state: presence, signed-in queues and campaigns, current interaction and pending wrap-up. Never another user's.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.SignIntoQueues;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = ContactCenterOperationsSupport.GetUserId(user);

        if (userId is null)
        {
            return NotAllowedMessage;
        }

        return Json(await DescribeAsync(services, userId, cancellationToken));
    }

    /// <summary>
    /// Describes the agent state of a user from the workspace snapshot, with names for the queues and campaigns.
    /// </summary>
    internal static async Task<object> DescribeAsync(IServiceProvider services, string userId, CancellationToken cancellationToken)
    {
        var snapshot = await services.GetRequiredService<IAgentSessionService>().BuildSnapshotAsync(userId, cancellationToken);

        if (snapshot is null || !snapshot.HasProfile)
        {
            return new
            {
                hasAgentProfile = false,
                signedIn = false,
                presence = AgentPresenceStatus.Offline.ToString(),
                note = "You have no agent profile yet. Signing in to a queue or campaign creates one.",
            };
        }

        var localClock = services.GetService<ILocalClock>();
        var queueNames = await ContactCenterSetupLookups.GetQueueNamesAsync(services, cancellationToken);
        var campaignNames = await ContactCenterSetupLookups.GetCampaignNamesAsync(services, cancellationToken);

        // A campaign is joined through a queue of its own, which the workspace never shows as a queue.
        var queues = (snapshot.QueueIds ?? [])
            .Where(queueId => ContactCenterConstants.CampaignQueue.GetCampaignId(queueId) is null)
            .Select(queueId => ContactCenterSetupLookups.Reference(queueId, queueNames))
            .ToList();
        var campaigns = (snapshot.CampaignIds ?? [])
            .Select(campaignId => ContactCenterSetupLookups.Reference(campaignId, campaignNames))
            .ToList();

        var profile = await services.GetRequiredService<IAgentProfileManager>().FindByUserIdAsync(userId, cancellationToken);
        var interaction = profile is null || services.GetService<IInteractionManager>() is not { } interactions
            ? null
            : await interactions.FindActiveByAgentAsync(profile.ItemId, cancellationToken);

        var signedIn = queues.Count > 0 || campaigns.Count > 0;
        var presence = snapshot.PresenceStatus ?? AgentPresenceStatus.Offline.ToString();

        return new
        {
            hasAgentProfile = true,
            agentId = profile?.ItemId,
            displayName = snapshot.DisplayName,
            signedIn,
            presence,
            presenceReason = snapshot.PresenceReason,
            requestedPresence = snapshot.RequestedPresenceStatus,
            wrapUpPending = string.Equals(presence, nameof(AgentPresenceStatus.WrapUp), StringComparison.Ordinal),
            hasOffer = !string.IsNullOrEmpty(snapshot.ActiveReservationId),
            workspaceOpen = snapshot.IsOnline,
            lastHeartbeat = await ToLocalAsync(localClock, snapshot.LastHeartbeatUtc),
            queues,
            campaigns,
            currentInteraction = interaction is null
                ? null
                : new
                {
                    id = interaction.ItemId,
                    channel = interaction.Channel.ToString(),
                    direction = interaction.Direction.ToString(),
                    status = interaction.Status.ToString(),
                    queue = ContactCenterSetupLookups.Reference(interaction.QueueId, queueNames),
                    activityId = interaction.ActivityItemId,
                    customerNumberEnding = ContactCenterOperationsSupport.Ending(interaction.CustomerAddress),
                    started = await ToLocalAsync(localClock, interaction.StartedUtc ?? interaction.CreatedUtc),
                    answered = await ToLocalAsync(localClock, interaction.AnsweredUtc),
                    wrapUpStarted = await ToLocalAsync(localClock, interaction.WrapUpStartedUtc),
                },
            note = signedIn && !snapshot.IsOnline
                ? "You are signed in, but no workspace or soft phone of yours is open, so no calls are offered until one is."
                : null,
        };
    }
}
