using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterActivityOwnershipAuthorizationHandlerTests
{
    private const string ActivityId = "activity-1";

    [Fact]
    public async Task HandleAsync_WhenWorkStateReservesActivityForUserBeforeActivityIsProjected_ShouldSucceed()
    {
        // Arrange: a preview offer reserved the activity, but the activity's own copy has no owner yet.
        var handler = CreateHandler(
            authorizeOwnActivity: true,
            new ContactCenterWorkState
            {
                ActivityItemId = ActivityId,
                ReservedById = "user-1",
            });
        var context = CreateContext(CreatePrincipal("user-1"), new OmnichannelActivity { ItemId = ActivityId });

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_WhenWorkStateAssignsActivityToUser_ShouldSucceed()
    {
        // Arrange
        var handler = CreateHandler(
            authorizeOwnActivity: true,
            new ContactCenterWorkState
            {
                ActivityItemId = ActivityId,
                AssignedToId = "user-1",
            });
        var context = CreateContext(CreatePrincipal("user-1"), new OmnichannelActivity { ItemId = ActivityId });

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_WhenResourceIsActivityContainer_ShouldSucceed()
    {
        // Arrange
        var handler = CreateHandler(
            authorizeOwnActivity: true,
            new ContactCenterWorkState
            {
                ActivityItemId = ActivityId,
                ReservedById = "user-1",
            });
        var container = new OmnichannelActivityContainer(
            new OmnichannelActivity { ItemId = ActivityId },
            new ContentTypeDefinition("Subject", "Subject"),
            contact: null,
            user: null);
        var context = CreateContext(CreatePrincipal("user-1"), container);

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_WhenWorkStateBelongsToAnotherUser_ShouldNotSucceed()
    {
        // Arrange
        var handler = CreateHandler(
            authorizeOwnActivity: true,
            new ContactCenterWorkState
            {
                ActivityItemId = ActivityId,
                ReservedById = "user-2",
                AssignedToId = "user-3",
            });
        var context = CreateContext(CreatePrincipal("user-1"), new OmnichannelActivity { ItemId = ActivityId });

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_WhenActivityWasNeverRouted_ShouldNotSucceed()
    {
        // Arrange
        var handler = CreateHandler(authorizeOwnActivity: true, workState: null);
        var context = CreateContext(CreatePrincipal("user-1"), new OmnichannelActivity { ItemId = ActivityId });

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_WhenUserLacksCompleteOwnPermission_ShouldNotSucceed()
    {
        // Arrange
        var handler = CreateHandler(
            authorizeOwnActivity: false,
            new ContactCenterWorkState
            {
                ActivityItemId = ActivityId,
                ReservedById = "user-1",
            });
        var context = CreateContext(CreatePrincipal("user-1"), new OmnichannelActivity { ItemId = ActivityId });

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_WhenPermissionIsNotCompleteActivity_ShouldNotLookUpWorkState()
    {
        // Arrange
        var workStateManager = new Mock<IContactCenterWorkStateManager>(MockBehavior.Strict);
        var handler = new ContactCenterActivityOwnershipAuthorizationHandler(
            workStateManager.Object,
            new Lazy<IAuthorizationService>(CreateAuthorizationService(authorizeOwnActivity: true)));
        var context = new AuthorizationHandlerContext(
            [new PermissionRequirement(OmnichannelConstants.Permissions.EditActivity)],
            CreatePrincipal("user-1"),
            new OmnichannelActivity { ItemId = ActivityId });

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
        workStateManager.VerifyNoOtherCalls();
    }

    private static ContactCenterActivityOwnershipAuthorizationHandler CreateHandler(bool authorizeOwnActivity, ContactCenterWorkState workState)
    {
        var workStateManager = new Mock<IContactCenterWorkStateManager>();
        workStateManager
            .Setup(manager => manager.FindByActivityIdAsync(ActivityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workState);

        return new ContactCenterActivityOwnershipAuthorizationHandler(
            workStateManager.Object,
            new Lazy<IAuthorizationService>(CreateAuthorizationService(authorizeOwnActivity)));
    }

    private static IAuthorizationService CreateAuthorizationService(bool authorizeOwnActivity)
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

        return authorizationService.Object;
    }

    private static AuthorizationHandlerContext CreateContext(ClaimsPrincipal user, object resource)
    {
        return new AuthorizationHandlerContext(
            [new PermissionRequirement(OmnichannelConstants.Permissions.CompleteActivity)],
            user,
            resource);
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
