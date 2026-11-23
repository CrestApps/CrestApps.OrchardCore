using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.MailingLists.Services;

/// <summary>
/// The outcome of verifying, or reading, a confirmation or claim link.
/// </summary>
public sealed class MailingListVerifyResult
{
    /// <summary>
    /// Gets what happened.
    /// </summary>
    public MailingListVerifyOutcome Outcome { get; init; }

    /// <summary>
    /// Gets the member the link points at.
    /// </summary>
    public MailingListMember Member { get; init; }

    /// <summary>
    /// Gets the list the member is on.
    /// </summary>
    public MailingList List { get; init; }

    /// <summary>
    /// Gets the welcome coupon to show, when one was issued.
    /// </summary>
    public MailingListIssuedCoupon Coupon { get; init; }
}

/// <summary>
/// What a verification link did.
/// </summary>
public enum MailingListVerifyOutcome
{
    /// <summary>
    /// The link is valid and waits for the person's click (when only read).
    /// </summary>
    Ready,

    /// <summary>
    /// The address is verified now.
    /// </summary>
    Verified,

    /// <summary>
    /// The address was verified earlier; nothing changed.
    /// </summary>
    AlreadyVerified,

    /// <summary>
    /// The link expired or a newer link was sent; the person can ask for a new one.
    /// </summary>
    Expired,

    /// <summary>
    /// The link is not valid.
    /// </summary>
    Invalid,
}
