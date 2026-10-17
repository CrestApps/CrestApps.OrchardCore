namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Reads what the dialer is doing for every paced campaign queue with waiting work: dialing, idle for want of an agent,
/// held back, with nothing to call, or stalled.
/// </summary>
public interface IDialerPacingHealthService
{
    /// <summary>
    /// Reads the dialer's state now.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The state of each paced campaign queue with waiting work, the most serious first.</returns>
    Task<DialerPacingHealthSnapshot> EvaluateAsync(CancellationToken cancellationToken = default);
}
