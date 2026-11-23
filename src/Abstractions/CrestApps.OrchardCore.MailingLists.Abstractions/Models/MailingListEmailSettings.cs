using CrestApps.OrchardCore.Emails;

namespace CrestApps.OrchardCore.MailingLists.Models;

/// <summary>
/// The site's own templates of the mailing lists e-mails (confirm, welcome, coupon claim) and a layout around them.
/// Kept in the site settings; edited under <b>Settings &gt; Mailing lists</b>.
/// </summary>
public sealed class MailingListEmailSettings
{
    /// <summary>
    /// Gets or sets the HTML around every mailing list e-mail, with <c>{{ Content }}</c> where the e-mail goes.
    /// </summary>
    public string Layout { get; set; }

    /// <summary>
    /// Gets or sets the templates, by kind (<see cref="MailingListsConstants.EmailKinds"/>).
    /// </summary>
    public Dictionary<string, TemplatedEmailTemplate> Templates { get; set; } = new(StringComparer.Ordinal);
}
