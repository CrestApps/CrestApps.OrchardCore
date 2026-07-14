using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Controllers;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AgentSoftPhoneControllerTests
{
    [Fact]
    public async Task SignIn_WhenNoQueueOrCampaignIsSelected_ReturnsBadRequest()
    {
        // Arrange
        var presenceManager = new Mock<IAgentPresenceManager>();
        var controller = CreateController(true, presenceManager.Object);

        // Act
        var result = await controller.SignIn([], [], "/Admin");

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Select at least one queue or campaign before signing in.", badRequest.Value);
        presenceManager.Verify(
            manager => manager.SignInAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SignIn_WhenPresenceManagerRejectsEntitlement_ReturnsBadRequest()
    {
        // Arrange
        var presenceManager = new Mock<IAgentPresenceManager>();
        presenceManager.Setup(manager => manager.SignInAsync(
                "user-1",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AgentEntitlementDeniedException("user-1"));
        var controller = CreateController(true, presenceManager.Object);

        // Act
        var result = await controller.SignIn(["q1"], [], "/Admin");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static AgentSoftPhoneController CreateController(
        bool isAuthorized,
        IAgentPresenceManager presenceManager = null)
    {
        var authorizationService = new TestAuthorizationService(isAuthorized);
        var controller = new AgentSoftPhoneController(
            presenceManager ?? Mock.Of<IAgentPresenceManager>(),
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
