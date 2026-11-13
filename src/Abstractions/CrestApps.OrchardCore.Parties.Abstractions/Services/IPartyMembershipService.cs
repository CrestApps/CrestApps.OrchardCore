using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Parties.Services;

/// <summary>
/// Answers who may act for a party, across every kind, through the registered <see cref="IPartyMemberSource"/>s. A user
/// always acts for themselves: the party <c>PartyRef(User, userId)</c> has that user as its member even when no source
/// of users is registered.
/// </summary>
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
    /// Gets the ids of the users who may act for a party, from every source of its kind, each once.
    /// </summary>
    /// <param name="party">The party.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The user ids, or none.</returns>
    Task<IReadOnlyList<string>> GetMemberUserIdsAsync(PartyRef party, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the parties of every kind a user may act for, always starting with the user themselves as
    /// <c>PartyRef(User, userId)</c>, each once.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The parties, or none for an empty user id.</returns>
    Task<IReadOnlyList<PartyRef>> GetPartiesAsync(string userId, CancellationToken cancellationToken = default);
}
