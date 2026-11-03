using CrestApps.OrchardCore.Calendars.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Calendars.Core.Indexes;

/// <summary>
/// Maps <see cref="CalendarCategory"/> to <see cref="CalendarCategoryIndex"/>.
/// </summary>
public sealed class CalendarCategoryIndexProvider : IndexProvider<CalendarCategory>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarCategoryIndexProvider"/> class.
    /// </summary>
    public CalendarCategoryIndexProvider()
    {
        CollectionName = CalendarsConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<CalendarCategory> context)
    {
        context.For<CalendarCategoryIndex>()
            .Map(category => new CalendarCategoryIndex
            {
                ItemId = category.ItemId,
                OwnerId = category.OwnerId,
                Name = category.Name?.Length > 100 ? category.Name[..100] : category.Name,
            });
    }
}
