using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Models;

/// <summary>
/// One worker's adherence over one day in their time zone: the totals and the segments of scheduled time.
/// </summary>
public sealed class AdherenceDay : CatalogItem
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the worker's team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the worker's agent profile.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the day.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets the time zone the day is in.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the scheduled seconds, exceptions included.
    /// </summary>
    public double ScheduledSeconds { get; set; }

    /// <summary>
    /// Gets or sets the seconds in adherence.
    /// </summary>
    public double InAdherenceSeconds { get; set; }

    /// <summary>
    /// Gets or sets the scheduled seconds an exception covers.
    /// </summary>
    public double ExceptionSeconds { get; set; }

    /// <summary>
    /// Gets or sets the scheduled seconds of work.
    /// </summary>
    public double ScheduledWorkSeconds { get; set; }

    /// <summary>
    /// Gets or sets the seconds in a working state.
    /// </summary>
    public double ActualWorkSeconds { get; set; }

    /// <summary>
    /// Gets or sets the segments of scheduled time.
    /// </summary>
    public List<AdherenceSegment> Segments { get; set; } = [];

    /// <summary>
    /// Gets or sets when the day was computed.
    /// </summary>
    public DateTime ComputedUtc { get; set; }

    /// <summary>
    /// Gets the share of scheduled time, less exceptions, in adherence; <see langword="null"/> without scheduled time.
    /// </summary>
    public double? Adherence => ScheduledSeconds - ExceptionSeconds > 0 ? InAdherenceSeconds / (ScheduledSeconds - ExceptionSeconds) : null;

    /// <summary>
    /// Gets the worked time as a share of the scheduled work; <see langword="null"/> without scheduled work.
    /// </summary>
    public double? Conformance => ScheduledWorkSeconds > 0 ? ActualWorkSeconds / ScheduledWorkSeconds : null;
}
