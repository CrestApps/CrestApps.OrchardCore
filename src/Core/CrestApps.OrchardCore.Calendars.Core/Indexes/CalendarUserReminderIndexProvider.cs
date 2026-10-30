using CrestApps.OrchardCore.Calendars.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Calendars.Core.Indexes;

/// <summary>
/// Maps <see cref="CalendarUserReminder"/> to <see cref="CalendarUserReminderIndex"/>.
/// </summary>
public sealed class CalendarUserReminderIndexProvider : IndexProvider<CalendarUserReminder>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarUserReminderIndexProvider"/> class.
    /// </summary>
    public CalendarUserReminderIndexProvider()
    {
        CollectionName = CalendarsConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<CalendarUserReminder> context)
    {
        context.For<CalendarUserReminderIndex>()
            .Map(reminder => new CalendarUserReminderIndex
            {
                ItemId = reminder.ItemId,
                UserId = reminder.UserId,
                Dismissed = reminder.DismissedUtc.HasValue,
                CreatedUtc = reminder.CreatedUtc,
            });
    }
}
