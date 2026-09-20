using CrestApps.Core.Omnichannel.Models;

namespace CrestApps.Core.Omnichannel.Services;

/// <summary>
/// Reads the kinds of contact a tenant has defined.
/// </summary>
public interface IContactDefinitionProvider
{
    /// <summary>
    /// Gets every contact definition.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The definitions, ordered by the name to show.</returns>
    Task<IReadOnlyCollection<ContactDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds one contact definition by technical name.
    /// </summary>
    /// <param name="name">The technical name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The definition, or <see langword="null"/> when there is no such kind of contact.</returns>
    Task<ContactDefinition> FindAsync(string name, CancellationToken cancellationToken = default);
}
