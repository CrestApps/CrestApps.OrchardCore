using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The read side of the dialer profile catalog, separated from <see cref="IDialerProfileManager"/> so code that
/// only needs to look a profile up does not take a dependency on the management surface — and, more importantly,
/// so it can be given a default for tenants that have not enabled the dialer at all.
/// </summary>
public interface IDialerProfileReader
{
    /// <summary>
    /// Finds a dialer profile by its identifier.
    /// </summary>
    /// <param name="itemId">The dialer profile identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The dialer profile, or <see langword="null"/> when there is none.</returns>
    ValueTask<DialerProfile> FindByIdAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every enabled dialer profile.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The enabled dialer profiles.</returns>
    Task<IReadOnlyCollection<DialerProfile>> GetEnabledAsync(CancellationToken cancellationToken = default);
}
