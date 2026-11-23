using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// Decides what a confirmation or claim link can do for its member, without changing anything.
/// </summary>
public static class MailingListVerification
{
    /// <summary>
    /// Returns what a link does: <see cref="MailingListVerifyOutcome.Ready"/> when it may verify the address now,
    /// <see cref="MailingListVerifyOutcome.AlreadyVerified"/> for a second click on a subscribed, verified member, and
    /// <see cref="MailingListVerifyOutcome.Expired"/> when the member left the list, a newer link was sent or the link's
    /// time is over. A member that left is never brought back by an old link.
    /// </summary>
    /// <param name="member">The member the link names.</param>
    /// <param name="ticket">What the link's token holds.</param>
    /// <param name="nowUtc">The current time (UTC).</param>
    /// <returns>The outcome.</returns>
    public static MailingListVerifyOutcome Evaluate(MailingListMember member, MailingListVerificationTicket ticket, DateTime nowUtc)
    {
        if (member is null || ticket is null || !string.Equals(member.ItemId, ticket.MemberId, StringComparison.Ordinal))
        {
            return MailingListVerifyOutcome.Invalid;
        }

        if (member.Status is MailingListMemberStatus.Unsubscribed or MailingListMemberStatus.Cleaned)
        {
            return MailingListVerifyOutcome.Expired;
        }

        if (member.Status == MailingListMemberStatus.Subscribed && member.EmailVerifiedUtc is not null)
        {
            return MailingListVerifyOutcome.AlreadyVerified;
        }

        if (string.IsNullOrEmpty(member.VerificationNonce) || !string.Equals(member.VerificationNonce, ticket.Nonce, StringComparison.Ordinal))
        {
            return MailingListVerifyOutcome.Expired;
        }

        return ticket.ExpiresUtc <= nowUtc
            ? MailingListVerifyOutcome.Expired
            : MailingListVerifyOutcome.Ready;
    }

    /// <summary>
    /// Returns whether a new link may be sent to the member: a pending sign-up, or a subscribed member whose address is
    /// not verified yet (a claim link).
    /// </summary>
    /// <param name="member">The member.</param>
    /// <returns><see langword="true"/> when a new link can be sent.</returns>
    public static bool CanResend(MailingListMember member)
        => member is not null
            && (member.Status == MailingListMemberStatus.Pending
                || (member.Status == MailingListMemberStatus.Subscribed && member.EmailVerifiedUtc is null));
}
