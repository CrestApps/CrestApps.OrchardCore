using CrestApps.OrchardCore.Omnichannel.Models;

namespace CrestApps.OrchardCore.Omnichannel.Services;

/// <summary>
/// Reads the kinds of subject a tenant has defined.
/// </summary>
public interface ISubjectDefinitionProvider
{
    /// <summary>
    /// Gets every subject definition.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The definitions, ordered by the name to show.</returns>
    Task<IReadOnlyCollection<SubjectDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds one subject definition by technical name.
    /// </summary>
    /// <param name="name">The technical name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The definition, or <see langword="null"/> when there is no such kind of subject.</returns>
    Task<SubjectDefinition> FindAsync(string name, CancellationToken cancellationToken = default);
}
