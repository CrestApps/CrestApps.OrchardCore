using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Parties.Services;

/// <summary>
/// Tells which parties a signed-in user is, besides the user party itself: the module that owns a kind of party that
/// can be linked to a user (a staff member, a customer record) answers for its kind. Self-service pages use it to show
/// a person the records they own, such as their bank accounts. Register one with
/// <see cref="PartiesServiceCollectionExtensions.AddUserPartySource{TSource}"/>.
/// </summary>
public interface IUserPartySource
{
    /// <summary>
    /// Returns the parties of this source's kind that a user is.
    /// </summary>
    /// <param name="userId">The user's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The parties; empty when the user is none.</returns>
    Task<IReadOnlyList<PartyRef>> GetPartiesAsync(string userId, CancellationToken cancellationToken = default);
}
