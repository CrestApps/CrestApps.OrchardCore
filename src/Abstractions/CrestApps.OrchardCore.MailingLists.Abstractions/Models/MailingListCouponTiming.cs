namespace CrestApps.OrchardCore.MailingLists.Models;

/// <summary>
/// When a mailing list issues its welcome coupon.
/// </summary>
public enum MailingListCouponTiming
{
    /// <summary>
    /// When the person signs up; on a list with confirmation, the confirmation e-mail carries the code.
    /// </summary>
    AtSignup = 0,

    /// <summary>
    /// When the person's address is verified (a confirmation or claim link, a confirmed account e-mail or the
    /// provider's confirmation).
    /// </summary>
    AfterVerification = 1,
}
