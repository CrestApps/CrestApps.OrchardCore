namespace CrestApps.OrchardCore.Emails;

/// <summary>
/// An email as it was built: its subject, its text and HTML bodies, and the values the site's template of it can use.
/// </summary>
public sealed class TemplatedEmailContent
{
    /// <summary>
    /// Gets the subject.
    /// </summary>
    public string Subject { get; init; }

    /// <summary>
    /// Gets the text body, or <see langword="null"/> for an email with only an HTML body.
    /// </summary>
    public string Text { get; init; }

    /// <summary>
    /// Gets the HTML body, or <see langword="null"/> for an email with only a text body.
    /// </summary>
    public string Html { get; init; }

    /// <summary>
    /// Gets the email's own values the site's template can use, such as the link to pay, by name.
    /// </summary>
    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();
}
