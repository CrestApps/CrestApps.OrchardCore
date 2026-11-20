using CrestApps.OrchardCore.Emails;

namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// The site's own templates of the e-mails Tickets sends requesters (received, reply, solved), and a layout around
/// them. Kept in the site settings; edited under <b>Settings &gt; Tickets &gt; E-mails</b>.
/// </summary>
public sealed class TicketEmailSettings
{
    /// <summary>
    /// Gets or sets the HTML around every ticket e-mail, with <c>{{ Content }}</c> where the e-mail goes.
    /// </summary>
    public string Layout { get; set; }

    /// <summary>
    /// Gets or sets the templates, by kind (<see cref="TicketsConstants.EmailKinds"/>).
    /// </summary>
    public Dictionary<string, TemplatedEmailTemplate> Templates { get; set; } = new(StringComparer.Ordinal);
}
