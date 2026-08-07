using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IDialerProfileManager"/>.
/// </summary>
public sealed class DialerProfileManager : CatalogManager<DialerProfile>, IDialerProfileManager
{
    private readonly IDialerProfileStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerProfileManager"/> class.
    /// </summary>
    /// <param name="store">The underlying dialer profile store.</param>
    /// <param name="handlers">The catalog entry handlers for dialer profiles.</param>
    /// <param name="logger">The logger instance.</param>
    public DialerProfileManager(
        IDialerProfileStore store,
        IEnumerable<ICatalogEntryHandler<DialerProfile>> handlers,
        ILogger<CatalogManager<DialerProfile>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<DialerProfile>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await _store.ListEnabledAsync(cancellationToken);

        foreach (var profile in profiles)
        {
            await LoadAsync(profile, cancellationToken);
        }

        return profiles;
    }

    /// <inheritdoc/>
    public async Task<DialerProfile> FindByCampaignAsync(string campaignId, CancellationToken cancellationToken = default)
    {
        var profile = await _store.FindByCampaignAsync(campaignId, cancellationToken);

        if (profile is not null)
        {
            await LoadAsync(profile, cancellationToken);
        }

        return profile;
    }
}
