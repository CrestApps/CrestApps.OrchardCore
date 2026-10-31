using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.ContactCenter.SharedSkills;

/// <summary>
/// Keeps a copy of every shared catalog skill in the Contact Center store, and clears Contact Center's cached skill
/// list, whichever screen changed the skill.
/// </summary>
internal sealed class ContactCenterSkillMirrorHandler : CatalogEntryHandlerBase<Skill>
{
    private readonly ContactCenterSkillStore _contactCenterStore;
    private readonly IContactCenterConfigurationCache _cache;

    public ContactCenterSkillMirrorHandler(
        ContactCenterSkillStore contactCenterStore,
        IContactCenterConfigurationCache cache)
    {
        _contactCenterStore = contactCenterStore;
        _cache = cache;
    }

    public override Task CreatedAsync(CreatedContext<Skill> context, CancellationToken cancellationToken = default)
        => MirrorAsync(context.Model, cancellationToken);

    public override Task UpdatedAsync(UpdatedContext<Skill> context, CancellationToken cancellationToken = default)
        => MirrorAsync(context.Model, cancellationToken);

    public override async Task DeletedAsync(DeletedContext<Skill> context, CancellationToken cancellationToken = default)
    {
        var copy = await _contactCenterStore.FindByIdAsync(context.Model.ItemId, cancellationToken);

        if (copy is not null)
        {
            await _contactCenterStore.DeleteAsync(copy, cancellationToken);
        }

        await _cache.InvalidateEnabledAsync<ContactCenterSkill>();
    }

    private async Task MirrorAsync(Skill skill, CancellationToken cancellationToken)
    {
        var copy = await _contactCenterStore.FindByIdAsync(skill.ItemId, cancellationToken);

        if (copy is null)
        {
            await _contactCenterStore.CreateAsync(SharedSkillsMapper.ToContactCenter(skill), cancellationToken);
        }
        else
        {
            SharedSkillsMapper.CopyTo(skill, copy);
            await _contactCenterStore.UpdateAsync(copy, cancellationToken);
        }

        await _cache.InvalidateEnabledAsync<ContactCenterSkill>();
    }
}
