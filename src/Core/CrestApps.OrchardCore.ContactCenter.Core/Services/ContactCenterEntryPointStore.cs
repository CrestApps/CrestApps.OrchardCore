using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IContactCenterEntryPointStore"/>.
/// </summary>
public sealed class ContactCenterEntryPointStore : DocumentCatalog<ContactCenterEntryPoint, ContactCenterEntryPointIndex>, IContactCenterEntryPointStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEntryPointStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ContactCenterEntryPointStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterEntryPoint> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return await Session.Query<ContactCenterEntryPoint, ContactCenterEntryPointIndex>(
            index => index.Name == name,
            collection: ContactCenterConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ContactCenterEntryPoint>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var entryPoints = await Session.Query<ContactCenterEntryPoint, ContactCenterEntryPointIndex>(
            index => index.Enabled,
            collection: ContactCenterConstants.CollectionName)
            .ListAsync(cancellationToken);

        return entryPoints.ToArray();
    }
}
