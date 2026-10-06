namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Records the calls that are not Contact Center interactions when the tenant records every call.
/// </summary>
public interface ITelnyxAutomaticCallRecorder
{
    /// <summary>
    /// Starts recording an automated voice agent's call that was just answered.
    /// </summary>
    /// <param name="callControlId">The customer's leg.</param>
    /// <param name="activityId">The CRM activity the call belongs to.</param>
    /// <param name="customerNumber">The customer's number.</param>
    /// <param name="isInbound">Whether the customer placed the call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when Telnyx started recording.</returns>
    Task<bool> RecordAiCallAsync(
        string callControlId,
        string activityId,
        string customerNumber,
        bool isInbound,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts recording a number an agent dialed on the soft phone's keypad, once it answered.
    /// </summary>
    /// <param name="callControlId">The dialed number's leg.</param>
    /// <param name="agentUserId">The user identifier of the agent who dialed it.</param>
    /// <param name="telephonyCallId">The soft phone's identifier of the call (the agent's leg).</param>
    /// <param name="dialedNumber">The number dialed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when Telnyx started recording.</returns>
    Task<bool> RecordSoftPhoneCallAsync(
        string callControlId,
        string agentUserId,
        string telephonyCallId,
        string dialedNumber,
        CancellationToken cancellationToken = default);
}
