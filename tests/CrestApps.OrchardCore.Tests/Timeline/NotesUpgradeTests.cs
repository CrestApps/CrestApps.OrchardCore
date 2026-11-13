using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Migrations;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.WorkOrders;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Core;
using CrestApps.OrchardCore.Timeline.Core.Indexes;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Legacy;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Timeline;

/// <summary>
/// The notes work orders and orders kept on themselves move to their timelines once, with their own times, authors and
/// kinds, and the records no longer carry them.
/// </summary>
#pragma warning disable CS0618 // The tests store notes the old way, to move them.
public sealed class NotesUpgradeTests
{
    [Fact]
    public async Task WorkOrderNotes_MoveToTheTimeline_Once()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync(
            services => services.AddWorkOrdersWithServiceLevels(),
            WorkOrderTestStores.CreateSchemaAsync,
            [.. WorkOrderTestStores.IndexProviders],
            WorkOrderTestStores.Collections);

        var noted = CalendarTestHarness.Now.AddDays(-2);

        await harness.RunAsync(services => services.GetRequiredService<ISession>().SaveAsync(new WorkOrder
        {
            ItemId = "wo1",
            Number = "WO-000001",
            Summary = "Boiler service",
            Notes =
            [
                new WorkOrderNote { Utc = noted, By = "dispatcher", Text = "Created." },
                new WorkOrderNote { Utc = noted.AddHours(1), By = "tech", Text = "Bring the long ladder." },
            ],
        }, collection: WorkOrdersConstants.CollectionName));

        // Act
        var first = await harness.RunAsync(services => services.GetRequiredService<WorkOrderNotesUpgrade>().RunAsync());
        var second = await harness.RunAsync(services => services.GetRequiredService<WorkOrderNotesUpgrade>().RunAsync());

        // Assert
        Assert.Equal(2, first);
        Assert.Equal(0, second);

        var page = await harness.RunAsync(services => services.GetRequiredService<ITimelineService>().ListAsync(new TimelineQuery
        {
            Subject = new RecordRef(WorkOrdersConstants.RecordKind, "wo1"),
            IncludeInternal = true,
        }), commit: false);

        Assert.Equal(["Bring the long ladder.", "Created."], page.Entries.Select(entry => entry.Body));
        Assert.All(page.Entries, entry => Assert.Equal(TimelineVisibility.Internal, entry.Visibility));
        Assert.Equal("tech", page.Entries[0].AuthorName);
        Assert.Equal(noted.AddHours(1), page.Entries[0].CreatedUtc);
    }

    [Fact]
    public async Task OrderNotes_MoveToTheTimeline_WithTheirKinds()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddTimelineCore();
                services.AddScoped<OrderNotesUpgrade>();
            },
            async schemaBuilder =>
            {
                await new OrderMigrations(notesUpgrade: null) { SchemaBuilder = schemaBuilder }.CreateAsync();
                await TimelineIndexSchema.CreateAsync(schemaBuilder);
            },
            [new OrderIndexProvider(), new TimelineEntryIndexProvider()],
            [OrderConstants.CollectionName, TimelineConstants.CollectionName]);

        await harness.RunAsync(services => services.GetRequiredService<ISession>().SaveAsync(new Order
        {
            ItemId = "order1",
            OrderNumber = "261102-AAAAAA",
            CreatedUtc = CalendarTestHarness.Now.AddDays(-1),
            Notes =
            [
                new OrderNote { Id = "n1", Text = "Refunded 10.00.", Kind = OrderNoteKind.Event, CreatedUtc = CalendarTestHarness.Now.AddHours(-5) },
                new OrderNote { Id = "n2", Text = "Customer called.", Kind = OrderNoteKind.Note, AuthorName = "admin", CreatedUtc = CalendarTestHarness.Now.AddHours(-4) },
            ],
        }, collection: OrderConstants.CollectionName));

        // Act
        var moved = await harness.RunAsync(services => services.GetRequiredService<OrderNotesUpgrade>().RunAsync());

        // Assert
        Assert.Equal(2, moved);

        var (page, order) = await harness.RunAsync(async services => (
            await services.GetRequiredService<ITimelineService>().ListAsync(new TimelineQuery
            {
                Subject = new RecordRef(OrderConstants.RecordKind, "order1"),
                IncludeInternal = true,
            }),
            await services.GetRequiredService<ISession>().Query<Order, OrderIndex>(index => index.ItemId == "order1", collection: OrderConstants.CollectionName).FirstOrDefaultAsync()), commit: false);

        Assert.Equal([TimelineConstants.Kinds.Note, TimelineConstants.Kinds.Event], page.Entries.Select(entry => entry.Kind));
        Assert.Equal("admin", page.Entries[0].AuthorName);
        Assert.Empty(order.Notes);
    }
}
#pragma warning restore CS0618
