using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Controllers;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AgentSoftPhoneControllerTests
{
    [Fact]
    public async Task SyncQueuedVoiceWork_WhenAuthorized_OffersQueuedVoiceWorkForCurrentUser()
    {
        // Arrange
        var queuedVoiceWorkOfferService = new Mock<IQueuedVoiceWorkOfferService>();
        var controller = CreateController(true);

        // Act
        var result = await controller.SyncQueuedVoiceWork([queuedVoiceWorkOfferService.Object]);

        // Assert
        Assert.IsType<OkResult>(result);
        queuedVoiceWorkOfferService.Verify(service => service.OfferForUserAsync("user-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncQueuedVoiceWork_WhenUnauthorized_ReturnsForbid()
    {
        // Arrange
        var queuedVoiceWorkOfferService = new Mock<IQueuedVoiceWorkOfferService>();
        var controller = CreateController(false);

        // Act
        var result = await controller.SyncQueuedVoiceWork([queuedVoiceWorkOfferService.Object]);

        // Assert
        Assert.IsType<ForbidResult>(result);
        queuedVoiceWorkOfferService.Verify(service => service.OfferForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CurrentIncomingOffer_WhenOfferExists_ReturnsJson()
    {
        // Arrange
        var pendingIncomingCallOfferService = new Mock<IPendingIncomingCallOfferService>();
        pendingIncomingCallOfferService.Setup(service => service.GetForUserAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PendingIncomingCallOffer());
        var controller = CreateController(true);

        // Act
        var result = await controller.CurrentIncomingOffer([pendingIncomingCallOfferService.Object]);

        // Assert
        Assert.IsType<JsonResult>(result);
        pendingIncomingCallOfferService.Verify(service => service.GetForUserAsync("user-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CurrentIncomingOffer_WhenOfferMissing_ReturnsNotFound()
    {
        // Arrange
        var pendingIncomingCallOfferService = new Mock<IPendingIncomingCallOfferService>();
        var controller = CreateController(true);

        // Act
        var result = await controller.CurrentIncomingOffer([pendingIncomingCallOfferService.Object]);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    private static AgentSoftPhoneController CreateController(bool isAuthorized)
    {
        var authorizationService = new TestAuthorizationService(isAuthorized);
        var controller = new AgentSoftPhoneController(
            Mock.Of<IAgentPresenceManager>(),
            authorizationService,
            NullLogger<AgentSoftPhoneController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "user-1"),
                    ], "Test")),
                },
            },
        };

        return controller;
    }

    private sealed class TestAuthorizationService : IAuthorizationService
    {
        private readonly bool _isAuthorized;

        public TestAuthorizationService(bool isAuthorized)
        {
            _isAuthorized = isAuthorized;
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            return Task.FromResult(_isAuthorized
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            string policyName)
        {
            return Task.FromResult(_isAuthorized
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());
        }
    }
}
