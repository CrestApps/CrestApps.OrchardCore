using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;

namespace CrestApps.OrchardCore.ContactCenter.SharedSkills;

/// <summary>
/// The Contact Center skill store when Skills is on: reads and writes go to the shared catalog, so a skill made on
/// either screen is the same skill. <see cref="ContactCenterSkillMirrorHandler"/> keeps a copy in the Contact Center
/// store, so turning Skills off later loses nothing.
/// </summary>
internal sealed class SharedContactCenterSkillStore : IContactCenterSkillStore
{
    private readonly ISkillManager _skillManager;
    private readonly SharedSkillsCatalogBackfill _backfill;

    public SharedContactCenterSkillStore(
        ISkillManager skillManager,
        SharedSkillsCatalogBackfill backfill)
    {
        _skillManager = skillManager;
        _backfill = backfill;
    }

    public async ValueTask<ContactCenterSkill> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await _backfill.EnsureAsync(cancellationToken);

        return SharedSkillsMapper.ToContactCenter(await _skillManager.FindByIdAsync(id, cancellationToken));
    }

    public async ValueTask<IReadOnlyCollection<ContactCenterSkill>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _backfill.EnsureAsync(cancellationToken);

        return (await _skillManager.GetAllAsync(cancellationToken)).Select(SharedSkillsMapper.ToContactCenter).ToArray();
    }

    public async ValueTask<IReadOnlyCollection<ContactCenterSkill>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var wanted = ids.ToHashSet(StringComparer.Ordinal);

        return (await GetAllAsync(cancellationToken)).Where(skill => wanted.Contains(skill.ItemId)).ToArray();
    }

    public async ValueTask<PageResult<ContactCenterSkill>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
        where TQuery : QueryContext
    {
        await _backfill.EnsureAsync(cancellationToken);

        var result = await _skillManager.PageAsync(page, pageSize, context, cancellationToken);

        return new PageResult<ContactCenterSkill>
        {
            Count = result.Count,
            Entries = result.Entries.Select(SharedSkillsMapper.ToContactCenter).ToArray(),
        };
    }

    public async Task<ContactCenterSkill> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        await _backfill.EnsureAsync(cancellationToken);

        return SharedSkillsMapper.ToContactCenter(await _skillManager.FindByNameAsync(name, cancellationToken));
    }

    public async Task<IReadOnlyCollection<ContactCenterSkill>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        await _backfill.EnsureAsync(cancellationToken);

        return (await _skillManager.GetEnabledAsync(cancellationToken)).Select(SharedSkillsMapper.ToContactCenter).ToArray();
    }

    public async ValueTask CreateAsync(ContactCenterSkill record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var skill = new Skill();

        SharedSkillsMapper.CopyTo(record, skill);
        await _skillManager.CreateAsync(skill, cancellationToken);
        record.ItemId = skill.ItemId;
    }

    public async ValueTask UpdateAsync(ContactCenterSkill record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var skill = await _skillManager.FindByIdAsync(record.ItemId, cancellationToken);

        if (skill is null)
        {
            await CreateAsync(record, cancellationToken);

            return;
        }

        SharedSkillsMapper.CopyTo(record, skill);
        await _skillManager.UpdateAsync(skill, cancellationToken: cancellationToken);
    }

    public async ValueTask<bool> DeleteAsync(ContactCenterSkill entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var skill = await _skillManager.FindByIdAsync(entry.ItemId, cancellationToken);

        return skill is not null && await _skillManager.DeleteAsync(skill, cancellationToken);
    }
}
