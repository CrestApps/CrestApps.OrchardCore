namespace CrestApps.OrchardCore.Timeline.Models;

/// <summary>
/// The result of adding or changing a timeline entry.
/// </summary>
public sealed class TimelineResult
{
    /// <summary>
    /// Gets the entry.
    /// </summary>
    public TimelineEntry Entry { get; init; }

    /// <summary>
    /// Gets why it was refused; empty when it succeeded.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Gets whether it succeeded.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;
}
