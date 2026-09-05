using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the management contract for dialer profiles.
/// </summary>
public interface IDialerProfileManager : ICatalogManager<DialerProfile>
{
    /// <summary>
    /// Lists every enabled dialer profile.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The enabled dialer profiles.</returns>
    Task<IReadOnlyCollection<DialerProfile>> GetEnabledAsync(CancellationToken cancellationToken = default);
}
