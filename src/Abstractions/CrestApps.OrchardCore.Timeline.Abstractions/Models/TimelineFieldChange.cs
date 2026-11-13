namespace CrestApps.OrchardCore.Timeline.Models;

/// <summary>
/// A change an entry records: a field of the record, its value before and after, as people read them.
/// </summary>
public sealed class TimelineFieldChange
{
    /// <summary>
    /// Gets or sets the field's name, such as "Status".
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// Gets or sets the value before, or <see langword="null"/> when it was empty.
    /// </summary>
    public string From { get; set; }

    /// <summary>
    /// Gets or sets the value after, or <see langword="null"/> when it is empty.
    /// </summary>
    public string To { get; set; }
}
