using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Records a hold the agent placed or ended from the soft phone against the call the Contact Center tracks.
/// </summary>
/// <remarks>
/// A provider that performs hold in the agent's own media never reports it, so this is the only way such a hold
/// reaches the call's hold time and the audit log. A hold is recorded as
/// <see cref="ContactCenterConstants.Events.CallHeld"/> when it starts and
/// <see cref="ContactCenterConstants.Events.CallResumed"/>, carrying its length, when it ends. Repeating a change the
/// call is already in records nothing, so a retried command is recorded once.
/// </remarks>
public interface IAgentCallHoldRecorder
{
    /// <summary>
    /// Records the agent's hold or resume.
    /// </summary>
    /// <param name="change">The change, dated by when the agent asked for it.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the change was recorded; <see langword="false"/> when the call is not one
    /// the Contact Center tracks for this agent, has ended, or is already in the requested state.</returns>
    Task<bool> RecordAsync(TelephonyCallHoldChange change, CancellationToken cancellationToken = default);
}
