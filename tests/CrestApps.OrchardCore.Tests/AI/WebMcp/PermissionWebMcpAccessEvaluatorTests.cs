using System.Security.Claims;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using Microsoft.AspNetCore.Authorization;
using Moq;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

public sealed class PermissionWebMcpAccessEvaluatorTests
{
    [Fact]
    public async Task CanUseAsync_AllowsASignedInUserWithThePermission()
    {
        // Arrange
        var evaluator = CreateEvaluator(allowed: true, out _);

        // Act
        var canUse = await evaluator.CanUseAsync(SignedInUser(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(canUse);
    }

    [Fact]
    public async Task CanUseAsync_RefusesASignedInUserWithoutThePermission()
    {
        // Arrange
        var evaluator = CreateEvaluator(allowed: false, out _);

        // Act
        var canUse = await evaluator.CanUseAsync(SignedInUser(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(canUse);
    }

    [Fact]
    public async Task CanUseAsync_RefusesAnonymousUsersWithoutCheckingThePermission()
    {
        // Arrange
        var evaluator = CreateEvaluator(allowed: true, out var authorizationService);

        // Act
        var anonymous = await evaluator.CanUseAsync(new ClaimsPrincipal(new ClaimsIdentity()), TestContext.Current.CancellationToken);
        var missing = await evaluator.CanUseAsync(null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(anonymous);
        Assert.False(missing);
        authorizationService.Verify(
            service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()),
            Times.Never);
    }

    private static PermissionWebMcpAccessEvaluator CreateEvaluator(bool allowed, out Mock<IAuthorizationService> authorizationService)
    {
        authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        return new PermissionWebMcpAccessEvaluator(authorizationService.Object);
    }

    private static ClaimsPrincipal SignedInUser()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test"));
}
