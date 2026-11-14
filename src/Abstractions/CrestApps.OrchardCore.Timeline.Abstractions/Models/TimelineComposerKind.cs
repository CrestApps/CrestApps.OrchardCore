using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Timeline.Models;

/// <summary>
/// A kind of entry the timeline card's composer offers, as one button: a note, a comment, or a kind a module adds, such
/// as a reply. The composer shows the button only to a user the record's access provider lets add that kind.
/// </summary>
public sealed class TimelineComposerKind
{
    /// <summary>
    /// Gets or sets the entry kind the button adds.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets who may read the entries it adds.
    /// </summary>
    public TimelineVisibility Visibility { get; set; }

    /// <summary>
    /// Gets or sets the button's text.
    /// </summary>
    public LocalizationSource ButtonText { get; set; }

    /// <summary>
    /// Gets or sets the record kinds the button is offered on, or none for every kind.
    /// </summary>
    public ISet<string> SubjectKinds { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
