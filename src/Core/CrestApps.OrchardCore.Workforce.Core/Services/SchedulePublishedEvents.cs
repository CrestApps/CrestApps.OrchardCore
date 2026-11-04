using System.Globalization;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Raises <see cref="WorkforceEvents.SchedulePublished"/> when a schedule is published.
/// </summary>
internal sealed class SchedulePublishedEvents : ISchedulePublishHandler
{
    private readonly IWorkforceEvents _events;

    public SchedulePublishedEvents(IWorkforceEvents events)
    {
        _events = events;
    }

    public Task PublishedAsync(SchedulePublishedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var period = context.Period;

        return _events.RaiseAsync(WorkforceEvents.SchedulePublished, new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["PeriodId"] = period.ItemId,
            ["TeamId"] = period.TeamId,
            ["StartDate"] = period.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["EndDate"] = period.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["Actor"] = context.Actor,
        }, period.ItemId, cancellationToken);
    }
}
