using CrestApps.OrchardCore.Timeline.Models;

namespace CrestApps.OrchardCore.Timeline.ViewModels;

/// <summary>
/// A button of the timeline card's composer: the kind of entry it adds.
/// </summary>
public sealed class TimelineComposerButton
{
    /// <summary>
    /// Gets or sets the entry kind.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets who may read the entry.
    /// </summary>
    public TimelineVisibility Visibility { get; set; }

    /// <summary>
    /// Gets or sets the button's text.
    /// </summary>
    public string Text { get; set; }
}
