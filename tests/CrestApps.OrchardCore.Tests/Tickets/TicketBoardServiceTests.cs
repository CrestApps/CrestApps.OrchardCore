using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Moving a card on a board sets the ticket's status through the ticket service, with the board named on the
/// timeline. A column with several statuses asks which one unless it has a default; a move the user may not make, one
/// made on a ticket that changed since the board loaded, and one the life cycle refuses are refused with the reason and
/// leave the ticket as it was. A board's columns list only the tickets the viewer's desk scope covers.
/// </summary>
public sealed class TicketBoardServiceTests
{
    private static readonly TicketStatus _onHold = new() { ItemId = "onhold", Name = "With a supplier", Category = TicketStatusCategory.OnHold, Order = 40 };

    [Fact]
    public async Task ACardDroppedOnAColumn_SetsTheTicketsStatus_AndTheTimelineNamesTheBoard()
    {
        // Arrange
        await using var kit = await KitAsync(out _);
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;

        // Act
        var result = await MoveAsync(kit, new TicketBoardMove { TicketId = ticketId, ColumnId = "doing", ShownStatusId = TicketKit.New.ItemId });

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.True(result.Changed);
        Assert.Equal(TicketKit.Open.ItemId, (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>().StatusId);
        Assert.Contains(await kit.EntriesAsync(ticketId), entry => entry.Body?.Contains("Support board", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task AColumnWithSeveralStatusesAndNoDefault_AsksWhichOne_AndChangesNothing()
    {
        // Arrange
        await using var kit = await KitAsync(out _);
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;

        // Act
        var result = await MoveAsync(kit, new TicketBoardMove { TicketId = ticketId, ColumnId = "waiting" });

        // Assert
        Assert.True(result.NeedsChoice);
        Assert.False(result.Succeeded);
        Assert.Equal([TicketKit.Pending.ItemId, _onHold.ItemId], result.Choices.Select(status => status.ItemId));
        Assert.Equal(TicketKit.New.ItemId, (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>().StatusId);
    }

    [Fact]
    public async Task TheStatusChosen_OrTheColumnsDefault_IsTheOneSet()
    {
        // Arrange
        await using var kit = await KitAsync(out _);
        var chosenId = (await kit.CreateAsync("Chosen")).Ticket.ContentItemId;
        var defaultId = (await kit.CreateAsync("Default")).Ticket.ContentItemId;
        var board = Board();
        board.Columns[2].DefaultStatusId = _onHold.ItemId;

        // Act
        var chosen = await MoveAsync(kit, new TicketBoardMove { TicketId = chosenId, ColumnId = "waiting", StatusId = TicketKit.Pending.ItemId });
        var byDefault = await MoveAsync(kit, new TicketBoardMove { TicketId = defaultId, ColumnId = "waiting" }, board);

        // Assert
        Assert.True(chosen.Succeeded, string.Join(" ", chosen.Errors));
        Assert.True(byDefault.Succeeded, string.Join(" ", byDefault.Errors));
        Assert.Equal(TicketKit.Pending.ItemId, (await kit.GetAsync(chosenId)).GetOrCreate<TicketPart>().StatusId);
        Assert.Equal(_onHold.ItemId, (await kit.GetAsync(defaultId)).GetOrCreate<TicketPart>().StatusId);
    }

    [Fact]
    public async Task AStatusNotInTheColumn_IsRefused()
    {
        // Arrange
        await using var kit = await KitAsync(out _);
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;

        // Act
        var result = await MoveAsync(kit, new TicketBoardMove { TicketId = ticketId, ColumnId = "waiting", StatusId = TicketKit.Solved.ItemId });

        // Assert
        Assert.Single(result.Errors);
        Assert.Equal(TicketKit.New.ItemId, (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>().StatusId);
    }

    [Fact]
    public async Task AUserWhoMayNotEditTheTicket_IsRefused_AndTheTicketStays()
    {
        // Arrange
        await using var kit = await KitAsync(out var access);
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;
        access.Allowed.Remove(TicketOperation.Edit);

        // Act
        var result = await MoveAsync(kit, new TicketBoardMove { TicketId = ticketId, ColumnId = "doing" });

        // Assert
        Assert.Contains("may not change", Assert.Single(result.Errors), StringComparison.Ordinal);
        Assert.False(result.Changed);
        Assert.Equal(TicketKit.New.ItemId, (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>().StatusId);
    }

    [Fact]
    public async Task ATicketTheUserMayNotSee_IsAnsweredAsNotFound()
    {
        // Arrange
        await using var kit = await KitAsync(out var access);
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;
        access.Allowed.Clear();

        // Act
        var result = await MoveAsync(kit, new TicketBoardMove { TicketId = ticketId, ColumnId = "doing" });

        // Assert
        Assert.Contains("wasn't found", Assert.Single(result.Errors), StringComparison.Ordinal);
        Assert.Equal(TicketKit.New.ItemId, (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>().StatusId);
    }

    [Fact]
    public async Task ATicketChangedSinceTheBoardLoaded_IsRefusedAsStale()
    {
        // Arrange
        await using var kit = await KitAsync(out _);
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;
        await kit.RunAsync(services => services.GetRequiredService<ITicketService>().UpdateAsync(ticketId, new TicketUpdate { StatusId = TicketKit.Pending.ItemId }, cancellationToken: TestContext.Current.CancellationToken));

        // Act: the board still showed it as new.
        var result = await MoveAsync(kit, new TicketBoardMove { TicketId = ticketId, ColumnId = "done", ShownStatusId = TicketKit.New.ItemId });

        // Assert
        Assert.True(result.Stale);
        Assert.Single(result.Errors);
        Assert.Equal(TicketKit.Pending.ItemId, (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>().StatusId);
    }

    [Fact]
    public async Task AMoveTheLifeCycleRefuses_ComesBackWithTheTicketServicesReason()
    {
        // Arrange: a closed ticket is final.
        await using var kit = await KitAsync(out _);
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;
        await kit.RunAsync(services => services.GetRequiredService<ITicketService>().UpdateAsync(ticketId, new TicketUpdate { StatusId = TicketKit.Closed.ItemId }, cancellationToken: TestContext.Current.CancellationToken));

        // Act
        var result = await MoveAsync(kit, new TicketBoardMove { TicketId = ticketId, ColumnId = "doing" });

        // Assert
        Assert.NotEmpty(result.Errors);
        Assert.False(result.Changed);
        Assert.Equal(TicketKit.Closed.ItemId, (await kit.GetAsync(ticketId)).GetOrCreate<TicketPart>().StatusId);
    }

    [Fact]
    public async Task ACardDroppedBackOnItsColumn_LeavesTheTicket_AndAsksNothing()
    {
        // Arrange
        await using var kit = await KitAsync(out _);
        var ticketId = (await kit.CreateAsync()).Ticket.ContentItemId;
        await kit.RunAsync(services => services.GetRequiredService<ITicketService>().UpdateAsync(ticketId, new TicketUpdate { StatusId = _onHold.ItemId }, cancellationToken: TestContext.Current.CancellationToken));
        var entriesBefore = (await kit.EntriesAsync(ticketId)).Count;

        // Act
        var result = await MoveAsync(kit, new TicketBoardMove { TicketId = ticketId, ColumnId = "waiting" });

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(result.Changed);
        Assert.Equal(entriesBefore, (await kit.EntriesAsync(ticketId)).Count);
    }

    [Fact]
    public async Task EachColumn_ListsItsTickets_ThroughTheViewersScope()
    {
        // Arrange
        await using var kit = await KitAsync(out var access);
        var first = (await kit.CreateAsync("First")).Ticket.ContentItemId;
        await kit.CreateAsync("Second");
        await MoveAsync(kit, new TicketBoardMove { TicketId = first, ColumnId = "doing" });

        // Act
        var columns = await kit.ReadAsync(services => services.GetRequiredService<ITicketBoardService>().LoadAsync(TicketBoardRulesTests.User("agent"), Board(), cancellationToken: TestContext.Current.CancellationToken));
        access.SeesNoDesk = true;
        var noDesk = await kit.ReadAsync(services => services.GetRequiredService<ITicketBoardService>().LoadAsync(TicketBoardRulesTests.User("agent"), Board(), cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["todo", "doing", "waiting", "done"], columns.Select(column => column.Column.Id));
        Assert.Equal(["Second"], columns[0].Tickets.Select(ticket => ticket.DisplayText));
        Assert.Equal(1, columns[0].Total);
        Assert.Equal(["First"], columns[1].Tickets.Select(ticket => ticket.DisplayText));
        Assert.Equal([TicketKit.Pending.ItemId, _onHold.ItemId], columns[2].Statuses.Select(status => status.ItemId));
        Assert.Empty(columns[2].Tickets);

        // Every column's query went through the viewer's scope, with the board's desk.
        Assert.All(access.Scoped, query => Assert.Equal(["support"], query.DeskIds));
        Assert.All(noDesk, column => Assert.Empty(column.Tickets));
    }

    private static Task<TicketBoardMoveResult> MoveAsync(TicketKit kit, TicketBoardMove move, TicketBoard board = null)
        => kit.RunAsync(services => services.GetRequiredService<ITicketBoardService>().MoveAsync(TicketBoardRulesTests.User("agent"), board ?? Board(), move, TestContext.Current.CancellationToken));

    private static TicketBoard Board()
        => new()
        {
            ItemId = "board",
            Name = "Support board",
            Query = new TicketQuery { DeskIds = ["support"] },
            Columns =
            [
                new TicketBoardColumn { Id = "todo", Name = "To do", StatusIds = [TicketKit.New.ItemId] },
                new TicketBoardColumn { Id = "doing", Name = "Doing", StatusIds = [TicketKit.Open.ItemId] },
                new TicketBoardColumn { Id = "waiting", Name = "Pending", StatusIds = [TicketKit.Pending.ItemId, _onHold.ItemId] },
                new TicketBoardColumn { Id = "done", Name = "Done", StatusIds = [TicketKit.Solved.ItemId, "deleted-status"] },
            ],
        };

    private static Task<TicketKit> KitAsync(out FakeAccess access)
    {
        var fake = new FakeAccess();
        access = fake;

        return TicketKit.CreateAsync(configureServices: services =>
        {
            var statuses = new[] { TicketKit.New, TicketKit.Open, TicketKit.Pending, _onHold, TicketKit.Solved, TicketKit.Closed };
            var catalog = new Mock<ICatalog<TicketStatus>>();
            catalog.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(statuses);
            catalog.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => statuses.FirstOrDefault(status => status.ItemId == id));

            services.AddSingleton(catalog.Object);
            services.AddSingleton<ITicketAccessService>(fake);
            services.AddSingleton(Mock.Of<ICatalog<TicketBoard>>());
            services.AddSingleton(Mock.Of<ITeamMembershipService>());
            services.AddSingleton<IAuthorizationService>(new TicketBoardRulesTests.PermissionClaimsAuthorizationService());
            services.AddSingleton(RecipeRoundTrip.Localizer<DefaultTicketBoardService>());
            services.AddScoped<ITicketBoardService, DefaultTicketBoardService>();
        });
    }

    // Lets the agent see and change the support desk's tickets, and records the queries scoped.
    private sealed class FakeAccess : ITicketAccessService
    {
        public HashSet<TicketOperation> Allowed { get; } = [TicketOperation.ViewAll, TicketOperation.ViewTeam, TicketOperation.ViewAssigned, TicketOperation.Edit];

        public bool SeesNoDesk { get; set; }

        public List<TicketQuery> Scoped { get; } = [];

        public Task<bool> CanAsync(ClaimsPrincipal user, ContentItem ticket, TicketOperation operation, CancellationToken cancellationToken = default)
            => Task.FromResult(Allowed.Contains(operation));

        public Task<IReadOnlyDictionary<string, TicketViewScope>> GetViewScopesAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, TicketViewScope>>(SeesNoDesk ? new Dictionary<string, TicketViewScope>() : new Dictionary<string, TicketViewScope> { ["support"] = TicketViewScope.All });

        public Task<TicketScopedQuery> ApplyScopeAsync(ClaimsPrincipal user, TicketQuery query, CancellationToken cancellationToken = default)
        {
            Scoped.Add(query);

            return Task.FromResult(SeesNoDesk ? null : new TicketScopedQuery { Query = query, AllDeskIds = ["support"], UserId = "agent" });
        }

        public Task<bool> CanBeAssignedAsync(string userId, string deskId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
