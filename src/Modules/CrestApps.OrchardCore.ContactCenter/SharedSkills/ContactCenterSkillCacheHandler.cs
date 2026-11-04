using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.ContactCenter.SharedSkills;

/// <summary>
/// Refreshes Contact Center's cached skills whenever a skill of the shared catalog changes, wherever it is edited.
/// </summary>
internal sealed class ContactCenterSkillCacheHandler : CatalogEntryHandlerBase<Skill>
{
    private readonly IContactCenterConfigurationCache _cache;

    public ContactCenterSkillCacheHandler(IContactCenterConfigurationCache cache)
    {
        _cache = cache;
    }

    public override Task CreatedAsync(CreatedContext<Skill> context, CancellationToken cancellationToken = default)
        => _cache.InvalidateEnabledAsync<ContactCenterSkill>();

    public override Task UpdatedAsync(UpdatedContext<Skill> context, CancellationToken cancellationToken = default)
        => _cache.InvalidateEnabledAsync<ContactCenterSkill>();

    public override Task DeletedAsync(DeletedContext<Skill> context, CancellationToken cancellationToken = default)
        => _cache.InvalidateEnabledAsync<ContactCenterSkill>();
}
