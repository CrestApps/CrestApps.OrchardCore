using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Executes the mute and unmute operations a telephony provider supports.
/// </summary>
public interface ITelephonyMuteProvider
{
    /// <summary>
    /// Mutes the local audio of an active call.
    /// </summary>
    /// <param name="call">A reference to the call to mute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> MuteAsync(CallReference call, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unmutes the local audio of an active call.
    /// </summary>
    /// <param name="call">A reference to the call to unmute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="TelephonyResult"/> describing the outcome.</returns>
    Task<TelephonyResult> UnmuteAsync(CallReference call, CancellationToken cancellationToken = default);
}
