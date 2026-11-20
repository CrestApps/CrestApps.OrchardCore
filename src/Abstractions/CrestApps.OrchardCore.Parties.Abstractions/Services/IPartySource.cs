using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Parties.Services;

/// <summary>
/// Finds and names the parties of one kind, such as users or accounts. The module that keeps such records registers one
/// with <see cref="PartiesServiceCollectionExtensions.AddPartySource{TSource}"/>, so pickers and lists in any module
/// find and name them without knowing the module.
/// </summary>
public interface IPartySource
{
    /// <summary>
    /// Gets the kind of party this source finds, one of <see cref="PartyKinds"/> or a kind a module adds.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Finds parties whose name starts with or contains the text.
    /// </summary>
    /// <param name="query">The text typed, or empty for the first ones.</param>
    /// <param name="count">The most to return.</param>
    /// <param name="cancellationToken">A token to cancel the search.</param>
    /// <returns>The parties found, by name.</returns>
    Task<IReadOnlyList<PartyOption>> SearchAsync(string query, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the names of parties by their ids. Ids that aren't found are left out.
    /// </summary>
    /// <param name="ids">The ids.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The names by id.</returns>
    Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets how to reach parties: their name, e-mail address and phone number, for the ids that are found. A source
    /// whose parties can't be reached (or that doesn't know how) returns nothing, which is the default.
    /// </summary>
    /// <param name="ids">The ids.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The contact details by id.</returns>
    Task<IReadOnlyDictionary<string, ContactInfo>> GetContactInfoAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<string, ContactInfo>>(new Dictionary<string, ContactInfo>());
}
