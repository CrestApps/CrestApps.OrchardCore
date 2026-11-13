using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Parties.Services;

/// <summary>
/// Knows the users who may act for the parties of one kind, such as an account's members or the user a customer record
/// belongs to. A portal and a ledger's visibility checks use it through <see cref="IPartyMembershipService"/>. Register
/// one with <see cref="PartiesServiceCollectionExtensions.AddPartyMemberSource{TSource}"/>.
/// </summary>
/// <remarks>
/// A source says who has <see cref="PartyAccess.Billing"/> through the overloads that take a <see cref="PartyAccess"/>.
/// Their default implementations grant it to no one, so a source that does not override them never opens a party's money
/// to its members by mistake.
/// </remarks>
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

    /// <summary>
    /// Gets the ids of the users who have at least an access level for a party.
    /// </summary>
    /// <param name="party">The party, of the source's kind.</param>
    /// <param name="access">The least access the users must have.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The user ids, or none. The default gives every member for <see cref="PartyAccess.Member"/> and no one above it.</returns>
    Task<IReadOnlyList<string>> GetMemberUserIdsAsync(PartyRef party, PartyAccess access, CancellationToken cancellationToken = default)
        => access == PartyAccess.Member
            ? GetMemberUserIdsAsync(party, cancellationToken)
            : Task.FromResult<IReadOnlyList<string>>([]);

    /// <summary>
    /// Gets the parties of the source's kind a user has at least an access level for.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="access">The least access the user must have.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The parties, or none. The default gives every party for <see cref="PartyAccess.Member"/> and none above it.</returns>
    Task<IReadOnlyList<PartyRef>> GetPartiesAsync(string userId, PartyAccess access, CancellationToken cancellationToken = default)
        => access == PartyAccess.Member
            ? GetPartiesAsync(userId, cancellationToken)
            : Task.FromResult<IReadOnlyList<PartyRef>>([]);
}
