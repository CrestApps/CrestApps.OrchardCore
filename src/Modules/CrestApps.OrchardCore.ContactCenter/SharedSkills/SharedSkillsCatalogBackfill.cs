using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.ContactCenter.SharedSkills;

/// <summary>
/// Copies the Contact Center skills that aren't in the shared catalog yet, with the same ids, the first time the
/// catalog is read after Skills was turned on. A skill whose name the catalog already has is left out: the catalog's
/// skill answers to that name, and Contact Center names skills by their name.
/// </summary>
internal sealed class SharedSkillsCatalogBackfill
{
    private const string LockName = "CONTACT_CENTER_SHARED_SKILLS_BACKFILL";

    private readonly SharedSkillsCatalogBackfillState _state;
    private readonly ContactCenterSkillStore _contactCenterStore;
    private readonly ISkillManager _skillManager;
    private readonly IDistributedLock _distributedLock;

    public SharedSkillsCatalogBackfill(
        SharedSkillsCatalogBackfillState state,
        ContactCenterSkillStore contactCenterStore,
        ISkillManager skillManager,
        IDistributedLock distributedLock)
    {
        _state = state;
        _contactCenterStore = contactCenterStore;
        _skillManager = skillManager;
        _distributedLock = distributedLock;
    }

    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        if (_state.Done)
        {
            return;
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(LockName, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1));

        if (!locked)
        {
            return;
        }

        await using (locker)
        {
            if (_state.Done)
            {
                return;
            }

            var shared = await _skillManager.GetAllAsync(cancellationToken);
            var sharedIds = shared.Select(skill => skill.ItemId).ToHashSet(StringComparer.Ordinal);
            var sharedNames = shared.Select(skill => SharedSkillsMapper.NormalizeName(skill.Name)).Where(name => name is not null).ToHashSet(StringComparer.Ordinal);

            foreach (var skill in await _contactCenterStore.GetAllAsync(cancellationToken))
            {
                var name = SharedSkillsMapper.NormalizeName(skill.Name);

                if (sharedIds.Contains(skill.ItemId) || name is null || !sharedNames.Add(name))
                {
                    continue;
                }

                var copy = new Skill();

                SharedSkillsMapper.CopyTo(skill, copy);
                await _skillManager.CreateAsync(copy, cancellationToken);
                sharedIds.Add(copy.ItemId);
            }

            _state.Done = true;
        }
    }
}
