using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the management contract for Contact Center skills.
/// </summary>
public interface IContactCenterSkillManager : ICatalogManager<ContactCenterSkill>
{
    /// <summary>
    /// Finds the skill with the specified unique name.
    /// </summary>
    /// <param name="name">The skill name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching skill, or <see langword="null"/> when none exists.</returns>
    Task<ContactCenterSkill> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every enabled skill.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The enabled skills.</returns>
    Task<IReadOnlyCollection<ContactCenterSkill>> ListEnabledAsync(CancellationToken cancellationToken = default);
}
