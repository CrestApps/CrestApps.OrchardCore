using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Handlers;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// A board's columns are sound on save and on import: at least one column, each with a name and a status that exists,
/// a status in one column only, a default that is one of the column's statuses. A card dropped on a column gets the
/// status chosen, the column's default or its only status, and a column with several statuses and no default asks. A
/// board is open to everybody, a team or its owner, and only the right permission arranges it.
/// </summary>
public sealed class TicketBoardRulesTests
{
    private static readonly TicketStatus _new = new() { ItemId = "new", Name = "New", Category = TicketStatusCategory.New, Order = 10 };
    private static readonly TicketStatus _open = new() { ItemId = "open", Name = "Open", Category = TicketStatusCategory.Open, Order = 20 };
    private static readonly TicketStatus _requester = new() { ItemId = "requester", Name = "Waiting for the requester", Category = TicketStatusCategory.Pending, Order = 30 };
    private static readonly TicketStatus _thirdParty = new() { ItemId = "third-party", Name = "Waiting for a third party", Category = TicketStatusCategory.OnHold, Order = 40 };
    private static readonly TicketStatus _solved = new() { ItemId = "solved", Name = "Solved", Category = TicketStatusCategory.Solved, Order = 50 };

    private static readonly string[] _statusOrder = ["new", "open", "requester", "third-party", "solved"];

