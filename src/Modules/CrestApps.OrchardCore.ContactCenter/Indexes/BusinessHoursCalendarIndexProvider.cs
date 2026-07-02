using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Maps <see cref="BusinessHoursCalendar"/> documents to the <see cref="BusinessHoursCalendarIndex"/>.
/// </summary>
public sealed class BusinessHoursCalendarIndexProvider : IndexProvider<BusinessHoursCalendar>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursCalendarIndexProvider"/> class.
    /// </summary>
    public BusinessHoursCalendarIndexProvider()
    {
        CollectionName = ContactCenterConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<BusinessHoursCalendar> context)
    {
        context
            .For<BusinessHoursCalendarIndex>()
            .Map(calendar => new BusinessHoursCalendarIndex
            {
                ItemId = calendar.ItemId,
                Name = calendar.Name,
                Enabled = calendar.Enabled,
            });
    }
}
