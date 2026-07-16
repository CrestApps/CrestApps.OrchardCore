using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class OmnichannelAIChatSessionAccessProviderTests
{
    [Fact]
    public async Task CanAccessAsync_WhenActivityMatchesAndUserCanListActivities_ShouldReturnTrue()
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            ItemId = "activity-1",
            AIProfileId = "profile-1",
            AISessionId = "session-1",
        };
        var provider = CreateProvider(activity, authorize: true);

        // Act
        var result = await provider.CanAccessAsync(
            new ClaimsPrincipal(),
            "profile-1",
            "session-1",
            "activity-1");

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("other-profile", "session-1")]
    [InlineData("profile-1", "other-session")]
    public async Task CanAccessAsync_WhenActivityDoesNotOwnSession_ShouldReturnFalse(
        string profileId,
        string sessionId)
    {
        // Arrange
        var activity = new OmnichannelActivity
        {
            ItemId = "activity-1",
            AIProfileId = "profile-1",
            AISessionId = "session-1",
        };
        var provider = CreateProvider(activity, authorize: true);

        // Act
        var result = await provider.CanAccessAsync(
            new ClaimsPrincipal(),
            profileId,
            sessionId,
            "activity-1");

        // Assert
        Assert.False(result);
    }

    private static OmnichannelAIChatSessionAccessProvider CreateProvider(
        OmnichannelActivity activity,
        bool authorize)
    {
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(manager => manager.FindByIdAsync(activity.ItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);

        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(authorize
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());

        return new OmnichannelAIChatSessionAccessProvider(
            activityManager.Object,
            authorizationService.Object,
            Mock.Of<IContentManager>());
    }
}
