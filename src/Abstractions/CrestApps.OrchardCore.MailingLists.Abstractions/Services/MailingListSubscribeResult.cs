using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.MailingLists.Services;

/// <summary>
/// The outcome of <see cref="IMailingListMemberService.SubscribeAsync"/>.
/// </summary>
public sealed class MailingListSubscribeResult
{
    /// <summary>
    /// Gets what happened.
    /// </summary>
    public MailingListSubscribeOutcome Outcome { get; init; }

    /// <summary>
    /// Gets the member, when one was created or updated.
    /// </summary>
    public MailingListMember Member { get; init; }

    /// <summary>
    /// Gets why the sign-up was refused, for logs and the admin; never shown to the person.
    /// </summary>
    public string Error { get; init; }

    /// <summary>
    /// Gets the coupon to show on the page, when the list shows its welcome coupon right away.
    /// </summary>
    public MailingListIssuedCoupon Coupon { get; init; }

    /// <summary>
    /// Gets whether the sign-up was accepted (subscribed, waiting for confirmation, or already on the list).
    /// </summary>
    public bool Succeeded
        => Outcome != MailingListSubscribeOutcome.Refused;
}

/// <summary>
/// What a sign-up did.
/// </summary>
public enum MailingListSubscribeOutcome
{
    /// <summary>
    /// The address is subscribed.
    /// </summary>
    Subscribed,

    /// <summary>
    /// A confirmation e-mail was sent; the address is pending.
    /// </summary>
    ConfirmationSent,

    /// <summary>
    /// The address was already subscribed; nothing changed.
    /// </summary>
    AlreadySubscribed,

    /// <summary>
    /// The sign-up was refused (an invalid address, a blocked address, too many tries, a missing list).
    /// </summary>
    Refused,
}
