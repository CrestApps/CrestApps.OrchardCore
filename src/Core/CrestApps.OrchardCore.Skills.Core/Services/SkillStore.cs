using CrestApps.OrchardCore.Skills.Core.Indexes;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Skills.Core.Services;

/// <summary>
/// Stores skills.
/// </summary>
internal sealed class SkillStore : DocumentCatalog<Skill, SkillIndex>
{
    public SkillStore(ISession session)
        : base(session)
    {
        CollectionName = SkillsConstants.CollectionName;
    }

    public Task<Skill> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = SkillsNames.Normalize(name);

        return Session.Query<Skill, SkillIndex>(index => index.NormalizedName == normalized, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Skill>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        var skills = await Session.Query<Skill, SkillIndex>(index => index.Enabled, collection: CollectionName)
            .OrderBy(index => index.Name)
            .ListAsync(cancellationToken);

        return skills.ToArray();
    }

    public Task<(int Count, IReadOnlyList<Skill> Items)> SearchAsync(string search, int skip, int take, CancellationToken cancellationToken = default)
    {
        return PageByNameAsync(index => index.Name, search, skip, take, cancellationToken);
    }
}
