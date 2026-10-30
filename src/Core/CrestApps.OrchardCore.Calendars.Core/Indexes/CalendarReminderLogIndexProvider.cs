using CrestApps.OrchardCore.Calendars.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Calendars.Core.Indexes;

/// <summary>
/// Maps <see cref="CalendarReminderLog"/> documents to <see cref="CalendarReminderLogIndex"/> rows.
/// </summary>
public sealed class CalendarReminderLogIndexProvider : IndexProvider<CalendarReminderLog>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarReminderLogIndexProvider"/> class.
    /// </summary>
    public CalendarReminderLogIndexProvider()
    {
        CollectionName = CalendarsConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<CalendarReminderLog> context)
    {
        context.For<CalendarReminderLogIndex>()
            .Map(log => new CalendarReminderLogIndex
            {
                ItemId = log.ItemId,
                Key = log.Key,
                RaisedUtc = log.RaisedUtc,
            });
    }
}
