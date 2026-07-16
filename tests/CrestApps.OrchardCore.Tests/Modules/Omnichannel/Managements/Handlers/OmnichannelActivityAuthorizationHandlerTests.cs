using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Handlers;

public sealed class OmnichannelActivityAuthorizationHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenUserCanCompleteOwnAssignedActivity_ShouldSucceed()
    {
        // Arrange
        var handler = CreateHandler(authorizeOwnActivity: true);
        var user = CreatePrincipal("user-1");
        var activity = new OmnichannelActivity
        {
            AssignedToId = "user-1",
        };
        var context = CreateContext(user, activity);

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_WhenUserCanCompleteOwnReservedActivity_ShouldSucceed()
    {
        // Arrange
        var handler = CreateHandler(authorizeOwnActivity: true);
        var user = CreatePrincipal("user-1");
        var activity = new OmnichannelActivity
        {
            ReservedById = "user-1",
        };
        var context = CreateContext(user, activity);

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_WhenUserDoesNotOwnActivity_ShouldNotSucceed()
    {
        // Arrange
        var handler = CreateHandler(authorizeOwnActivity: true);
        var user = CreatePrincipal("user-1");
        var activity = new OmnichannelActivity
        {
            AssignedToId = "user-2",
            ReservedById = "user-3",
        };
        var context = CreateContext(user, activity);

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_WhenUserLacksCompleteOwnPermission_ShouldNotSucceed()
    {
        // Arrange
        var handler = CreateHandler(authorizeOwnActivity: false);
        var user = CreatePrincipal("user-1");
        var activity = new OmnichannelActivity
        {
            AssignedToId = "user-1",
        };
        var context = CreateContext(user, activity);

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    private static OmnichannelActivityAuthorizationHandler CreateHandler(bool authorizeOwnActivity)
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(authorizeOwnActivity
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());

        var services = new ServiceCollection()
            .AddSingleton(authorizationService.Object)
            .BuildServiceProvider();

        return new OmnichannelActivityAuthorizationHandler(services);
    }

    private static AuthorizationHandlerContext CreateContext(ClaimsPrincipal user, OmnichannelActivity activity)
    {
        var requirement = new PermissionRequirement(OmnichannelConstants.Permissions.CompleteActivity);

        return new AuthorizationHandlerContext(
            [requirement],
            user,
            activity);
    }

    private static ClaimsPrincipal CreatePrincipal(string userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
        ],
        authenticationType: "Test"));
    }
}
