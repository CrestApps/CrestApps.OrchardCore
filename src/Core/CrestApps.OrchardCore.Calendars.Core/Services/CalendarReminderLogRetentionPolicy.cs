using System.Linq.Expressions;
using CrestApps.OrchardCore.Calendars.Core.Indexes;
using CrestApps.OrchardCore.Calendars.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Retention;
using YesSql;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Removes the records of reminders raised more than a week ago, which can never be needed again: a reminder is only
/// looked up to avoid raising it twice while its moment is near.
/// </summary>
public sealed class CalendarReminderLogRetentionPolicy : DocumentRetentionPolicyBase<CalendarReminderLog, CalendarReminderLogIndex>
{
    /// <summary>
    /// How long a reminder record is kept, in days.
    /// </summary>
    public const double RetentionDays = 7;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarReminderLogRetentionPolicy"/> class.
    /// </summary>
    /// <param name="session">The tenant session.</param>
    public CalendarReminderLogRetentionPolicy(ISession session)
        : base(session, CalendarsConstants.CollectionName)
    {
    }

    /// <inheritdoc/>
    public override string Name => "CalendarReminderLog";

    /// <inheritdoc/>
    protected override double GetRetentionDays() => RetentionDays;

    /// <inheritdoc/>
    protected override Expression<Func<CalendarReminderLogIndex, bool>> BuildExpiredPredicate(DateTime cutoffUtc)
        => index => index.RaisedUtc < cutoffUtc;
}
