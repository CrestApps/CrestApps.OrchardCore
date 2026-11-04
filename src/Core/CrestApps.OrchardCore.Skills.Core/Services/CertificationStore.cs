using CrestApps.OrchardCore.Skills.Core.Indexes;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Skills.Core.Services;

/// <summary>
/// Stores certifications.
/// </summary>
internal sealed class CertificationStore : DocumentCatalog<Certification, CertificationIndex>
{
    public CertificationStore(ISession session)
        : base(session)
    {
        CollectionName = SkillsConstants.CollectionName;
    }

    public Task<Certification> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = SkillsNames.Normalize(name);

        return Session.Query<Certification, CertificationIndex>(index => index.NormalizedName == normalized, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Certification>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        var certifications = await Session.Query<Certification, CertificationIndex>(index => index.Enabled, collection: CollectionName)
            .OrderBy(index => index.Name)
            .ListAsync(cancellationToken);

        return certifications.ToArray();
    }

    public Task<(int Count, IReadOnlyList<Certification> Items)> SearchAsync(string search, int skip, int take, CancellationToken cancellationToken = default)
    {
        return PageByNameAsync(index => index.Name, search, skip, take, cancellationToken);
    }
}
