using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Revalidates outbound dialer compliance before a recovered pending command contacts a provider.
/// </summary>
public sealed class DialerProviderCommandDispatchValidator : IProviderCommandDispatchValidator
{
    private readonly IDialerProfileManager _profileManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IDialerEligibilityService _eligibilityService;
    private readonly IActivityReservationManager _reservationManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IInteractionManager _interactionManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerProviderCommandDispatchValidator"/> class.
    /// </summary>
    /// <param name="profileManager">The manager used to load the governing dialer profile.</param>
    /// <param name="activityManager">The manager used to load the current CRM activity.</param>
    /// <param name="eligibilityService">The compliance gate used to revalidate outbound eligibility.</param>
    /// <param name="reservationManager">The reservations, which say whom a dial was placed for.</param>
    /// <param name="agentManager">The agent profiles, which say whether that agent is still held for the dial.</param>
    /// <param name="interactionManager">The interactions, which say whether that agent is already on another call.</param>
    /// <param name="logger">The logger used to surface why a dial dispatch was refused.</param>
    public DialerProviderCommandDispatchValidator(
        IDialerProfileManager profileManager,
        IOmnichannelActivityManager activityManager,
        IDialerEligibilityService eligibilityService,
        IActivityReservationManager reservationManager,
        IAgentProfileManager agentManager,
        IInteractionManager interactionManager,
        ILogger<DialerProviderCommandDispatchValidator> logger)
    {
        _profileManager = profileManager;
        _activityManager = activityManager;
        _eligibilityService = eligibilityService;
        _reservationManager = reservationManager;
        _agentManager = agentManager;
        _interactionManager = interactionManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> CanDispatchAsync(ProviderCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Refusing dispatch here compensates the dial command and fails the interaction, so an agent who clicked
        // Dial sees nothing happen. Log every refusal reason so a silent no-dial is diagnosable from the log.
        if (string.IsNullOrWhiteSpace(command.DialerProfileId) ||
            string.IsNullOrWhiteSpace(command.ActivityItemId))
        {
            _logger.LogWarning(
                "Refused to dispatch dial command '{CommandId}' because it is missing its dialer profile or activity reference (profile '{DialerProfileId}', activity '{ActivityItemId}').",
                command.CommandId.SanitizeLogValue(),
                command.DialerProfileId.SanitizeLogValue(),
                command.ActivityItemId.SanitizeLogValue());

            return false;
        }

        // A queued callback is dialed with the platform's built-in callback profile, which is never stored.
        var profile = QueueCallbackDialerProfile.IsCallbackProfile(command.DialerProfileId)
            ? QueueCallbackDialerProfile.Create()
            : await _profileManager.FindByIdAsync(command.DialerProfileId, cancellationToken);
        var activity = await _activityManager.FindByIdAsync(command.ActivityItemId, cancellationToken);

        if (profile is null || activity is null)
        {
            _logger.LogWarning(
                "Refused to dispatch dial command '{CommandId}' because its dialer profile '{DialerProfileId}' ({ProfileState}) or activity '{ActivityItemId}' ({ActivityState}) no longer exists.",
                command.CommandId.SanitizeLogValue(),
                command.DialerProfileId.SanitizeLogValue(),
                profile is null ? "missing" : "found",
                command.ActivityItemId.SanitizeLogValue(),
                activity is null ? "missing" : "found");

            return false;
        }

        if (!await IsAgentStillHeldForDialAsync(command, profile, cancellationToken))
        {
            return false;
        }

        var eligibility = await _eligibilityService.EvaluateAsync(new DialerEligibilityContext
        {
            Profile = profile,
            Activity = activity,

            // This runs after the dial attempt was recorded but before it reaches the provider, so the
            // maximum-attempts gate must not count the in-flight attempt against the limit.
            AttemptAlreadyCounted = true,
        }, cancellationToken);

        if (!eligibility.IsEligible)
        {
            _logger.LogWarning(
                "Refused to dispatch dial command '{CommandId}' for activity '{ActivityItemId}' under dialer profile '{ProfileName}' because the compliance gate suppressed it: {SuppressionReason} - {SuppressionDescription}.",
                command.CommandId.SanitizeLogValue(),
                command.ActivityItemId.SanitizeLogValue(),
                profile.Name.SanitizeLogValue(),
                eligibility.Reason,
                eligibility.Description.SanitizeLogValue());
        }

        return eligibility.IsEligible;
    }

    // A dial placed for an agent may reach the provider well after it was recorded: a node that is shutting down defers
    // it, and the next node sends it. By then the agent may have been let go -- their session expired across the restart,
    // or they signed in again -- and the dialer may have reserved them for another customer. Sent anyway, both customers
    // rang for one agent and both were connected to them. So the dial goes out only while the reservation it was placed
    // under still holds that agent for it: accepted, with the agent held Reserved for it (an automated dial) or Busy on
    // it (a preview or callback dial), and on no other call.
    private async Task<bool> IsAgentStillHeldForDialAsync(ProviderCommand command, DialerProfile profile, CancellationToken cancellationToken)
    {
        // A call placed for the campaign rather than for an agent holds nobody.
        if (string.IsNullOrWhiteSpace(command.ReservationId))
        {
            return true;
        }

        var reservation = await _reservationManager.FindByIdAsync(command.ReservationId, cancellationToken);

        if (reservation is null || reservation.Status != ReservationStatus.Accepted || string.IsNullOrWhiteSpace(reservation.AgentId))
        {
            _logger.LogWarning(
                "Refused to dispatch dial command '{CommandId}' for activity '{ActivityItemId}' because reservation '{ReservationId}' it was placed under is no longer accepted ({ReservationStatus}).",
                command.CommandId.SanitizeLogValue(),
                command.ActivityItemId.SanitizeLogValue(),
                command.ReservationId.SanitizeLogValue(),
                reservation is null ? "missing" : reservation.Status.ToString());

            return false;
        }

        var agent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);
        var held = profile.Mode.IsAutomated()
            ? agent?.PresenceStatus == AgentPresenceStatus.Reserved &&
                string.Equals(agent.DialHoldReservationId, reservation.ItemId, StringComparison.Ordinal)
            : agent?.PresenceStatus == AgentPresenceStatus.Busy;

        if (!held)
        {
            _logger.LogWarning(
                "Refused to dispatch dial command '{CommandId}' for activity '{ActivityItemId}' because agent '{AgentId}' is no longer held for reservation '{ReservationId}' (presence {PresenceStatus}, dial hold '{DialHoldReservationId}').",
                command.CommandId.SanitizeLogValue(),
                command.ActivityItemId.SanitizeLogValue(),
                reservation.AgentId.SanitizeLogValue(),
                reservation.ItemId.SanitizeLogValue(),
                agent?.PresenceStatus,
                agent?.DialHoldReservationId.SanitizeLogValue());

            return false;
        }

        var interactions = await _interactionManager.GetActiveByAgentIdsAsync([agent.ItemId], cancellationToken);
        var otherCall = interactions.FirstOrDefault(interaction =>
            interaction.Channel == InteractionChannel.Voice &&
            !string.Equals(interaction.ItemId, command.InteractionId, StringComparison.Ordinal) &&
            string.Equals(interaction.AgentId, agent.ItemId, StringComparison.Ordinal) &&
            InteractionStatuses.OccupyingAgent.Contains(interaction.Status));

        if (otherCall is not null)
        {
            _logger.LogWarning(
                "Refused to dispatch dial command '{CommandId}' for activity '{ActivityItemId}' because agent '{AgentId}' is already on call '{InteractionId}' ({InteractionStatus}).",
                command.CommandId.SanitizeLogValue(),
                command.ActivityItemId.SanitizeLogValue(),
                agent.ItemId.SanitizeLogValue(),
                otherCall.ItemId.SanitizeLogValue(),
                otherCall.Status);

            return false;
        }

        return true;
    }
}
