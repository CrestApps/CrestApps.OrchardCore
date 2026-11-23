using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;

namespace CrestApps.OrchardCore.MailingLists.Core.Emails;

/// <summary>
/// Sends the mailing lists e-mails through the durable templated e-mail outbox. Every e-mail carries the list's
/// unsubscribe link.
/// </summary>
public interface IMailingListEmails
{
    /// <summary>
    /// Sends the e-mail with the link that confirms a sign-up (double opt-in), with the welcome coupon when one was
    /// issued at sign-up.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="member">The member, with its current verification nonce.</param>
    /// <param name="coupon">The welcome coupon, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the e-mail left or waits for a retry.</returns>
    Task<bool> SendConfirmAsync(MailingList list, MailingListMember member, MailingListIssuedCoupon coupon, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the welcome e-mail, with the welcome coupon when one was issued.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="member">The member.</param>
    /// <param name="coupon">The welcome coupon, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the e-mail left or waits for a retry.</returns>
    Task<bool> SendWelcomeAsync(MailingList list, MailingListMember member, MailingListIssuedCoupon coupon, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a subscribed member the welcome e-mail with a link that verifies the address and claims what waits for
    /// the verification (the welcome coupon).
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="member">The member, with its current verification nonce.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the e-mail left or waits for a retry.</returns>
    Task<bool> SendClaimAsync(MailingList list, MailingListMember member, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the stored e-mails sent to a member, for an erasure.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>How many stored e-mails were deleted.</returns>
    Task<int> ForgetAsync(MailingListMember member, CancellationToken cancellationToken = default);
}
