using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.MailingLists.ViewModels;

/// <summary>
/// The Confirmation card of the mailing list editor.
/// </summary>
public class MailingListConfirmationViewModel
{
    /// <summary>
    /// Gets or sets how a sign-up is confirmed.
    /// </summary>
    public MailingListConfirmation Confirmation { get; set; }

    /// <summary>
    /// Gets or sets whether a signed-in user with a confirmed account e-mail skips the confirmation.
    /// </summary>
    public bool SkipConfirmationForVerifiedUsers { get; set; }

    /// <summary>
    /// Gets or sets how many days a confirmation link works.
    /// </summary>
    public int LinkLifetimeDays { get; set; }
}
