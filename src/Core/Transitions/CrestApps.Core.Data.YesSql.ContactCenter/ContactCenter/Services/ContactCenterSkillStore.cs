using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.ContactCenter;
using CrestApps.Core.Data.YesSql.Services;
using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.ContactCenter.Models;
using YesSql;

namespace CrestApps.Core.Data.YesSql.ContactCenter.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="IContactCenterSkillStore"/>.
/// </summary>
public sealed class ContactCenterSkillStore : ConcurrentDocumentCatalog<ContactCenterSkill, ContactCenterSkillIndex>, IContactCenterSkillStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSkillStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public ContactCenterSkillStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterSkill> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return await Session.Query<ContactCenterSkill, ContactCenterSkillIndex>(
            index => index.Name == name,
            collection: ContactCenterStorage.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ContactCenterSkill>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        var skills = await Session.Query<ContactCenterSkill, ContactCenterSkillIndex>(
            index => index.Enabled,
            collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken);

        return skills.ToArray();
    }
}
