using System.Security.Claims;
using CrestApps.OrchardCore.Tests.DataConnectors;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace CrestApps.OrchardCore.Tests.Regression;

/// <summary>
/// Audit defect 4: the user search behind every user picker only required a signed-in user, so a storefront shopper
/// could list user IDs and names, filtered by role.
/// </summary>
public sealed class UserSearchEndpointAuthorizationTests
{
    [Fact]
    public async Task Search_ForASignedInUserWithoutTheSearchUsersPermission_IsForbidden()
    {
        // Arrange
        var searchService = new Mock<IUserSearchService>(MockBehavior.Strict);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "shopper")], "Test")),
        };

        // Act
        var result = await UserSearchEndpoints.HandleAsync(
            "a",
            null,
            ["Administrator"],
            httpContext,
            new PermissionClaimsAuthorizationService(),
            searchService.Object,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<ForbidHttpResult>(result);
        searchService.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("SearchUsers")]
    [InlineData("AccessAdminPanel")]
    public async Task Search_ForAUserWithTheSearchUsersPermissionOrAdminPanelAccess_ReturnsTheMatches(string permission)
    {
        // Arrange
        var searchService = new Mock<IUserSearchService>();

        searchService
            .Setup(service => service.SearchAsync(It.Is<UserSearchQuery>(query => query.Text == "ann"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new UserSearchResult
                {
                    UserId = "user-1",
                    UserName = "ann",
                    NormalizedUserName = "ANN",
                    DisplayName = "Ann Lee",
                    IsEnabled = true,
                },
            ]);

        var httpContext = new DefaultHttpContext
        {
            User = PermissionClaimsAuthorizationService.UserWith(permission),
        };

        // Act
        var result = await UserSearchEndpoints.HandleAsync(
            "ann",
            "userName",
            [],
            httpContext,
            new PermissionClaimsAuthorizationService(),
            searchService.Object,
            TestContext.Current.CancellationToken);

        // Assert
        var ok = Assert.IsType<Ok<IEnumerable<UserSearchItem>>>(result);
        var item = Assert.Single(ok.Value);
        Assert.Equal("ann", item.Value);
        Assert.Equal("Ann Lee", item.Text);
    }

    [Fact]
    public void SearchUsersPermission_IsImpliedByAdminPanelAccess()
    {
        // Act
        var impliedBy = UserSearchPermissions.SearchUsers.ImpliedBy.Select(permission => permission.Name);

        // Assert
        Assert.Contains("AccessAdminPanel", impliedBy);
    }
}
