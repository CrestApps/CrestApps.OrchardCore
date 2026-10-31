namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What Workforce keeps on a published Shift event, in the event's properties.
/// </summary>
public sealed class ShiftData
{
    /// <summary>
    /// Gets or sets the worker who works the shift; empty for an open shift.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the team the shift is for.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the schedule period that published it.
    /// </summary>
    public string ScheduleId { get; set; }

    /// <summary>
    /// Gets or sets the template it was made from.
    /// </summary>
    public string ShiftTemplateId { get; set; }

    /// <summary>
    /// Gets or sets a key that stays the same when the schedule is published again.
    /// </summary>
    public string ShiftKey { get; set; }

    /// <summary>
    /// Gets or sets the schedule version that published it.
    /// </summary>
    public int PublishedVersion { get; set; }

    /// <summary>
    /// Gets or sets the parts of the shift spent on activities other than plain work.
    /// </summary>
    public IList<ShiftSegment> Segments { get; set; } = [];
}
