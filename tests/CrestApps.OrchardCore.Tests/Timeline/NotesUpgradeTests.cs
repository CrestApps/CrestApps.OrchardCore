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
/// kinds, and the records no longer carry them: a batch at a time, without cutting a long note or announcing anything,
/// keeping on a record the notes that couldn't move, and adding nothing when it runs again after a run that stopped.
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
                await new OrderMigrations(steps: null) { SchemaBuilder = schemaBuilder }.CreateAsync();
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

    [Fact]
    public async Task WorkOrderNotes_MoveInBatches_KeepALongNote_AndAnnounceNothing()
    {
        // Arrange: five work orders, one with a note longer than a new note may be, read two at a time.
        var heard = new List<string>();
        await using var harness = await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddWorkOrdersWithServiceLevels();
                services.AddSingleton<ITimelineEntryHandler>(new Listener(heard));
            },
            WorkOrderTestStores.CreateSchemaAsync,
            [.. WorkOrderTestStores.IndexProviders],
            WorkOrderTestStores.Collections);
        var longNote = new string('n', TimelineConstants.MaxBodyLength + 1_000);

        for (var index = 0; index < 5; index++)
        {
            await SaveWorkOrderAsync(harness, $"wo{index}", index == 3 ? longNote : $"Note {index}");
        }

        // Act
        var first = await harness.RunAsync(services => Upgrade(services, batchSize: 2).RunAsync());
        var second = await harness.RunAsync(services => Upgrade(services, batchSize: 2).RunAsync());

        // Assert
        Assert.Equal(5, first);
        Assert.Equal(0, second);
        Assert.Empty(heard);
        var page = await harness.RunAsync(services => services.GetRequiredService<ITimelineService>().ListAsync(new TimelineQuery { Subject = new RecordRef(WorkOrdersConstants.RecordKind, "wo3"), IncludeInternal = true }), commit: false);
        Assert.Equal(longNote, Assert.Single(page.Entries).Body);
    }

    [Fact]
    public async Task AWorkOrderWhoseNoteIsRefused_KeepsItsNotes()
    {
        // Arrange: a record id longer than the timeline's index holds, so its note can't be stored.
        await using var harness = await CalendarTestHarness.CreateAsync(
            services => services.AddWorkOrdersWithServiceLevels(),
            WorkOrderTestStores.CreateSchemaAsync,
            [.. WorkOrderTestStores.IndexProviders],
            WorkOrderTestStores.Collections);
        var id = new string('w', TimelineConstants.MaxIndexedLength + 1);
        await SaveWorkOrderAsync(harness, id, "Keep me.");

        // Act
        var moved = await harness.RunAsync(services => services.GetRequiredService<WorkOrderNotesUpgrade>().RunAsync());

        // Assert
        Assert.Equal(0, moved);
        var stored = await harness.RunAsync(services => services.GetRequiredService<ISession>().Query<WorkOrder>(collection: WorkOrdersConstants.CollectionName).FirstOrDefaultAsync(), commit: false);
        Assert.Equal("Keep me.", Assert.Single(stored.Notes).Text);
    }

    [Fact]
    public async Task ARunAfterOneThatStoppedBeforeClearingTheNotes_AddsNothingTwice()
    {
        // Arrange: the notes were copied but the work order still carries them, as if the first run stopped there.
        await using var harness = await CalendarTestHarness.CreateAsync(
            services => services.AddWorkOrdersWithServiceLevels(),
            WorkOrderTestStores.CreateSchemaAsync,
            [.. WorkOrderTestStores.IndexProviders],
            WorkOrderTestStores.Collections);
        await SaveWorkOrderAsync(harness, "wo1", "Once only.");
        await harness.RunAsync(services => services.GetRequiredService<WorkOrderNotesUpgrade>().RunAsync());
        await SaveWorkOrderAsync(harness, "wo1-again", "Once only.", itemId: "wo1");

        // Act
        await harness.RunAsync(services => services.GetRequiredService<WorkOrderNotesUpgrade>().RunAsync());

        // Assert
        var page = await harness.RunAsync(services => services.GetRequiredService<ITimelineService>().ListAsync(new TimelineQuery { Subject = new RecordRef(WorkOrdersConstants.RecordKind, "wo1"), IncludeInternal = true }), commit: false);
        Assert.Equal(1, page.Total);
    }

    private static WorkOrderNotesUpgrade Upgrade(IServiceProvider services, int batchSize)
    {
        var upgrade = services.GetRequiredService<WorkOrderNotesUpgrade>();
        upgrade.BatchSize = batchSize;

        return upgrade;
    }

    private static Task SaveWorkOrderAsync(CalendarTestHarness harness, string number, string note, string itemId = null)
        => harness.RunAsync(services => services.GetRequiredService<ISession>().SaveAsync(new WorkOrder
        {
            ItemId = itemId ?? number,
            Number = "WO-" + number,
            Summary = "Boiler service",
            Notes = [new WorkOrderNote { Utc = CalendarTestHarness.Now.AddDays(-2), By = "tech", Text = note }],
        }, collection: WorkOrdersConstants.CollectionName));

    private sealed class Listener : ITimelineEntryHandler
    {
        private readonly List<string> _heard;

        public Listener(List<string> heard)
        {
            _heard = heard;
        }

        public Task AddingAsync(TimelineEntry entry, IList<string> errors, CancellationToken cancellationToken = default)
        {
            _heard.Add("adding");

            return Task.CompletedTask;
        }

        public Task AddedAsync(TimelineEntry entry, CancellationToken cancellationToken = default)
        {
            _heard.Add("added");

            return Task.CompletedTask;
        }
    }
}
#pragma warning restore CS0618
