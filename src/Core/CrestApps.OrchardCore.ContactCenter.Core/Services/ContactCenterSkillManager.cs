using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterSkillManager"/>.
/// </summary>
public sealed class ContactCenterSkillManager : CatalogManager<ContactCenterSkill>, IContactCenterSkillManager
{
    private readonly IContactCenterSkillStore _store;
    private readonly IContactCenterConfigurationCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSkillManager"/> class.
    /// </summary>
    /// <param name="store">The underlying skill store.</param>
    /// <param name="handlers">The catalog entry handlers for skills.</param>
    /// <param name="cache">The routing configuration cache used to serve enabled skills without re-querying the store.</param>
    /// <param name="logger">The logger instance.</param>
    public ContactCenterSkillManager(
        IContactCenterSkillStore store,
        IEnumerable<ICatalogEntryHandler<ContactCenterSkill>> handlers,
        IContactCenterConfigurationCache cache,
        ILogger<CatalogManager<ContactCenterSkill>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
        _cache = cache;
    }

    /// <inheritdoc/>
    public async Task<ContactCenterSkill> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var skill = await _store.FindByNameAsync(name, cancellationToken);

        if (skill is not null)
        {
            await LoadAsync(skill, cancellationToken);
        }

        return skill;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ContactCenterSkill>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetEnabledAsync(async token =>
        {
            var skills = await _store.ListEnabledAsync(token);

            foreach (var skill in skills)
            {
                await LoadAsync(skill, token);
            }

            return skills;
        }, cancellationToken);
    }
}
