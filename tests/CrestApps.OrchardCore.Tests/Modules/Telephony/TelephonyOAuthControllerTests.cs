using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Controllers;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Telephony;

public sealed class TelephonyOAuthControllerTests
{
    [Fact]
    public async Task Disconnect_WhenAuthorizedAndRemoteRevocationIsConfirmed_ReturnsOk()
    {
        // Arrange
        var authenticationService = new Mock<ITelephonyAuthenticationService>();
        authenticationService
            .Setup(service => service.DisconnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success());
        var controller = CreateController(authenticationService.Object, isAuthorized: true);

        // Act
        var result = await controller.Disconnect();

        // Assert
        Assert.IsType<OkResult>(result);
        authenticationService.Verify(
            service => service.DisconnectAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Disconnect_WhenRemoteRevocationIsNotConfirmed_ReturnsWarningPayload()
    {
        // Arrange
        var authenticationService = new Mock<ITelephonyAuthenticationService>();
        authenticationService
            .Setup(service => service.DisconnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Unknown("The provider did not confirm the remote revocation."));
        var controller = CreateController(authenticationService.Object, isAuthorized: true);

        // Act
        var result = await controller.Disconnect();

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"remoteRevocationConfirmed\":false", json, StringComparison.Ordinal);
        Assert.Contains("The provider did not confirm the remote revocation.", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Disconnect_WhenUnauthorized_ReturnsForbid()
    {
        // Arrange
        var authenticationService = new Mock<ITelephonyAuthenticationService>();
        var controller = CreateController(authenticationService.Object, isAuthorized: false);

        // Act
        var result = await controller.Disconnect();

        // Assert
        Assert.IsType<ForbidResult>(result);
        authenticationService.Verify(
            service => service.DisconnectAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static TelephonyOAuthController CreateController(
        ITelephonyAuthenticationService authenticationService,
        bool isAuthorized)
    {
        return new TelephonyOAuthController(
            authenticationService,
            new TestAuthorizationService(isAuthorized),
            new EphemeralDataProtectionProvider(),
            NullLogger<TelephonyOAuthController>.Instance)
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
