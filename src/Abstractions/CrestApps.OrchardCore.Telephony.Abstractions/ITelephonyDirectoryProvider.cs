using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Defines optional provider directory lookup support for transfer destinations.
/// </summary>
public interface ITelephonyDirectoryProvider
{
    /// <summary>
    /// Gets directory entries that can be used as call-transfer destinations.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The provider directory lookup result.</returns>
    Task<TelephonyDirectoryResult> GetDirectoryAsync(CancellationToken cancellationToken = default);
}
