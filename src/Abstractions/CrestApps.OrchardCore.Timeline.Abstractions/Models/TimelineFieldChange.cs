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

    /// <summary>
    /// Gets or sets a stable name of what changed, such as <c>Status</c>, the same in every language, for code that
    /// reacts to the change (workflows, webhooks, reports). <see cref="Field"/> is what people read.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the value before the change as code knows it, such as a status id; <see cref="From"/> is what
    /// people read.
    /// </summary>
    public string FromValue { get; set; }

    /// <summary>
    /// Gets or sets the value after the change as code knows it, such as a status id; <see cref="To"/> is what people
    /// read.
    /// </summary>
    public string ToValue { get; set; }
}
