using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Decides when one more bulk message may leave a sending address on a channel. Automated outreach asks before it
/// composes or sends anything, so an address at its sending limit, warming up, or paused because a provider asked it
/// to slow down holds the work back instead of failing it.
/// </summary>
public interface IOmnichannelSendPacer
{
    /// <summary>
    /// Gets the channel the pacer governs.
    /// </summary>
    string Channel { get; }

    /// <summary>
    /// Asks whether one more bulk message may leave <paramref name="address"/> now.
    /// </summary>
    /// <param name="address">The sending address.</param>
    /// <param name="reserveTurn">
    /// <see langword="true"/> when the caller will wait for the time returned (an activity rescheduled to it), so the
    /// pacer gives the next caller a later turn and held-back work is spread out instead of all coming due at once;
    /// <see langword="false"/> when the caller only asks and will ask again later.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="null"/> to send now, or the time the message may be sent.</returns>
    Task<DateTime?> GetBulkSendTimeAsync(OmnichannelChannelEndpoint address, bool reserveTurn, CancellationToken cancellationToken = default);
}
