using CrestApps.Core.Services;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Migrations;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Covers the service desk's collection: its records are read and written there, and a site whose records were in the
/// default collection has them moved, once.
/// </summary>
public sealed class TicketServiceDeskCollectionTests
{
    // Mike's sites kept the desks, statuses, priorities, views and boards as one dictionary per kind, and the protected
    // information, outbox, backlog and markers as documents, all in the default collection.
    [Fact]
    public async Task Move_PutsEveryRecordInTheServiceDeskCollection_AndRunningAgainChangesNothing()
    {
        var harness = await CreateAsync();

        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();

            await session.SaveAsync(Dictionary(new TicketDesk { ItemId = "support", Name = "Support" }, new TicketDesk { ItemId = "hr", Name = "HR (old copy)" }));
            await session.SaveAsync(Dictionary(new TicketStatus { ItemId = "open", Name = "Open" }, new TicketStatus { ItemId = "solved", Name = "Solved" }));
            await session.SaveAsync(Dictionary(new TicketPriority { ItemId = "normal", Name = "Normal" }));
            await session.SaveAsync(Dictionary(new TicketView { ItemId = "mine", Name = "My tickets" }));
            await session.SaveAsync(Dictionary(new TicketBoard { ItemId = "board", Name = "Everyone's board" }));
            await session.SaveAsync(new TicketProtectedItemDocument { ItemId = "p1", TicketId = "t1", Label = "VPN" });
            await session.SaveAsync(new TicketProtectedItemDocument { ItemId = "p2", TicketId = "t1", Label = "Wi-Fi" });
            await session.SaveAsync(new TicketEmailOutboxItem { TicketId = "t1", To = "pat@example.com", Status = TicketEmailOutboxStatus.Pending });
            await session.SaveAsync(new TicketBacklogDay { Day = "2026-11-02", DeskId = "support", TakenUtc = CalendarTestHarness.Now });
            await session.SaveAsync(new TicketTimeElapsedMark { StartKey = "start", TicketId = "t1", Moment = CalendarTestHarness.Now, FiredUtc = CalendarTestHarness.Now });

            // A desk the collection already has keeps its newer copy.
            await services.GetRequiredService<ICatalog<TicketDesk>>().CreateAsync(new TicketDesk { ItemId = "hr", Name = "HR" });

            return true;
        });

        Assert.Equal(11, await MoveAsync(harness));

        await harness.RunAsync(async services =>
        {
            var desks = await services.GetRequiredService<ICatalog<TicketDesk>>().GetAllAsync(TestContext.Current.CancellationToken);

            Assert.Equal(["HR", "Support"], desks.Select(desk => desk.Name).Order());
            Assert.Equal(2, (await services.GetRequiredService<ICatalog<TicketStatus>>().GetAllAsync(TestContext.Current.CancellationToken)).Count);
            Assert.Equal("Normal", (await services.GetRequiredService<ICatalog<TicketPriority>>().FindByIdAsync("normal", TestContext.Current.CancellationToken)).Name);
            Assert.Equal("My tickets", (await services.GetRequiredService<ICatalog<TicketView>>().FindByIdAsync("mine", TestContext.Current.CancellationToken)).Name);
            Assert.Equal("Everyone's board", (await services.GetRequiredService<ICatalog<TicketBoard>>().FindByIdAsync("board", TestContext.Current.CancellationToken)).Name);

            var session = services.GetRequiredService<ISession>();

            // In the order they were stored, so the protected items keep theirs.
            Assert.Equal(["p1", "p2"], (await session.Query<TicketProtectedItemDocument, TicketProtectedItemIndex>(index => index.TicketId == "t1", TicketsConstants.CollectionName).OrderBy(index => index.Id).ListAsync(TestContext.Current.CancellationToken)).Select(item => item.ItemId));
            Assert.Equal(1, await session.QueryIndex<TicketEmailOutboxIndex>(index => index.TicketId == "t1", TicketsConstants.CollectionName).CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(1, await session.QueryIndex<TicketBacklogIndex>(index => index.Day == "2026-11-02", TicketsConstants.CollectionName).CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(1, await session.QueryIndex<TicketTimeElapsedMarkIndex>(index => index.StartKey == "start", TicketsConstants.CollectionName).CountAsync(TestContext.Current.CancellationToken));

            // Nothing is left in the default collection.
            Assert.Equal(0, await session.Query<DictionaryDocument<TicketDesk>>().CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, await session.Query<DictionaryDocument<TicketBoard>>().CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, await session.Query<TicketProtectedItemDocument>().CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, await session.Query<TicketEmailOutboxItem>().CountAsync(TestContext.Current.CancellationToken));

            return true;
        }, commit: false);

        Assert.Equal(0, await MoveAsync(harness));
        Assert.Equal(2, await harness.RunAsync(async services => (await services.GetRequiredService<ICatalog<TicketDesk>>().GetAllAsync(TestContext.Current.CancellationToken)).Count, commit: false));
        Assert.Equal(2, await harness.RunAsync(services => services.GetRequiredService<ISession>().QueryIndex<TicketProtectedItemIndex>(TicketsConstants.CollectionName).CountAsync(TestContext.Current.CancellationToken), commit: false));
    }

    // A fresh site has nothing to move.
    [Fact]
    public async Task Move_OnAFreshSite_MovesNothing()
    {
        var harness = await CreateAsync();

        Assert.Equal(0, await MoveAsync(harness));
    }

    [Fact]
    public async Task Catalog_WritesEachRecordAsItsOwnDocumentInTheCollection()
    {
        var harness = await CreateAsync();

        await harness.RunAsync(async services =>
        {
            var boards = services.GetRequiredService<ICatalog<TicketBoard>>();

            await boards.CreateAsync(new TicketBoard { ItemId = "b1", Name = "Support" });
            await boards.CreateAsync(new TicketBoard { ItemId = "b2", Name = "IT" });

            return true;
        });

        await harness.RunAsync(async services =>
        {
            var boards = services.GetRequiredService<ICatalog<TicketBoard>>();
            var board = await boards.FindByIdAsync("b1", TestContext.Current.CancellationToken);

            board.Name = "Customer support";
            await boards.UpdateAsync(board, TestContext.Current.CancellationToken);
            await boards.DeleteAsync(await boards.FindByIdAsync("b2", TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

            return true;
        });

        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            var rows = await session.QueryIndex<TicketBoardIndex>(TicketsConstants.CollectionName).ListAsync(TestContext.Current.CancellationToken);

            Assert.Equal("Customer support", Assert.Single(rows).Name);
            Assert.Equal("Customer support", Assert.Single(await services.GetRequiredService<ICatalog<TicketBoard>>().GetAllAsync(TestContext.Current.CancellationToken)).Name);
            Assert.Equal(0, await session.Query<TicketBoard>().CountAsync(TestContext.Current.CancellationToken));

            return true;
        }, commit: false);
    }

    private static Task<int> MoveAsync(CalendarTestHarness harness)
        => harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            var store = services.GetRequiredService<IStore>();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);

            return await TicketServiceDeskMove.MoveAsync(new SchemaBuilder(store.Configuration, transaction), store, session);
        });

    private static DictionaryDocument<T> Dictionary<T>(params T[] records)
        where T : CrestApps.Core.Models.CatalogItem
    {
        var document = new DictionaryDocument<T>();

        foreach (var record in records)
        {
            document.Records[record.ItemId] = record;
        }

        return document;
    }

    private static Task<CalendarTestHarness> CreateAsync()
        => CalendarTestHarness.CreateAsync(
            services => services.AddTicketsCore(),
            async schema =>
            {
                await TicketIndexSchema.CreateAsync(schema);
                await TicketIndexSchema.CreateServiceDeskAsync(schema);
            },
            [
                new TicketSettingsRecordIndexProvider<TicketDesk, TicketDeskIndex>(),
                new TicketSettingsRecordIndexProvider<TicketStatus, TicketStatusIndex>(),
                new TicketSettingsRecordIndexProvider<TicketPriority, TicketPriorityIndex>(),
                new TicketSettingsRecordIndexProvider<TicketView, TicketViewIndex>(),
                new TicketSettingsRecordIndexProvider<TicketBoard, TicketBoardIndex>(),
                new TicketProtectedItemIndexProvider(),
                new TicketEmailOutboxIndexProvider(),
                new TicketBacklogIndexProvider(),
                new TicketTimeElapsedMarkIndexProvider(),
            ],
            [TicketsConstants.CollectionName]);
}
