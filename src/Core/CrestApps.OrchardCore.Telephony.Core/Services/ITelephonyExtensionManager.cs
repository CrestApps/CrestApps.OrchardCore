using CrestApps.Core.Services;
using CrestApps.OrchardCore.Telephony.Core.Models;

namespace CrestApps.OrchardCore.Telephony.Core.Services;

/// <summary>
/// Defines the management contract for internal telephony extensions.
/// </summary>
public interface ITelephonyExtensionManager : ICatalogManager<TelephonyExtension>
{
    /// <summary>
    /// Finds the enabled extension that owns the given dialed number.
    /// </summary>
    /// <param name="number">The dialed extension number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching extension, or <see langword="null"/> when none exists or it is disabled.</returns>
    Task<TelephonyExtension> FindByNumberAsync(string number, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the extension assigned to the given Orchard user.
    /// </summary>
    /// <param name="userId">The Orchard user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching extension, or <see langword="null"/> when none exists.</returns>
    Task<TelephonyExtension> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
