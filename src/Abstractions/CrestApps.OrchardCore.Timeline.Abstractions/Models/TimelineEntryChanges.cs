namespace CrestApps.OrchardCore.Timeline.Models;

/// <summary>
/// What may change on an entry after it was written: the properties a channel keeps (delivery status, recipients), the
/// channel and the external id it was sent under. What the entry says, where it is, who wrote it and its kind can't
/// be reached from here; a body can only be redacted.
/// </summary>
public sealed class TimelineEntryChanges
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineEntryChanges"/> class with an entry's current values.
    /// </summary>
    /// <param name="entry">The entry.</param>
    public TimelineEntryChanges(TimelineEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Properties = entry.Properties is null ? [] : new Dictionary<string, object>(entry.Properties, StringComparer.Ordinal);
        Channel = entry.Channel;
        ExternalId = entry.ExternalId;
    }

    /// <summary>
    /// Gets the entry's properties, to change.
    /// </summary>
    public IDictionary<string, object> Properties { get; }

    /// <summary>
    /// Gets or sets the channel.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the external id.
    /// </summary>
    public string ExternalId { get; set; }
}