    [Fact]
    public async Task ASoundBoard_IsKept_AndEachColumnGetsAnId()
    {
        // Arrange
        var board = Board();
        board.Columns.ForEach(column => column.Id = null);
        var context = new ValidatingContext<TicketBoard>(board);

        // Act
        await Handler().ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Result.Succeeded, string.Join(" ", context.Result.Errors.Select(error => error.ErrorMessage)));
        Assert.All(board.Columns, column => Assert.False(string.IsNullOrEmpty(column.Id)));
        Assert.Equal(board.Columns.Count, board.Columns.Select(column => column.Id).Distinct().Count());
    }

    [Fact]
    public async Task ABoardWithNoColumn_IsRefused()
    {
        // Arrange
        var context = new ValidatingContext<TicketBoard>(new TicketBoard { ItemId = "b1", Name = "Empty" });

        // Act
        await Handler().ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var error = Assert.Single(context.Result.Errors);
        Assert.Contains(nameof(TicketBoard.Columns), error.MemberNames);
    }

    [Fact]
    public async Task AStatusInTwoColumns_IsRefused_WithBothColumnNames()
    {
        // Arrange
        var board = Board();
        board.Columns[1].StatusIds.Add("new");
        var context = new ValidatingContext<TicketBoard>(board);

        // Act
        await Handler().ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var error = Assert.Single(context.Result.Errors);
        Assert.Contains("To do", error.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("Doing", error.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("no-status")]
    [InlineData("unknown-status")]
    [InlineData("default-elsewhere")]
    [InlineData("no-name")]
    public async Task ABrokenColumn_IsRefused(string broken)
    {
        // Arrange
        var board = Board();
        var column = board.Columns[2];

        switch (broken)
        {
            case "no-status":
                column.StatusIds.Clear();
                column.DefaultStatusId = null;
                break;
            case "unknown-status":
                column.StatusIds.Add("gone");
                break;
            case "default-elsewhere":
                column.DefaultStatusId = "solved";
                break;
            case "no-name":
                column.Name = "  ";
                break;
        }

        var context = new ValidatingContext<TicketBoard>(board);

        // Act
        await Handler().ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(context.Result.Succeeded);
        Assert.All(context.Result.Errors, error => Assert.Contains(nameof(TicketBoard.Columns), error.MemberNames));
    }

    [Fact]
    public async Task TooManyColumns_AreRefused()
    {
        // Arrange
        var board = Board();
        board.Columns = [.. Enumerable.Range(1, TicketBoard.MaxColumns + 1).Select(index => new TicketBoardColumn { Name = $"C{index}", StatusIds = [index == 1 ? "new" : string.Empty] })];
        board.Columns.Skip(1).ToList().ForEach(column => column.StatusIds = []);
        var context = new ValidatingContext<TicketBoard>(board);

        // Act
        await Handler().ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(context.Result.Errors, error => error.ErrorMessage.Contains(TicketBoard.MaxColumns.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
    }

    [Fact]
    public void Normalizing_DropsRepeatedStatuses_KeepsColumnIds_AndClearsTheQuerysStatuses()
    {
        // Arrange
        var board = Board();
        board.Columns[0].StatusIds = ["new", "new", " "];
        board.Columns[1].Id = board.Columns[0].Id;
        board.Query.StatusIds = ["open"];
        board.Query.Categories = [TicketStatusCategory.Open];
        var firstId = board.Columns[0].Id;

        // Act
        TicketBoardHandler.Normalize(board);

        // Assert
        Assert.Equal(["new"], board.Columns[0].StatusIds);
        Assert.Equal(firstId, board.Columns[0].Id);
        Assert.NotEqual(firstId, board.Columns[1].Id);
        Assert.Empty(board.Query.StatusIds);
        Assert.Empty(board.Query.Categories);
    }

    [Fact]
    public void ATicketShowsInTheColumnOfItsStatus()
    {
        // Arrange
        var board = Board();

        // Act and assert
        Assert.Equal("waiting", TicketBoardColumns.ColumnOf(board, "third-party").Id);
        Assert.Equal("todo", TicketBoardColumns.ColumnOf(board, "new").Id);
        Assert.Null(TicketBoardColumns.ColumnOf(board, "closed"));
        Assert.Null(TicketBoardColumns.ColumnOf(board, null));
    }

    [Fact]
    public void ADropOnAColumnWithOneStatus_GetsThatStatus()
    {
        // Act
        var target = TicketBoardColumns.Target(Board().Columns[1], _statusOrder, "new", chosenStatusId: null);

        // Assert
        Assert.Equal(TicketBoardTargetKind.Status, target.Kind);
        Assert.Equal("open", target.StatusId);
    }

    [Fact]
    public void ADropOnAColumnWithSeveralStatuses_AndNoDefault_AsksWhichOne_InTheStatusListsOrder()
    {
        // Arrange
        var column = Board().Columns[2];
        column.StatusIds = ["third-party", "requester"];

        // Act
        var target = TicketBoardColumns.Target(column, _statusOrder, "open", chosenStatusId: null);

        // Assert
        Assert.Equal(TicketBoardTargetKind.Choose, target.Kind);
        Assert.Equal(["requester", "third-party"], target.Choices);
    }

    [Fact]
    public void ADropOnAColumnWithSeveralStatuses_TakesItsDefault_OrTheStatusChosen()
    {
        // Arrange
        var column = Board().Columns[2];
        column.DefaultStatusId = "requester";

        // Act
        var byDefault = TicketBoardColumns.Target(column, _statusOrder, "open", chosenStatusId: null);
        var chosen = TicketBoardColumns.Target(column, _statusOrder, "open", chosenStatusId: "third-party");
        var elsewhere = TicketBoardColumns.Target(column, _statusOrder, "open", chosenStatusId: "solved");

        // Assert
        Assert.Equal(new TicketBoardTarget(TicketBoardTargetKind.Status, "requester"), byDefault);
        Assert.Equal(new TicketBoardTarget(TicketBoardTargetKind.Status, "third-party"), chosen);
        Assert.Equal(TicketBoardTargetKind.NotInColumn, elsewhere.Kind);
    }

    [Fact]
    public void ADropOnItsOwnColumn_ChangesNothing_AndAsksNothing()
    {
        // Act
        var target = TicketBoardColumns.Target(Board().Columns[2], _statusOrder, "third-party", chosenStatusId: null);

        // Assert
        Assert.Equal(TicketBoardTargetKind.Unchanged, target.Kind);
    }

    [Fact]
    public void ADropOnAColumnWhoseStatusesAreGone_IsRefused()
    {
        // Arrange
        var column = new TicketBoardColumn { Id = "x", Name = "Gone", StatusIds = ["deleted"] };

        // Act
        var target = TicketBoardColumns.Target(column, _statusOrder, "new", chosenStatusId: null);

        // Assert
        Assert.Equal(TicketBoardTargetKind.NoStatus, target.Kind);
    }

    [Theory]
    [InlineData(null, null, "ana", true)]
    [InlineData("ana", null, "ana", true)]
    [InlineData("ana", null, "ben", false)]
    [InlineData(null, "it", "ben", true)]
    [InlineData(null, "hr", "ben", false)]
    [InlineData("ana", "it", "ben", true)]
    public void ABoardIsOpenToEverybody_ATeam_OrItsOwner(string ownerId, string teamId, string userId, bool visible)
    {
        // Arrange
        var board = new TicketBoard { OwnerId = ownerId, SharedWithTeamId = teamId };
        var teams = new HashSet<string>(userId == "ben" ? ["it"] : [], StringComparer.Ordinal);

        // Act and assert
        Assert.Equal(visible, DefaultTicketBoardService.IsVisible(board, userId, teams));
    }

    [Fact]
    public void AColumnsQuery_KeepsTheBoardsFilters_TakesTheColumnsStatuses_AndTheViewersSearch()
    {
        // Arrange
        var board = Board();
        board.Query = new TicketQuery
        {
            DeskIds = ["it"],
            TeamIds = ["network"],
            ContentTypes = ["Incident"],
            Assignee = TicketAssignees.Me,
            Categories = [TicketStatusCategory.Solved],
            Sort = TicketSort.Priority,
        };

        // Act
        var query = DefaultTicketBoardService.QueryOf(board, new TicketBoardFilter { Search = " printer ", Assignee = TicketAssignees.Unassigned }, ["requester", "third-party"]);

        // Assert
        Assert.Equal(["it"], query.DeskIds);
        Assert.Equal(["network"], query.TeamIds);
        Assert.Equal(["Incident"], query.ContentTypes);
        Assert.Equal(["requester", "third-party"], query.StatusIds);
        Assert.Empty(query.Categories);
        Assert.Equal("printer", query.Search);
        Assert.Equal(TicketAssignees.Unassigned, query.Assignee);
        Assert.Equal(TicketSort.Priority, query.Sort);
        Assert.Equal(ITicketBoardService.CardsPerColumn, query.Take);
    }

    [Fact]
    public async Task OnlyTheRightPermission_ArrangesABoard_AndNobodyArrangesAnotherUsersBoard()
    {
        // Arrange
        var shared = new TicketBoard { ItemId = "shared", Name = "IT" };
        var anas = new TicketBoard { ItemId = "anas", Name = "Mine", OwnerId = "ana" };
        var anaOwnOnly = User("ana", TicketsPermissions.ManageOwnTicketBoards);
        var benShared = User("ben", TicketsPermissions.ManageTicketBoards);
        var settingsManager = User("cleo", TicketsPermissions.ManageTicketSettings);
        var agent = User("dan");

        // Act and assert
        Assert.False(await Service().CanManageAsync(anaOwnOnly, shared));
        Assert.True(await Service().CanManageAsync(anaOwnOnly, anas));
        Assert.True(await Service().CanManageAsync(benShared, shared));
        Assert.False(await Service().CanManageAsync(benShared, anas));
        Assert.True(await Service().CanManageAsync(settingsManager, shared));
        Assert.False(await Service().CanManageAsync(agent, shared));
        Assert.True(await Service().CanCreateAsync(benShared));
        Assert.True(await Service().CanCreateAsync(anaOwnOnly));
        Assert.False(await Service().CanShareAsync(anaOwnOnly));
        Assert.False(await Service().CanCreateAsync(agent));
    }

    [Fact]
    public async Task TheBoardsListed_AreThoseTheUserMayOpen_AndNoneForAUserWhoSeesNoDesk()
    {
        // Arrange
        var boards = new[]
        {
            new TicketBoard { ItemId = "everyone", Name = "Everyone", Order = 2 },
            new TicketBoard { ItemId = "it", Name = "IT", SharedWithTeamId = "it-team", Order = 1 },
            new TicketBoard { ItemId = "hr", Name = "HR", SharedWithTeamId = "hr-team" },
            new TicketBoard { ItemId = "anas", Name = "Ana's", OwnerId = "ana" },
            new TicketBoard { ItemId = "bens", Name = "Ben's", OwnerId = "ben" },
        };
        var memberships = new Mock<ITeamMembershipService>();
        memberships.Setup(service => service.GetMembershipsAsync("ana", It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TeamMember { TeamId = "it-team", UserId = "ana" }]);

        // Act
        var forAna = await Service(boards, memberships.Object).ListVisibleAsync(User("ana"), TestContext.Current.CancellationToken);
        var forNobody = await Service(boards, memberships.Object, seesNoDesk: true).ListVisibleAsync(User("ana"), TestContext.Current.CancellationToken);
        var bensBoard = await Service(boards, memberships.Object).FindVisibleAsync(User("ana"), "bens", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["anas", "it", "everyone"], forAna.Select(board => board.ItemId));
        Assert.Empty(forNobody);
        Assert.Null(bensBoard);
    }

    [Fact]
    public async Task ABoardFromARecipe_KeepsItsColumns_AndItsOwnerTravelsByUserName()
    {
        // Arrange
        var ana = Mock.Of<IUser>();
        var users = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        users.Setup(manager => manager.FindByNameAsync("ana")).ReturnsAsync(ana);
        users.Setup(manager => manager.FindByIdAsync("ana-here")).ReturnsAsync(ana);
        users.Setup(manager => manager.GetUserIdAsync(ana)).ReturnsAsync("ana-here");
        var services = new ServiceCollection().AddSingleton(users.Object).BuildServiceProvider();
        var recordHandler = new TicketSettingsRecordHandler<TicketBoard>(Mock.Of<ICatalog<TicketBoard>>(), Mock.Of<IClock>(), services, RecipeRoundTrip.Localizer<TicketSettingsRecordHandler<TicketBoard>>());
        var board = new TicketBoard { ItemId = "b1" };
        var json = JsonNode.Parse("""
            {
              "Name": "Ana's board",
              "OwnerUserName": "ana",
              "Query": { "DeskIds": ["it"] },
              "Columns": [
                { "Name": "To do", "StatusIds": ["new"] },
                { "Id": "waiting", "Name": "Pending", "StatusIds": ["requester", "third-party"], "DefaultStatusId": "requester", "Limit": 5 }
              ]
            }
            """);

        // Act
        await recordHandler.InitializingAsync(new InitializingContext<TicketBoard>(board, json), TestContext.Current.CancellationToken);
        var result = new ValidatingContext<TicketBoard>(board);
        await recordHandler.ValidatingAsync(result, TestContext.Current.CancellationToken);
        await Handler().ValidatingAsync(result, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Result.Succeeded, string.Join(" ", result.Result.Errors.Select(error => error.ErrorMessage)));
        Assert.Equal("ana-here", board.OwnerId);
        Assert.Equal(["it"], board.Query.DeskIds);
        Assert.Equal(["To do", "Pending"], board.Columns.Select(column => column.Name));
        Assert.False(string.IsNullOrEmpty(board.Columns[0].Id));
        Assert.Equal("waiting", board.Columns[1].Id);
        Assert.Equal("requester", board.Columns[1].DefaultStatusId);
        Assert.Equal(5, board.Columns[1].Limit);
    }

    internal static TicketBoard Board()
        => new()
        {
            ItemId = "board",
            Name = "IT board",
            Columns =
            [
                new TicketBoardColumn { Id = "todo", Name = "To do", StatusIds = ["new"] },
                new TicketBoardColumn { Id = "doing", Name = "Doing", StatusIds = ["open"] },
                new TicketBoardColumn { Id = "waiting", Name = "Pending", StatusIds = ["requester", "third-party"] },
                new TicketBoardColumn { Id = "done", Name = "Done", StatusIds = ["solved"] },
            ],
        };

    private static TicketBoardHandler Handler()
        => new(Catalog(_new, _open, _requester, _thirdParty, _solved), RecipeRoundTrip.Localizer<TicketBoardHandler>());

    private static DefaultTicketBoardService Service(TicketBoard[] boards = null, ITeamMembershipService memberships = null, bool seesNoDesk = false)
    {
        var access = new Mock<ITicketAccessService>();
        access.Setup(service => service.GetViewScopesAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seesNoDesk ? new Dictionary<string, TicketViewScope>() : new Dictionary<string, TicketViewScope> { ["support"] = TicketViewScope.All });

        return new DefaultTicketBoardService(
            Catalog(boards ?? []),
            Catalog(_new, _open, _requester, _thirdParty, _solved),
            Mock.Of<ITicketService>(),
            access.Object,
            memberships ?? Mock.Of<ITeamMembershipService>(),
            new PermissionClaimsAuthorizationService(),
            RecipeRoundTrip.Localizer<DefaultTicketBoardService>());
    }

    private static ICatalog<T> Catalog<T>(params T[] records)
        where T : CatalogItem
    {
        var catalog = new Mock<ICatalog<T>>();
        catalog.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(records);
        catalog.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => records.FirstOrDefault(record => record.ItemId == id));

        return catalog.Object;
    }

    internal static ClaimsPrincipal User(string userId, params Permission[] permissions)
        => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Name, userId), .. permissions.Select(permission => new Claim(Permission.ClaimType, permission.Name))],
            "test"));

    // Grants a permission held as a claim, or implied by one held, as Orchard Core's role handler does.
    internal sealed class PermissionClaimsAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(requirements.OfType<PermissionRequirement>().All(requirement => Holds(user, requirement.Permission))
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());

        private static bool Holds(ClaimsPrincipal user, Permission permission)
            => user.HasClaim(Permission.ClaimType, permission.Name) || (permission.ImpliedBy?.Any(implied => Holds(user, implied)) ?? false);
    }
}
