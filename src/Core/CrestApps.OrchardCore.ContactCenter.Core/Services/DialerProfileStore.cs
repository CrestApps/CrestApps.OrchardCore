using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IDialerProfileStore"/>.
/// </summary>
public sealed class DialerProfileStore : DocumentCatalog<DialerProfile, DialerProfileIndex>, IDialerProfileStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DialerProfileStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public DialerProfileStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<DialerProfile>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await Session.Query<DialerProfile, DialerProfileIndex>(
            index => index.Enabled,
            collection: ContactCenterConstants.CollectionName)
            .ListAsync(cancellationToken);

        return profiles.ToArray();
    }

    /// <inheritdoc/>
    public async Task<DialerProfile> FindByCampaignAsync(string campaignId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(campaignId);

        return await Session.Query<DialerProfile, DialerProfileIndex>(
            index => index.CampaignId == campaignId,
            collection: ContactCenterConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
