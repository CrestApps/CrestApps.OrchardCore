using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Executes the hold and resume operations a telephony provider supports.
/// </summary>
public interface ITelephonyHoldProvider
{
    /// <summary>
    /// Places an active call on hold.
    /// </summary>
    /// <param name="call">A reference to the call to place on hold.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> HoldAsync(CallReference call, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a call that is currently on hold.
    /// </summary>
    /// <param name="call">A reference to the call to resume.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> ResumeAsync(CallReference call, CancellationToken cancellationToken = default);
}
