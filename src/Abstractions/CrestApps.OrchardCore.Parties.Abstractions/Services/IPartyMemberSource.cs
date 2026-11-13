using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Parties.Services;

/// <summary>
/// Knows the users who may act for the parties of one kind, such as an account's members or the user a customer record
/// belongs to. A portal and a ledger's visibility checks use it through <see cref="IPartyMembershipService"/>. Register
/// one with <see cref="PartiesServiceCollectionExtensions.AddPartyMemberSource{TSource}"/>.
/// </summary>
public interface IPartyMemberSource
{
    /// <summary>
    /// Gets the kind of party the source knows, one of <see cref="PartyKinds"/> or a kind a module adds.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Gets the ids of the users who may act for a party.
    /// </summary>
    /// <param name="party">The party, of the source's kind.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The user ids, or none when the party isn't found or has no members.</returns>
    Task<IReadOnlyList<string>> GetMemberUserIdsAsync(PartyRef party, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the parties of the source's kind a user may act for.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The parties, or none.</returns>
    Task<IReadOnlyList<PartyRef>> GetPartiesAsync(string userId, CancellationToken cancellationToken = default);
}
