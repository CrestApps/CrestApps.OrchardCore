using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.MailingLists.Services;

/// <summary>
/// Signs people up to mailing lists, verifies their addresses, unsubscribes them, and applies the changes a list's
/// provider reports. Every path goes through it, so observers, consent, the provider outbox and the timeline always run.
/// </summary>
public interface IMailingListMemberService
{
    /// <summary>
    /// Signs an address up to a list: subscribes it, or sends the confirmation e-mail, as the list says.
    /// </summary>
    /// <param name="request">The list, the person and how they signed up.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>What happened. Show the person the same message whether or not they were already on the list.</returns>
    Task<MailingListSubscribeResult> SubscribeAsync(MailingListSubscribeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the address a confirmation or claim link was sent to, and subscribes a pending member.
    /// </summary>
    /// <param name="token">The token from the link.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>What happened, with the member and any coupon to show.</returns>
    Task<MailingListVerifyResult> VerifyAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the member a verification token points at, without changing anything, so a page can show what the link
    /// confirms before the person clicks.
    /// </summary>
    /// <param name="token">The token from the link.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The member and list, or a result that says the link is not valid.</returns>
    Task<MailingListVerifyResult> ReadVerificationAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a pending member a new confirmation link; older links stop working.
    /// </summary>
    /// <param name="memberId">The member's id.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a link was sent.</returns>
    Task<bool> ResendConfirmationAsync(string memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes an address from one list, or from every list when <paramref name="listId"/> is empty.
    /// </summary>
    /// <param name="listId">The list's id, or <see langword="null"/> for every list.</param>
    /// <param name="email">The address.</param>
    /// <param name="source">Who or what unsubscribed it, such as <c>UnsubscribeLink</c>, <c>Admin</c> or <c>Consent</c>.</param>
    /// <param name="reason">Why, when known.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>How many memberships changed.</returns>
    Task<int> UnsubscribeAsync(string listId, string email, string source, string reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a change the list's provider reported (a webhook or the reconciliation). A change older than the
    /// member's last status change is ignored.
    /// </summary>
    /// <param name="change">The change.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the member changed.</returns>
    Task<bool> ApplyProviderChangeAsync(MailingListProviderChange change, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queues a member's state for the list's provider again after every attempt to send its last change failed: an
    /// unsubscribed member as an unsubscribe, any other member as an upsert. A member whose last change did not fail
    /// is left as it is.
    /// </summary>
    /// <param name="memberId">The member's id.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the member's state was queued again.</returns>
    Task<bool> RetrySyncAsync(string memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Erases every membership of an address for a privacy request, and asks each provider to erase it too.
    /// </summary>
    /// <param name="email">The address.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>How many memberships were erased.</returns>
    Task<int> EraseAsync(string email, CancellationToken cancellationToken = default);
}
