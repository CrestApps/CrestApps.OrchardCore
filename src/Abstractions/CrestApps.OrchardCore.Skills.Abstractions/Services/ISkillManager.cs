using CrestApps.Core.Services;
using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// Manages the skill catalog. Writes run the catalog handlers, which validate and stamp the records.
/// </summary>
public interface ISkillManager : ICatalogManager<Skill>
{
    /// <summary>
    /// Finds the skill with a name.
    /// </summary>
    /// <param name="name">The skill name.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The skill, or <see langword="null"/> when there is none.</returns>
    Task<Skill> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the enabled skills, by name.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The enabled skills.</returns>
    Task<IReadOnlyList<Skill>> GetEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of the skills whose name contains a text, by name.
    /// </summary>
    /// <param name="search">The text, or <see langword="null"/> for every skill.</param>
    /// <param name="skip">How many skills to skip.</param>
    /// <param name="take">How many skills to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many match, and the page.</returns>
    Task<(int Count, IReadOnlyList<Skill> Items)> SearchAsync(string search, int skip, int take, CancellationToken cancellationToken = default);
}
