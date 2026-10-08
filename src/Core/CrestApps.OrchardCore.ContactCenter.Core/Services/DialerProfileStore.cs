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
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<DialerProfile>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await Session.Query<DialerProfile, DialerProfileIndex>(
            index => index.Enabled,
            collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken);

        return profiles.ToArray();
    }
}
