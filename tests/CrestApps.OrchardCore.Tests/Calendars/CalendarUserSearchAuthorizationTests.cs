using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Endpoints;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Tests.DataConnectors;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using OrchardCore.Admin;
using UsersPermissions = OrchardCore.Users.UsersPermissions;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// The user search behind the calendar pickers is a minimal API under <c>Admin/</c>, which the admin filter doesn't
/// guard, so it checks Access admin panel itself, and matches and shows emails only to someone who may view users.
/// </summary>
public sealed class CalendarUserSearchAuthorizationTests
{
    private static readonly string _access = CalendarPermissions.AccessCalendars.Name;
    private static readonly string _admin = AdminPermissions.AccessAdminPanel.Name;

    [Fact]
    public async Task Search_ForASignedInUserWhoMayUseCalendarsButNotOpenTheAdmin_IsForbidden()
    {
        // Arrange
        var searchService = new Mock<IUserSearchService>(MockBehavior.Strict);

        // Act
        var result = await SearchAsync(PermissionClaimsAuthorizationService.UserWith(_access), searchService.Object, "a");

        // Assert
        Assert.IsType<ForbidHttpResult>(result);
        searchService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Search_ForAnAdminUserWhoMayNotUseCalendars_IsForbidden()
    {
        // Arrange
        var searchService = new Mock<IUserSearchService>(MockBehavior.Strict);

        // Act
        var result = await SearchAsync(PermissionClaimsAuthorizationService.UserWith(_admin), searchService.Object, "a");

        // Assert
        Assert.IsType<ForbidHttpResult>(result);
        searchService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Search_ForAnAdminUserWhoMayNotViewUsers_MatchesAndShowsUserNamesOnly()
    {
        // Arrange
        var (searchService, queries) = CreateSearch();

        // Act
        var result = await SearchAsync(PermissionClaimsAuthorizationService.UserWith(_access, _admin), searchService.Object, "ann");

        // Assert
        var item = Assert.Single(ReadItems(result));
        Assert.Equal("user-1", item.Value);
        Assert.Equal("ann", item.Text);
        Assert.False(Assert.Single(queries).MatchEmail);
    }

    // Orchard Core names the View users permission "View Users", with a space.
    public static TheoryData<string> ViewUsersPermissions => [UsersPermissions.ViewUsers.Name, UsersPermissions.ManageUsers.Name];

    [Theory]
    [MemberData(nameof(ViewUsersPermissions))]
    public async Task Search_ForAnAdminUserWhoMayViewUsers_MatchesAndShowsEmails(string permission)
    {
        // Arrange
        var (searchService, queries) = CreateSearch();

        // Act
        var result = await SearchAsync(PermissionClaimsAuthorizationService.UserWith(_access, _admin, permission), searchService.Object, "ann");

        // Assert
        var item = Assert.Single(ReadItems(result));
        Assert.Equal("user-1", item.Value);
        Assert.Equal("ann (ann@example.test)", item.Text);
        Assert.True(Assert.Single(queries).MatchEmail);
    }

    private static Task<IResult> SearchAsync(ClaimsPrincipal user, IUserSearchService searchService, string query)
    {
        return CalendarAdminEndpoints.SearchUsersAsync(
            query,
            new DefaultHttpContext { User = user },
            new PermissionClaimsAuthorizationService(),
            new CalendarUserLookup(searchService));
    }

    private static (Mock<IUserSearchService> Service, List<UserSearchQuery> Queries) CreateSearch()
    {
        var queries = new List<UserSearchQuery>();
        var searchService = new Mock<IUserSearchService>();

        searchService
            .Setup(service => service.SearchAsync(It.IsAny<UserSearchQuery>(), It.IsAny<CancellationToken>()))
            .Callback<UserSearchQuery, CancellationToken>((query, _) => queries.Add(query))
            .ReturnsAsync(
            [
                new UserSearchResult
                {
                    UserId = "user-1",
                    UserName = "ann",
                    NormalizedUserName = "ANN",
                    Email = "ann@example.test",
                    DisplayName = "Ann Lee",
                    IsEnabled = true,
                },
            ]);

        return (searchService, queries);
    }

    private static List<CalendarUserItem> ReadItems(IResult result)
    {
        var value = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);

        return JsonSerializer.Deserialize<List<CalendarUserItem>>(JsonSerializer.Serialize(value), JsonSerializerOptions.Web);
    }
}
