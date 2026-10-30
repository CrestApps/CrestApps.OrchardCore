using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Scheduling.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Scheduling.Core.Indexes;

/// <summary>
/// Maps <see cref="AppointmentType"/> documents to <see cref="AppointmentTypeIndex"/> rows.
/// </summary>
public sealed class AppointmentTypeIndexProvider : IndexProvider<AppointmentType>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentTypeIndexProvider"/> class.
    /// </summary>
    public AppointmentTypeIndexProvider()
    {
        CollectionName = SchedulingConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<AppointmentType> context)
    {
        context.For<AppointmentTypeIndex>()
            .Map(type => new AppointmentTypeIndex
            {
                ItemId = type.ItemId,
                Name = type.Name,
                Slug = type.Slug,
                Enabled = type.Enabled,
            });
    }
}
