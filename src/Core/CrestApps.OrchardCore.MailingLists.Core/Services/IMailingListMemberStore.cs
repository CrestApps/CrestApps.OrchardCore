using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// Reads and writes mailing list members. Writes go through <see cref="IMailingListMemberService"/> only, so its
/// observers, consent checks, provider outbox and timeline always run; the store is public for the admin pages and the
/// data sets that read members.
/// </summary>
public interface IMailingListMemberStore
{
    /// <summary>
    /// Returns the member with the given id.
    /// </summary>
    /// <param name="memberId">The member's id.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The member, or <see langword="null"/>.</returns>
    Task<MailingListMember> FindByIdAsync(string memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the member of a list with the given address.
    /// </summary>
    /// <param name="listId">The list's id.</param>
    /// <param name="normalizedEmail">The normalized address.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The member, or <see langword="null"/>.</returns>
    Task<MailingListMember> FindAsync(string listId, string normalizedEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every membership of an address, on every list.
    /// </summary>
    /// <param name="normalizedEmail">The normalized address.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The memberships.</returns>
    Task<IReadOnlyList<MailingListMember>> ListByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every membership of a user, on every list.
    /// </summary>
    /// <param name="userId">The user's id.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The memberships.</returns>
    Task<IReadOnlyList<MailingListMember>> ListByUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of a list's members, newest first.
    /// </summary>
    /// <param name="query">The list, the state and the address search.</param>
    /// <param name="skip">How many members to skip.</param>
    /// <param name="take">How many members to return.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The total that match and the page.</returns>
    Task<(int Count, IReadOnlyList<MailingListMember> Members)> PageAsync(MailingListMemberQuery query, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of the subscribed members of a list in the order they were stored, for paging through a whole list
    /// without counting it.
    /// </summary>
    /// <param name="listId">The list's id.</param>
    /// <param name="cursor">The cursor the previous page returned, or <see langword="null"/> for the first page.</param>
    /// <param name="take">How many members to return.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The members, and the cursor of the next page or <see langword="null"/> on the last page.</returns>
    Task<(IReadOnlyList<MailingListMember> Members, string NextCursor)> ListSubscribedAsync(string listId, string cursor, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts a list's members in a state.
    /// </summary>
    /// <param name="listId">The list's id.</param>
    /// <param name="status">The state.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The count.</returns>
    Task<int> CountAsync(string listId, MailingListMemberStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts a list's members whose last change did not reach the list's provider.
    /// </summary>
    /// <param name="listId">The list's id.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The count.</returns>
    Task<int> CountNotSyncedAsync(string listId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a member.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>A task that completes when the member is staged.</returns>
    Task CreateAsync(MailingListMember member, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a changed member.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>A task that completes when the change is staged.</returns>
    Task UpdateAsync(MailingListMember member, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a member.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>A task that completes when the removal is staged.</returns>
    Task DeleteAsync(MailingListMember member, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the staged changes of the unit of work, so they are visible to other requests before a lock is released.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>A task that completes when the changes are written.</returns>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
