using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Integrations;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Dispatch;

/// <summary>
/// The field service integrations: an Omnichannel disposition's Create work order action makes one work order per
/// activity, with the activity's notes and the number it reached.
/// </summary>
public sealed class FieldServiceIntegrationTests
{
    [Fact]
    public async Task CreateWorkOrder_MakesOneWorkOrderPerActivity_WithItsNotesAndNumber()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var action = new SubjectAction { Source = WorkOrderSubjectActions.CreateWorkOrder };

        action.Put(new CreateWorkOrderActionMetadata { TypeId = kit.Type.ItemId, Priority = WorkOrderPriority.High });

        var activity = new OmnichannelActivity
        {
            ItemId = UniqueId.GenerateId(),
            Channel = "Phone",
            PreferredDestination = "+15550100123",
            Notes = "Boiler makes a banging noise when it starts.\nCustomer home after 3pm.",
            CompletedByUsername = "agent",
        };

        for (var run = 0; run < 2; run++)
        {
            await kit.Harness.RunAsync(services => ActivatorUtilities.CreateInstance<CreateWorkOrderSubjectActionHandler>(services)
                .ExecuteAsync(action, new SubjectActionExecutionContext { Activity = activity }));
        }

        var made = await kit.Harness.RunAsync(async services => (await services.GetRequiredService<ISession>()
            .Query<WorkOrder, WorkOrderIndex>(index => index.SourceId == activity.ItemId, collection: WorkOrdersConstants.CollectionName)
            .ListAsync()).ToList(), commit: false);

        var workOrder = Assert.Single(made);

        Assert.Equal(WorkOrderSource.Activity, workOrder.Source);
        Assert.Equal("Boiler makes a banging noise when it starts.", workOrder.Summary);
        Assert.Equal(activity.Notes, workOrder.Description);
        Assert.Equal(WorkOrderPriority.High, workOrder.Priority);
        Assert.Equal("+15550100123", workOrder.Contact.Phone);
        Assert.Single(await kit.VisitsOfAsync(workOrder.ItemId));
    }

    [Fact]
    public async Task ABooking_MakesAWorkOrderWhenConfirmed_MovesItWhenRescheduled_AndCancelsIt()
    {
        await using var kit = await DispatchKit.CreateAsync();

        await kit.UpdateTypeAsync(type => type.AppointmentTypeIds = ["boiler-booking"]);

        var appointment = new Appointment
        {
            ItemId = UniqueId.GenerateId(),
            AppointmentTypeId = "boiler-booking",
            StartUtc = DispatchKit.Monday(14),
            EndUtc = DispatchKit.Monday(16),
            TimeZoneId = "America/New_York",
            CustomerName = "Pat Customer",
            CustomerPhone = "+15550100123",
            Notes = "Annual service.",
        };
        var bookingType = new AppointmentType { ItemId = "boiler-booking", Name = "Boiler service booking" };

        async Task ChangeAsync(AppointmentChangeKind kind)
            => await kit.Harness.RunAsync(services => ActivatorUtilities.CreateInstance<AppointmentWorkOrderObserver>(services)
                .OnChangedAsync(new AppointmentChange { Kind = kind, Appointment = appointment, AppointmentType = bookingType, Actor = "customer" }));

        async Task<List<WorkOrder>> MadeAsync()
            => await kit.Harness.RunAsync(async services => (await services.GetRequiredService<ISession>()
                .Query<WorkOrder, WorkOrderIndex>(index => index.SourceId == appointment.ItemId, collection: WorkOrdersConstants.CollectionName)
                .ListAsync()).ToList(), commit: false);

        await ChangeAsync(AppointmentChangeKind.Requested);
        Assert.Empty(await MadeAsync());

        await ChangeAsync(AppointmentChangeKind.Confirmed);
        await ChangeAsync(AppointmentChangeKind.Confirmed);

        var made = Assert.Single(await MadeAsync());

        Assert.Equal("Boiler service booking", made.Summary);
        Assert.Equal(WorkOrderSource.Appointment, made.Source);
        Assert.Equal(DispatchKit.Monday(14), made.EarliestUtc);

        // The booked time is when the technician arrives; the job's hour comes after it.
        Assert.Equal(appointment.EndUtc.AddMinutes(60), made.LatestUtc);
        Assert.Equal("Pat Customer", made.Contact.Name);

        appointment.StartUtc = DispatchKit.Monday(10);
        appointment.EndUtc = DispatchKit.Monday(12);
        await ChangeAsync(AppointmentChangeKind.Rescheduled);

        var moved = Assert.Single(await MadeAsync());

        Assert.Equal((DispatchKit.Monday(10), DispatchKit.Monday(13)), (moved.EarliestUtc, moved.LatestUtc));

        await ChangeAsync(AppointmentChangeKind.Cancelled);

        Assert.Equal(WorkOrderStatus.Canceled, Assert.Single(await MadeAsync()).Status);
    }
}
