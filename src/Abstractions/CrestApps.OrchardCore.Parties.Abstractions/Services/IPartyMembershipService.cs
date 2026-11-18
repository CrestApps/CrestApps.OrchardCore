using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Parties.Services;

/// <summary>
/// Answers who may act for a party, across every kind, through the registered <see cref="IPartyMemberSource"/>s. A user
/// always acts for themselves: the party <c>PartyRef(User, userId)</c> has that user as its member, with every access
/// level, even when no source of users is registered.
/// </summary>
/// <remarks>
/// Membership comes in levels (<see cref="PartyAccess"/>). The overloads without one answer for
/// <see cref="PartyAccess.Member"/>. Anything that shows or settles a party's money (what it owes, its receipts, paying
/// for it, its saved payment methods) asks for <see cref="PartyAccess.Billing"/>, so a plain member of a business account
/// sees only what is theirs, and an admin of the account sees all of it.
/// </remarks>
public interface IPartyMembershipService
{
    /// <summary>
    /// Gets whether a user may act for a party.
    /// </summary>
    /// <param name="party">The party.</param>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns><see langword="true"/> when the user is one of the party's members.</returns>
    Task<bool> IsMemberAsync(PartyRef party, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a user has at least an access level for a party.
    /// </summary>
    /// <param name="party">The party.</param>
    /// <param name="userId">The user id.</param>
    /// <param name="access">The least access the user must have.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns><see langword="true"/> when the user has that access.</returns>
    Task<bool> IsMemberAsync(PartyRef party, string userId, PartyAccess access, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the ids of the users who may act for a party, from every source of its kind, each once.
    /// </summary>
    /// <param name="party">The party.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The user ids, or none.</returns>
    Task<IReadOnlyList<string>> GetMemberUserIdsAsync(PartyRef party, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the ids of the users who have at least an access level for a party, from every source of its kind, each once.
    /// </summary>
    /// <param name="party">The party.</param>
    /// <param name="access">The least access the users must have.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The user ids, or none.</returns>
    Task<IReadOnlyList<string>> GetMemberUserIdsAsync(PartyRef party, PartyAccess access, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the parties of every kind a user may act for, always starting with the user themselves as
    /// <c>PartyRef(User, userId)</c>, each once.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The parties, or none for an empty user id.</returns>
    Task<IReadOnlyList<PartyRef>> GetPartiesAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the parties of every kind a user has at least an access level for, always starting with the user themselves as
    /// <c>PartyRef(User, userId)</c>, each once.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="access">The least access the user must have.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The parties, or none for an empty user id.</returns>
    Task<IReadOnlyList<PartyRef>> GetPartiesAsync(string userId, PartyAccess access, CancellationToken cancellationToken = default);
}
