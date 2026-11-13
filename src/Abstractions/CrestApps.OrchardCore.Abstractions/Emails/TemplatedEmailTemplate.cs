namespace CrestApps.OrchardCore.Emails;

/// <summary>
/// A site's template of one email: a subject and a body with Liquid, used when it's on.
/// </summary>
public sealed class TemplatedEmailTemplate
{
    /// <summary>
    /// Gets or sets a value indicating whether the template is used.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the subject, with Liquid; the email's own when empty.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the body, as HTML with Liquid; <c>{{ Content }}</c> is the email as the site would send it.
    /// </summary>
    public string Body { get; set; }
}
