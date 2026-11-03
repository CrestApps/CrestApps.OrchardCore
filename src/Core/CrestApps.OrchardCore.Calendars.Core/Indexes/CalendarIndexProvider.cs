using CrestApps.OrchardCore.Calendars.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Calendars.Core.Indexes;

/// <summary>
/// Maps <see cref="Calendar"/> documents to <see cref="CalendarIndex"/> rows.
/// </summary>
public sealed class CalendarIndexProvider : IndexProvider<Calendar>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarIndexProvider"/> class.
    /// </summary>
    public CalendarIndexProvider()
    {
        CollectionName = CalendarsConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<Calendar> context)
    {
        context.For<CalendarIndex>()
            .Map(calendar => new CalendarIndex
            {
                ItemId = calendar.ItemId,
                Name = Truncate(calendar.Name, 255),
                Type = calendar.Type,
                OwnerKind = calendar.OwnerKind,
                OwnerId = calendar.OwnerId,
                Visibility = calendar.Visibility,
                Source = calendar.Source,
                SourceId = calendar.SourceId,
                FeedToken = calendar.FeedToken,
                Enabled = calendar.Enabled,
            });

        context.For<CalendarShareIndex>()
            .Map(calendar => calendar.Shares
                .Where(share => !string.IsNullOrEmpty(share.PrincipalId) && share.Access > CalendarAccessLevel.None)
                .Select(share => new CalendarShareIndex
                {
                    CalendarId = calendar.ItemId,
                    PrincipalKind = share.PrincipalKind,
                    PrincipalId = share.PrincipalId,
                    Access = share.Access,
                }));
    }

    internal static string Truncate(string value, int length)
    {
        return value is not null && value.Length > length ? value.Substring(0, length) : value;
    }
}
