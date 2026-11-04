using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// What a request would count and what would happen to it, before it is submitted.
/// </summary>
public sealed class TimeOffPreview
{
    /// <summary>
    /// Gets or sets the time-off type.
    /// </summary>
    public TimeOffType Type { get; set; }

    /// <summary>
    /// Gets or sets the policy the time off would be taken from.
    /// </summary>
    public TimeOffPolicy Policy { get; set; }

    /// <summary>
    /// Gets or sets the team the request would belong to.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the time zone of the days.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets what each day counts.
    /// </summary>
    public IReadOnlyList<TimeOffRequestDay> Days { get; set; } = [];

    /// <summary>
    /// Gets what the request counts in all.
    /// </summary>
    public decimal Amount => Days.Sum(day => day.Amount);

    /// <summary>
    /// Gets or sets the balance on the first day, before the request.
    /// </summary>
    public TimeOffBalance Balance { get; set; }

    /// <summary>
    /// Gets or sets the published shifts the time off overlaps.
    /// </summary>
    public int OverlappingShifts { get; set; }

    /// <summary>
    /// Gets or sets the dates on which as many people as an allotment allows are already off.
    /// </summary>
    public IReadOnlyList<DateOnly> FullDates { get; set; } = [];

    /// <summary>
    /// Gets or sets what submitting would do: approve it, add it to the waitlist or send it to a manager.
    /// </summary>
    public TimeOffRequestStatus Outcome { get; set; } = TimeOffRequestStatus.Pending;

    /// <summary>
    /// Gets or sets why the request can't be submitted.
    /// </summary>
    public IReadOnlyList<string> Problems { get; set; } = [];
}
