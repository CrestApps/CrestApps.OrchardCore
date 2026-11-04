using CrestApps.OrchardCore.Dispatch.Integrations;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.DependencyInjection;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Dispatch;

/// <summary>
/// Booking slots of an appointment type that makes work orders are offered only while a technician can take the job:
/// on shift and free, less the bookings already made into the slot that no one has been given yet.
/// </summary>
public sealed class DispatchCapacitySlotTests
{
    private const string AppointmentTypeId = "boiler-service-booking";

    [Fact]
    public async Task ASlot_IsOffered_OnlyWhileATechnicianIsFree_ForIt()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);
        await kit.UpdateTypeAsync(type => type.AppointmentTypeIds = [AppointmentTypeId]);
        kit.Now = DispatchKit.Monday(7);

        // 10:00 to 12:00 is on Ana's shift; 17:00 to 19:00 is after it.
        var slots = await FilterAsync(kit, null, Slot(10, 12), Slot(17, 19));

        Assert.Equal([DispatchKit.Monday(10)], slots.Select(slot => slot.StartUtc));
        Assert.Equal(1, slots[0].Remaining);

        // A booking into 10:00 to 12:00 that no one has been given yet takes Ana's place.
        await kit.AddJobAsync("Booked boiler service", DispatchKit.Depot, order =>
        {
            order.Source = WorkOrderSource.Appointment;
            order.SourceId = "appointment-1";
            order.EarliestUtc = DispatchKit.Monday(10);
            order.LatestUtc = DispatchKit.Monday(12);
        });

        Assert.Empty(await FilterAsync(kit, null, Slot(10, 12)));

        // Moving that same booking, its own place doesn't count.
        Assert.Single(await FilterAsync(kit, "appointment-1", Slot(10, 12)));
    }

    [Fact]
    public async Task AnAppointmentType_NoWorkOrderTypeMakes_KeepsItsSlots()
    {
        await using var kit = await DispatchKit.CreateAsync();

        kit.Now = DispatchKit.Monday(7);

        // No technician is on shift, but no work order type lists the appointment type.
        Assert.Single(await FilterAsync(kit, null, Slot(10, 12)));
    }

    private static AppointmentSlot Slot(int fromHour, int toHour)
        => new() { StartUtc = DispatchKit.Monday(fromHour), EndUtc = DispatchKit.Monday(toHour), Remaining = 1 };

    private static Task<IReadOnlyList<AppointmentSlot>> FilterAsync(DispatchKit kit, string excludeAppointmentId, params AppointmentSlot[] slots)
        => kit.Harness.RunAsync(services =>
        {
            var filter = new DispatchCapacitySlotFilter(
                services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>(),
                services.GetRequiredService<IDispatchCandidateFinder>(),
                services.GetRequiredService<ISession>());

            return filter.FilterAsync(new AppointmentSlotFilterContext { AppointmentType = new AppointmentType { ItemId = AppointmentTypeId }, ExcludeAppointmentId = excludeAppointmentId }, slots);
        }, commit: false);
}
