using System.Globalization;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Writes an agent's soft-phone calls that the Contact Center did not route -- extension calls to and from
/// colleagues, and calls placed straight from the browser -- to the audit log, so the time an agent spends on the
/// phone is accounted for even when no queue was involved.
/// </summary>
/// <remarks>
/// A call the Contact Center routed also appears in the agent's telephony history, keyed by its interaction, and
/// is already recorded by its own call events; it is skipped here so it is not counted twice.
/// </remarks>
public sealed class ContactCenterTelephonyCallObserver : ITelephonyCallObserver
{
    private readonly IInteractionManager _interactionManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly Lazy<IContactCenterAuditRecorder> _auditRecorder;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterTelephonyCallObserver"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager used to tell a routed call from an extension call.</param>
    /// <param name="agentManager">The agent profile manager used to name the agent on the call.</param>
    /// <param name="auditRecorder">The recorder that writes the call to the audit log. Lazy because the telephony
    /// history store this observer is told by is used by an event handler, and the recorder depends on the publisher
    /// that dispatches to every handler.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterTelephonyCallObserver(
        IInteractionManager interactionManager,
        IAgentProfileManager agentManager,
        Lazy<IContactCenterAuditRecorder> auditRecorder,
        ILogger<ContactCenterTelephonyCallObserver> logger)
    {
        _interactionManager = interactionManager;
        _agentManager = agentManager;
        _auditRecorder = auditRecorder;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task CallStartedAsync(TelephonyInteraction interaction, CancellationToken cancellationToken = default)
        => RecordAsync(ContactCenterConstants.Events.ExtensionCallStarted, interaction, cancellationToken);

    /// <inheritdoc/>
    public Task CallEndedAsync(TelephonyInteraction interaction, CancellationToken cancellationToken = default)
        => RecordAsync(ContactCenterConstants.Events.ExtensionCallEnded, interaction, cancellationToken);

    private async Task RecordAsync(string eventType, TelephonyInteraction interaction, CancellationToken cancellationToken)
    {
        if (interaction is null || string.IsNullOrEmpty(interaction.CallId))
        {
            return;
        }

        try
        {
            if (!string.IsNullOrEmpty(interaction.InteractionId) &&
                await _interactionManager.FindByIdAsync(interaction.InteractionId, cancellationToken) is not null)
            {
                return;
            }

            var agent = string.IsNullOrEmpty(interaction.UserId)
                ? null
                : await _agentManager.FindByUserIdAsync(interaction.UserId, cancellationToken);

            var ended = eventType == ContactCenterConstants.Events.ExtensionCallEnded;
            var data = new CallLifecycleEventData
            {
                ProviderName = interaction.ProviderName,
                ProviderCallId = interaction.CallId,
                AgentId = agent?.ItemId,
                Direction = interaction.Direction.ToString(),
                State = interaction.Outcome.ToString(),
                Target = interaction.Direction == CallDirection.Inbound ? interaction.From : interaction.To,
                DurationSeconds = ended ? interaction.DurationSeconds : null,
            };

            data.Details["telephonyInteractionId"] = interaction.InteractionId ?? string.Empty;
            data.Details["userId"] = interaction.UserId ?? string.Empty;
            data.Details["isExtension"] = interaction.IsExtension.ToString(CultureInfo.InvariantCulture);

            if (!string.IsNullOrEmpty(interaction.ExtensionNumber))
            {
                data.Details["extensionNumber"] = interaction.ExtensionNumber;
            }

            // The history store tells this observer every time it saves the call, so each start and each end is
            // keyed on the call alone and a repeat collapses onto the first.
            await _auditRecorder.Value.RecordCallAsync(
                eventType,
                data,
                ended ? interaction.EndedUtc ?? interaction.StartedUtc : interaction.StartedUtc,
                ContactCenterActor.Agent(interaction.UserId),
                $"extension-call:{eventType}:{interaction.InteractionId ?? interaction.CallId}",
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "The soft-phone call '{CallId}' could not be written to the Contact Center audit log.",
                interaction.CallId.SanitizeLogValue());
        }
    }
}
