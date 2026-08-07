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
    Task<IReadOnlyCollection<DialerProfile>> ListEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the dialer profile that targets the specified campaign.
    /// </summary>
    /// <param name="campaignId">The campaign identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching dialer profile, or <see langword="null"/> when none exists.</returns>
    Task<DialerProfile> FindByCampaignAsync(string campaignId, CancellationToken cancellationToken = default);
}
