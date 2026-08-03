namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents a record a module contributes to an incoming-call modal, such as a customer matched by
/// the caller's phone number. Cards let other modules (for example the Contact Center) enrich the
/// incoming-call experience with related records and shortcuts without the Telephony module taking a
/// dependency on them.
/// </summary>
public sealed class IncomingCallCard
{
    /// <summary>
    /// Gets or sets the stable identifier of the card, unique within a single incoming-call context.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the primary title of the card, such as the matched contact's display name.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the optional secondary text of the card, such as the matched phone number.
    /// </summary>
    public string Subtitle { get; set; }

    /// <summary>
    /// Gets or sets the optional descriptive text shown under the title and subtitle.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the optional CSS class of the icon shown for the card.
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// Gets or sets the optional URL the agent can open as a shortcut, such as the matched contact content item.
    /// When set, the modal renders an answer-and-open action that answers the call and opens this URL.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="Url"/> opens in a new browser tab. Defaults to <see langword="true"/>.
    /// </summary>
    public bool OpenInNewTab { get; set; } = true;

    /// <summary>
    /// Gets or sets the contributing source name, used for grouping and diagnostics.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the sort priority of the card. Cards with a lower value are shown first.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the badges shown on the card, such as a queue name or a tag.
    /// </summary>
    public IList<string> Badges { get; set; } = [];

    /// <summary>
    /// Gets or sets additional links shown for the card, such as related records or actions.
    /// </summary>
    public IList<IncomingCallCardLink> Links { get; set; } = [];
}
