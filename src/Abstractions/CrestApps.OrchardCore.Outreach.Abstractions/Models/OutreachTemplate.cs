namespace CrestApps.OrchardCore.Outreach.Models;

/// <summary>
/// What an outreach run sends, before it is rendered for a recipient.
/// </summary>
public sealed class OutreachTemplate
{
    /// <summary>
    /// Gets or sets the subject, for channels that have one.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets whether the body is HTML.
    /// </summary>
    public bool IsHtml { get; set; }
}
