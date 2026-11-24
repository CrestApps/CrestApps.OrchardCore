using System.Data.Common;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Migrations;
using CrestApps.OrchardCore.Tickets.Models;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.ContentManagement;
using OrchardCore.Data;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The ticket list's query on a real database: the scope filter, the counts, the due sort with tickets that have nothing
/// due last (on a column that is never null, as SQL Server needs), and paging that never repeats or skips a ticket when
/// several share the sorted value. The migration's backfill fills the due sort of rows indexed before it existed.
/// </summary>
public sealed class TicketListSqliteTests
{
    [Fact]
    public async Task DueSort_PutsTicketsWithNothingDueLast_AndTheScopeAndCountApply()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var now = CalendarTestHarness.Now;
        await SaveAsync(harness,
            Ticket("a", "support", "teamA", now.AddHours(2)),
            Ticket("b", "support", "teamA", now.AddHours(1)),
            Ticket("c", "support", "teamA", null),
            Ticket("d", "support", "teamA", now.AddHours(3)),
            Ticket("e", "support", "teamB", now.AddMinutes(10)),
            Ticket("f", "hr", "teamA", now.AddMinutes(5)));

        // Act
        var (tickets, total) = await ListAsync(harness, new TicketScopedQuery
        {
            Query = new TicketQuery { Sort = TicketSort.Due, Take = 10 },
            TeamDeskIds = ["support"],
            TeamIds = ["teamA"],
            UserId = "ana",
        });

        // Assert
        Assert.Equal(4, total);
        Assert.Equal(["b", "a", "d", "c"], tickets.Select(ticket => ticket.ContentItemId));
    }

    [Theory]
    [InlineData(TicketSort.Updated)]
    [InlineData(TicketSort.Created)]
    [InlineData(TicketSort.Priority)]
    [InlineData(TicketSort.Due)]
    public async Task Paging_NeverRepeatsOrSkipsTickets_ThatShareTheSortedValue(TicketSort sort)
    {
        // Arrange
        await using var harness = await CreateAsync();
        await SaveAsync(harness, [.. Enumerable.Range(1, 7).Select(number => Ticket($"t{number}", "support", "teamA", null))]);
        var seen = new List<string>();

        // Act
        for (var skip = 0; skip < 9; skip += 3)
        {
            var (page, total) = await ListAsync(harness, new TicketScopedQuery
            {
                Query = new TicketQuery { Sort = sort, Skip = skip, Take = 3 },
                AllDeskIds = ["support"],
            });

            Assert.Equal(7, total);
            seen.AddRange(page.Select(ticket => ticket.ContentItemId));
        }

        // Assert
        Assert.Equal(7, seen.Count);
        Assert.Equal(7, seen.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task TheBackfill_FillsTheDueSortOfRowsIndexedBeforeIt()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var now = CalendarTestHarness.Now;
        await SaveAsync(harness, Ticket("a", "support", null, now.AddHours(2)), Ticket("b", "support", null, null));
        var services = new ServiceCollection()
            .AddSingleton(harness.Store)
            .AddSingleton<IDbConnectionAccessor>(new StoreConnections(harness.Store))
            .BuildServiceProvider();

        await using (var connection = new StoreConnections(harness.Store).CreateConnection())
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await connection.ExecuteAsync("UPDATE TicketIndex SET DueSortUtc = NULL");
        }

        // Act
        await TicketIndexBackfill.FillDueSortAsync(services);

        // Assert
        var rows = await harness.RunAsync(
            async scope => (await scope.GetRequiredService<ISession>().QueryIndex<TicketIndex>().ListAsync(TestContext.Current.CancellationToken)).ToDictionary(row => row.ContentItemId),
            commit: false);
        Assert.Equal(now.AddHours(2), rows["a"].DueSortUtc);
        Assert.Equal(TicketIndexProvider.NoDueUtc, rows["b"].DueSortUtc);
    }

    private static Task<CalendarTestHarness> CreateAsync()
        => CalendarTestHarness.CreateAsync(null, TicketIndexSchema.CreateAsync, [new TicketIndexProvider()]);

    private static Task SaveAsync(CalendarTestHarness harness, params ContentItem[] tickets)
        => harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();

            foreach (var ticket in tickets)
            {
                await session.SaveAsync(ticket);
            }
        });

    private static Task<(IReadOnlyList<ContentItem> Tickets, int Total)> ListAsync(CalendarTestHarness harness, TicketScopedQuery query)
        => harness.RunAsync(services => new DefaultTicketService(
            null, null, services.GetRequiredService<ISession>(), null, null, null, null, null, null, null, null,
            services.GetRequiredService<IClock>(), services, [], NullLogger<DefaultTicketService>.Instance, null)
            .ListAsync(query, TestContext.Current.CancellationToken), commit: false);

    private static ContentItem Ticket(string id, string deskId, string teamId, DateTime? dueUtc)
    {
        var created = CalendarTestHarness.Now.AddDays(-1);
        var item = new ContentItem
        {
            ContentItemId = id,
            ContentType = TicketsConstants.DefaultContentType,
            Latest = true,
            Published = true,
            DisplayText = id,
            CreatedUtc = created,
            ModifiedUtc = created,
        };
        item.Apply(new TicketPart { Number = id, DeskId = deskId, TeamId = teamId, StatusCategory = TicketStatusCategory.Open, PriorityRank = 2, NextDueUtc = dueUtc });

        return item;
    }

    private sealed class StoreConnections : IDbConnectionAccessor
    {
        private readonly IStore _store;

        public StoreConnections(IStore store)
        {
            _store = store;
        }

        public DbConnection CreateConnection()
            => (DbConnection)_store.Configuration.ConnectionFactory.CreateConnection();
    }
}
