using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Timeline;

/// <summary>
/// A content item's timeline (an account, a contact, a lead): internal entries are for the people who may edit the
/// item, public ones for signed-in people who may view it, and a visitor who isn't signed in reads nothing, even on a
/// site where anyone may view content.
/// </summary>
public sealed class ContentItemTimelineAccessTests
{
    private static readonly RecordRef _account = new(TimelineConstants.ContentItemKind, "account-1");

    [Fact]
    public async Task InternalEntries_AreOnlyForThoseWhoMayEditTheItem()
    {
        // Arrange
        var access = Create();

        // Act
        var viewer = await access.CanViewAsync(User("viewer", CommonPermissions.ViewContent), _account, TimelineVisibility.Internal, TestContext.Current.CancellationToken);
        var editor = await access.CanViewAsync(User("editor", CommonPermissions.ViewContent, CommonPermissions.EditContent), _account, TimelineVisibility.Internal, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(viewer);
        Assert.True(editor);
    }

    [Fact]
    public async Task PublicEntries_AreForSignedInPeopleWhoMayViewTheItem()
    {
        // Arrange
        var access = Create();

        // Act
        var viewer = await access.CanViewAsync(User("viewer", CommonPermissions.ViewContent), _account, TimelineVisibility.Public, TestContext.Current.CancellationToken);
        var stranger = await access.CanViewAsync(User("stranger"), _account, TimelineVisibility.Public, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(viewer);
        Assert.False(stranger);
    }

    [Fact]
    public async Task AVisitorWhoIsNotSignedIn_ReadsNothing_EvenWhereAnyoneMayViewContent()
    {
        // Arrange
        var access = Create();
        var visitor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(Permission.ClaimType, CommonPermissions.ViewContent.Name)]));

        // Act
        var publicEntries = await access.CanViewAsync(visitor, _account, TimelineVisibility.Public, TestContext.Current.CancellationToken);
        var internalEntries = await access.CanViewAsync(visitor, _account, TimelineVisibility.Internal, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(publicEntries);
        Assert.False(internalEntries);
    }

    [Fact]
    public async Task AMissingItem_HasNoTimeline()
    {
        // Arrange
        var access = Create();

        // Act
        var sees = await access.CanViewAsync(User("editor", CommonPermissions.ViewContent, CommonPermissions.EditContent), new RecordRef(TimelineConstants.ContentItemKind, "missing"), TimelineVisibility.Public, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(sees);
    }

    private static ContentItemTimelineAccess Create()
    {
        var contentManager = new Mock<IContentManager>();

        contentManager
            .Setup(manager => manager.GetAsync("account-1", It.IsAny<VersionOptions>()))
            .ReturnsAsync(new ContentItem { ContentItemId = "account-1", ContentType = "Account" });

        return new ContentItemTimelineAccess(contentManager.Object, new PermissionClaimsAuthorizationService());
    }

    private static ClaimsPrincipal User(string userId, params Permission[] permissions)
        => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId), .. permissions.Select(permission => new Claim(Permission.ClaimType, permission.Name))],
            "test"));

    private sealed class PermissionClaimsAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            var granted = requirements.OfType<PermissionRequirement>().All(requirement => user.HasClaim(Permission.ClaimType, requirement.Permission.Name));

            return Task.FromResult(granted ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }
}
