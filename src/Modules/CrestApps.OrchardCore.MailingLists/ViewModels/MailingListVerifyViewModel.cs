using CrestApps.OrchardCore.MailingLists.Services;

namespace CrestApps.OrchardCore.MailingLists.ViewModels;

/// <summary>
/// The page a confirmation or claim link opens.
/// </summary>
public class MailingListVerifyViewModel
{
    /// <summary>
    /// Gets or sets the token from the link.
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets what the link did or can do.
    /// </summary>
    public MailingListVerifyOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets the name of the list.
    /// </summary>
    public string ListName { get; set; }

    /// <summary>
    /// Gets or sets the address the link was sent to.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets whether the person is already subscribed and only verifies the address (a claim link).
    /// </summary>
    public bool AlreadySubscribed { get; set; }

    /// <summary>
    /// Gets or sets whether a new link can be asked for.
    /// </summary>
    public bool CanResend { get; set; }

    /// <summary>
    /// Gets or sets whether a new link was asked for.
    /// </summary>
    public bool Resent { get; set; }

    /// <summary>
    /// Gets or sets whether another change of the same sign-up was running, so the person should try again.
    /// </summary>
    public bool Busy { get; set; }

    /// <summary>
    /// Gets or sets the welcome coupon issued with the verification.
    /// </summary>
    public MailingListIssuedCoupon Coupon { get; set; }
}
