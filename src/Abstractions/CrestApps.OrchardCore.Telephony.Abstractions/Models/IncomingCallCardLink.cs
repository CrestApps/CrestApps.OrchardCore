namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents a link a module contributes to an incoming-call card, such as a shortcut that opens a
/// related record. Links are rendered as clickable actions next to the matched record in the
/// incoming-call modal.
/// </summary>
public sealed class IncomingCallCardLink
{
    /// <summary>
    /// Gets or sets the visible text of the link.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the URL the link navigates to.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the optional CSS class of the icon shown next to the link.
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the link opens in a new browser tab. Defaults to <see langword="true"/>.
    /// </summary>
    public bool OpenInNewTab { get; set; } = true;
}
