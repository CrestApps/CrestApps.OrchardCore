using System.Security.Claims;
using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.Acp;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// SEC-C17i: an ACP request acts for a linked buyer only when the buyer's token grants the checkout scope, as UCP
/// requires; a token issued with other scopes signs the platform in, not the buyer.
/// </summary>
public sealed class AcpLinkedBuyerScopeTests
{
    [Fact]
    public async Task SignInAsync_ATokenWithoutTheCheckoutScope_DoesNotActForTheBuyer()
    {
        // Act
        var (request, error) = await AcpRequestFilter.SignInAsync(CreateRequest("openid profile"), [], "2026-04-17");

        // Assert
        Assert.Null(error);
        Assert.Null(request.Context.UserId);
    }

    [Fact]
    public async Task SignInAsync_ATokenWithTheCheckoutScope_ActsForTheBuyer()
    {
        // Act
        var (request, _) = await AcpRequestFilter.SignInAsync(CreateRequest("openid dev.ucp.shopping.checkout:manage"), [], "2026-04-17");

        // Assert
        Assert.Equal("buyer-1", request.Context.UserId);
    }

    private static DefaultHttpContext CreateRequest(string scope)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("client_id", "client-1"),
            new Claim("sub", "buyer-1"),
            new Claim("scope", scope),
        ], "Bearer"));

        var authentication = new Mock<IAuthenticationService>();
        authentication.Setup(service => service.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string>()))
            .ReturnsAsync(AuthenticateResult.Success(new AuthenticationTicket(principal, "Api")));

        var platforms = new Mock<IAgentPlatformStore>();
        platforms.Setup(store => store.FindByClientIdAsync("client-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentPlatform { ItemId = "platform-1", Enabled = true, Protocols = [AgenticCommerceConstants.Protocols.Acp] });

        var services = new ServiceCollection()
            .AddSingleton(authentication.Object)
            .AddSingleton(new AgentPlatformAuthenticator(platforms.Object, NullLogger<AgentPlatformAuthenticator>.Instance))
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };

        httpContext.Request.Headers.Authorization = "Bearer token-1";

        return httpContext;
    }
}
