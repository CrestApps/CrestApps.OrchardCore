using CrestApps.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Skills.Core.Services;

/// <summary>
/// The default <see cref="ISkillManager"/>.
/// </summary>
internal sealed class SkillManager : CatalogManager<Skill>, ISkillManager
{
    private readonly SkillStore _store;

    public SkillManager(
        SkillStore store,
        IEnumerable<ICatalogEntryHandler<Skill>> handlers,
        ILogger<CatalogManager<Skill>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    public async Task<Skill> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var skill = await _store.FindByNameAsync(name, cancellationToken);

        if (skill is not null)
        {
            await LoadAsync(skill, cancellationToken);
        }

        return skill;
    }

    public async Task<IReadOnlyList<Skill>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        var skills = await _store.GetEnabledAsync(cancellationToken);

        foreach (var skill in skills)
        {
            await LoadAsync(skill, cancellationToken);
        }

        return skills;
    }

    public async Task<(int Count, IReadOnlyList<Skill> Items)> SearchAsync(string search, int skip, int take, CancellationToken cancellationToken = default)
    {
        var page = await _store.SearchAsync(search, skip, take, cancellationToken);

        foreach (var skill in page.Items)
        {
            await LoadAsync(skill, cancellationToken);
        }

        return page;
    }
}
