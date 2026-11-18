namespace CrestApps.OrchardCore.Timeline.Models;

/// <summary>
/// A page of a record's timeline.
/// </summary>
/// <param name="Entries">The entries, newest first.</param>
/// <param name="Total">How many entries match in all.</param>
public sealed record TimelinePage(IReadOnlyList<TimelineEntry> Entries, int Total);
