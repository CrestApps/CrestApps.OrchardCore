using System.Security.Claims;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Chat.Controllers;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// The "Review AI conversation" page loads a session unscoped so a system-owned session (an automated SMS or voice
/// conversation, which has no user) can be reviewed, and read-only rendering was added for it. Confirmed live. What
/// keeps that safe is that such a session is served only when an access provider for its owning resource says so;
/// these pin that gate.
/// </summary>
public sealed class AIChatAdminControllerSystemSessionReviewTests
{
    [Fact]
    public async Task Index_ASystemSessionThatNoAccessProviderAuthorizes_IsForbidden()
    {
        // Arrange
        var harness = new Harness(new AIChatSession { SessionId = "session-1", ProfileId = "profile-1" }, providers: [false, false]);

        // Act
        var result = await harness.IndexAsync();

        // Assert
        Assert.IsType<ForbidResult>(result);
        harness.DisplayManager.Verify(
            manager => manager.BuildEditorAsync(It.IsAny<AIChatSession>(), It.IsAny<IUpdateModel>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Index_ASystemSessionWithNoAccessProvidersRegistered_IsForbidden()
    {
        // Arrange
        var harness = new Harness(new AIChatSession { SessionId = "session-1", ProfileId = "profile-1" }, providers: []);

        // Act
        var result = await harness.IndexAsync();

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_ASystemSessionThatAnAccessProviderAuthorizes_IsServed()
    {
        // Arrange
        var harness = new Harness(new AIChatSession { SessionId = "session-1", ProfileId = "profile-1" }, providers: [false, true]);

        // Act
        var result = await harness.IndexAsync();

        // Assert
        Assert.IsType<ViewResult>(result);
        harness.DisplayManager.Verify(
            manager => manager.BuildEditorAsync(
                It.Is<AIChatSession>(session => session.SessionId == "session-1"),
                It.IsAny<IUpdateModel>(),
                false,
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
    }

    // The session id comes from the URL, so a session of another profile must not be reachable through this profile's
    // page -- the access providers are asked about this profile, not the session's.
    [Fact]
    public async Task Index_ASessionOfAnotherProfile_IsNotFound()
    {
        // Arrange
        var harness = new Harness(new AIChatSession { SessionId = "session-1", ProfileId = "profile-2" }, providers: [true]);

        // Act
        var result = await harness.IndexAsync();

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Index_AnotherUsersSession_IsForbiddenEvenWhenAProviderWouldAuthorize()
    {
        // Arrange
        var harness = new Harness(new AIChatSession { SessionId = "session-1", ProfileId = "profile-1", UserId = "user-2" }, providers: [true]);

        // Act
        var result = await harness.IndexAsync();

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    private sealed class Harness
    {
        private readonly AdminController _controller;

        public Harness(AIChatSession storedSession, bool[] providers)
        {
            var profileManager = new Mock<IAIProfileManager>();
            profileManager
                .Setup(manager => manager.FindByIdAsync("profile-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AIProfile { ItemId = "profile-1" });

            var sessionManager = new Mock<IAIChatSessionManager>();
            sessionManager
                .Setup(manager => manager.FindByIdAsync(storedSession.SessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(storedSession);
            sessionManager
                .Setup(manager => manager.PageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<AIChatSessionQueryContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AIChatSessionResult { Count = 0, Sessions = [] });

            var accessProviders = providers.Select(allows =>
            {
                var provider = new Mock<IAIChatSessionAccessProvider>();
                provider
                    .Setup(candidate => candidate.CanAccessAsync(It.IsAny<ClaimsPrincipal>(), "profile-1", storedSession.SessionId, "activity-1"))
                    .ReturnsAsync(allows);

                return provider.Object;
            }).ToArray();

            var authorizationService = new Mock<IAuthorizationService>();
            authorizationService
                .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync(AuthorizationResult.Success());

            _controller = new AdminController(
                profileManager.Object,
                sessionManager.Object,
                authorizationService.Object,
                accessProviders,
                DisplayManager.Object,
                Mock.Of<IDisplayManager<AIChatSessionListOptions>>(),
                Mock.Of<IUpdateModelAccessor>(),
                Mock.Of<IShapeFactory>(),
                Mock.Of<INotifier>(),
                NullLogger<AdminController>.Instance,
                Mock.Of<IHtmlLocalizer<AdminController>>(),
                new PassThroughStringLocalizer<AdminController>())
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test")),
                    },
                },
            };
        }

        public Mock<IDisplayManager<AIChatSession>> DisplayManager { get; } = new();

        public Task<IActionResult> IndexAsync()
            => _controller.Index("profile-1", "session-1", "activity-1", Options.Create(new PagerOptions()));
    }
}
