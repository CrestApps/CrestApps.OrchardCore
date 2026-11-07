using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Parties.Services;

/// <summary>
/// Reads parties of every kind through the registered <see cref="IPartySource"/>s: which kinds can be picked, finding
/// them and naming them.
/// </summary>
public interface IPartyDirectory
{
    /// <summary>
    /// Gets the kinds that have a source, in registration order.
    /// </summary>
    IReadOnlyList<string> Kinds { get; }

    /// <summary>
    /// Gets the source of a kind, or <see langword="null"/> when nothing registers one.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The source.</returns>
    IPartySource GetSource(string kind);

    /// <summary>
    /// Finds parties of a kind.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="query">The text typed.</param>
    /// <param name="count">The most to return.</param>
    /// <param name="cancellationToken">A token to cancel the search.</param>
    /// <returns>The parties found, or none when the kind has no source.</returns>
    Task<IReadOnlyList<PartyOption>> SearchAsync(string kind, string query, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a party's name.
    /// </summary>
    /// <param name="party">The party.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The name, or <see langword="null"/> when it isn't found or its kind has no source.</returns>
    Task<string> GetNameAsync(PartyRef party, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the names of several parties, one lookup per kind.
    /// </summary>
    /// <param name="parties">The parties.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The names of the parties found.</returns>
    Task<IReadOnlyDictionary<PartyRef, string>> GetNamesAsync(IEnumerable<PartyRef> parties, CancellationToken cancellationToken = default);
}
