using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IContactCenterSkillStore"/>.
/// </summary>
public sealed class ContactCenterSkillStore : DocumentCatalog<ContactCenterSkill, ContactCenterSkillIndex>, IContactCenterSkillStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSkillStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ContactCenterSkillStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterSkill> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return await Session.Query<ContactCenterSkill, ContactCenterSkillIndex>(
            index => index.Name == name,
            collection: ContactCenterConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ContactCenterSkill>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var skills = await Session.Query<ContactCenterSkill, ContactCenterSkillIndex>(
            index => index.Enabled,
            collection: ContactCenterConstants.CollectionName)
            .ListAsync(cancellationToken);

        return skills.ToArray();
    }
}
