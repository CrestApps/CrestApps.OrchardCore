using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Scheduling.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Scheduling.Core.Indexes;

/// <summary>
/// Maps <see cref="Appointment"/> documents to <see cref="AppointmentIndex"/> and <see cref="AppointmentResourceIndex"/> rows.
/// </summary>
public sealed class AppointmentIndexProvider : IndexProvider<Appointment>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentIndexProvider"/> class.
    /// </summary>
    public AppointmentIndexProvider()
    {
        CollectionName = SchedulingConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<Appointment> context)
    {
        context.For<AppointmentIndex>()
            .Map(appointment => new AppointmentIndex
            {
                ItemId = appointment.ItemId,
                AppointmentTypeId = appointment.AppointmentTypeId,
                StartUtc = appointment.StartUtc,
                EndUtc = appointment.EndUtc,
                Status = appointment.Status,
                HoldExpiresUtc = appointment.HoldExpiresUtc,
                CustomerName = appointment.CustomerName?.Length > 255 ? appointment.CustomerName.Substring(0, 255) : appointment.CustomerName,
                CustomerEmail = appointment.CustomerEmail?.Trim().ToLowerInvariant(),
                CustomerPhone = appointment.CustomerPhone,
                UserId = appointment.UserId,
                CreatedUtc = appointment.CreatedUtc,
            });

        context.For<AppointmentResourceIndex>()
            .Map(appointment => appointment.ResourceIds.Select(resourceId => new AppointmentResourceIndex
            {
                AppointmentId = appointment.ItemId,
                AppointmentTypeId = appointment.AppointmentTypeId,
                ResourceId = resourceId,
                StartUtc = appointment.StartUtc,
                EndUtc = appointment.EndUtc,
                Status = appointment.Status,
            }));
    }
}
