using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Proves the supervisor queue authorization gate refuses access unless the caller both holds the monitor
/// permission and is entitled to the specific queue, so a supervisor can never monitor a queue outside their
/// entitlements even when they carry the permission generally.
/// </summary>
public sealed class SupervisorQueueAuthorizationServiceTests
{
    [Theory]
    [InlineData(null, "user-1", "queue-1")]
    [InlineData("principal", null, "queue-1")]
    [InlineData("principal", "", "queue-1")]
    [InlineData("principal", "user-1", null)]
    [InlineData("principal", "user-1", "")]
    public async Task IsAuthorizedAsync_WithMissingArguments_ReturnsFalse(string principalMarker, string userId, string queueId)
    {
        // Arrange
        var authorizationService = new Mock<IAuthorizationService>(MockBehavior.Strict);
        var agentManager = new Mock<IAgentProfileManager>(MockBehavior.Strict);
        var service = new SupervisorQueueAuthorizationService(authorizationService.Object, agentManager.Object);
        var principal = principalMarker is null ? null : CreatePrincipal();

        // Act
        var authorized = await service.IsAuthorizedAsync(principal, userId, queueId, TestContext.Current.CancellationToken);

        // Assert - the guard short-circuits before touching either collaborator.
        Assert.False(authorized);
        authorizationService.VerifyNoOtherCalls();
        agentManager.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenMonitorPermissionIsDenied_ReturnsFalse_AndNeverResolvesTheAgent()
    {
        // Arrange
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(x => x.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Failed());

        var agentManager = new Mock<IAgentProfileManager>(MockBehavior.Strict);
        var service = new SupervisorQueueAuthorizationService(authorizationService.Object, agentManager.Object);

        // Act
        var authorized = await service.IsAuthorizedAsync(CreatePrincipal(), "user-1", "queue-1", TestContext.Current.CancellationToken);

        // Assert - a caller without the permission is refused before their entitlements are even loaded.
        Assert.False(authorized);
        agentManager.Verify(x => x.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenSupervisorLacksTheQueueEntitlement_ReturnsFalse()
    {
        // Arrange
        var service = CreateService(
            permitted: true,
            supervisor: new AgentProfile { UserId = "user-1", AllowedQueueIds = ["queue-other"] });

        // Act
        var authorized = await service.IsAuthorizedAsync(CreatePrincipal(), "user-1", "queue-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(authorized);
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenSupervisorProfileIsMissing_ReturnsFalse()
    {
        // Arrange
        var service = CreateService(permitted: true, supervisor: null);

        // Act
        var authorized = await service.IsAuthorizedAsync(CreatePrincipal(), "user-1", "queue-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(authorized);
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenPermittedAndEntitledToTheQueue_ReturnsTrue()
    {
        // Arrange
        var service = CreateService(
            permitted: true,
            supervisor: new AgentProfile { UserId = "user-1", AllowedQueueIds = ["queue-1"] });

        // Act
        var authorized = await service.IsAuthorizedAsync(CreatePrincipal(), "user-1", "queue-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(authorized);
    }

    [Fact]
    public async Task IsAuthorizedAsync_MatchesTheQueueEntitlementCaseInsensitively()
    {
        // Arrange - queue identifiers are matched case-insensitively so a casing difference never denies a
        // genuinely entitled supervisor.
        var service = CreateService(
            permitted: true,
            supervisor: new AgentProfile { UserId = "user-1", AllowedQueueIds = ["QUEUE-1"] });

        // Act
        var authorized = await service.IsAuthorizedAsync(CreatePrincipal(), "user-1", "queue-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(authorized);
    }

    private static SupervisorQueueAuthorizationService CreateService(bool permitted, AgentProfile supervisor)
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(x => x.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(permitted ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(x => x.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(supervisor);

        return new SupervisorQueueAuthorizationService(authorizationService.Object, agentManager.Object);
    }

    private static ClaimsPrincipal CreatePrincipal()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test"));
}
