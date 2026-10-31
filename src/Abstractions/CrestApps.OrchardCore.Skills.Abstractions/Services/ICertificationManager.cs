using CrestApps.Core.Services;
using CrestApps.OrchardCore.Skills.Models;

namespace CrestApps.OrchardCore.Skills.Services;

/// <summary>
/// Manages the certification catalog. Writes run the catalog handlers, which validate and stamp the records.
/// </summary>
public interface ICertificationManager : ICatalogManager<Certification>
{
    /// <summary>
    /// Finds the certification with a name.
    /// </summary>
    /// <param name="name">The certification name.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The certification, or <see langword="null"/> when there is none.</returns>
    Task<Certification> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the enabled certifications, by name.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The enabled certifications.</returns>
    Task<IReadOnlyList<Certification>> GetEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of the certifications whose name contains a text, by name.
    /// </summary>
    /// <param name="search">The text, or <see langword="null"/> for every certification.</param>
    /// <param name="skip">How many certifications to skip.</param>
    /// <param name="take">How many certifications to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many match, and the page.</returns>
    Task<(int Count, IReadOnlyList<Certification> Items)> SearchAsync(string search, int skip, int take, CancellationToken cancellationToken = default);
}
