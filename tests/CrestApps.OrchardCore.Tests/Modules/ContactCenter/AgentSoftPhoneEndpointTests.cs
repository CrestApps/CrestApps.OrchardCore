using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class AgentSoftPhoneEndpointTests
{
    [Fact]
    public async Task SyncQueuedVoiceWork_WhenAuthorized_OffersQueuedVoiceWorkForCurrentUser()
    {
        var queuedVoiceWorkOfferService = new Mock<IQueuedVoiceWorkOfferService>();
        var httpContext = CreateHttpContext();

        var result = await AgentSoftPhoneEndpoints.HandleSyncQueuedVoiceWorkAsync(
            new TestAuthorizationService(true),
            CreateAntiforgery(),
            [queuedVoiceWorkOfferService.Object],
            httpContext);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        queuedVoiceWorkOfferService.Verify(
            service => service.OfferForUserAsync("user-1", httpContext.RequestAborted),
            Times.Once);
    }

    [Fact]
    public async Task SyncQueuedVoiceWork_WhenUnauthorized_ReturnsForbidden()
    {
        var queuedVoiceWorkOfferService = new Mock<IQueuedVoiceWorkOfferService>();

        var result = await AgentSoftPhoneEndpoints.HandleSyncQueuedVoiceWorkAsync(
            new TestAuthorizationService(false),
            CreateAntiforgery(),
            [queuedVoiceWorkOfferService.Object],
            CreateHttpContext());

        Assert.IsType<ForbidHttpResult>(result);
        queuedVoiceWorkOfferService.Verify(
            service => service.OfferForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CurrentIncomingOffer_WhenOfferExists_ReturnsOffer()
    {
        var offer = new PendingIncomingCallOffer();
        var pendingIncomingCallOfferService = new Mock<IPendingIncomingCallOfferService>();
        pendingIncomingCallOfferService
            .Setup(service => service.GetForUserAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(offer);

        var result = await AgentSoftPhoneEndpoints.HandleCurrentIncomingOfferAsync(
            new TestAuthorizationService(true),
            [pendingIncomingCallOfferService.Object],
            CreateHttpContext());

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Same(offer, Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
    }

    [Fact]
    public async Task CurrentIncomingOffer_WhenOfferMissing_ReturnsNotFound()
    {
        var pendingIncomingCallOfferService = new Mock<IPendingIncomingCallOfferService>();

        var result = await AgentSoftPhoneEndpoints.HandleCurrentIncomingOfferAsync(
            new TestAuthorizationService(true),
            [pendingIncomingCallOfferService.Object],
            CreateHttpContext());

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    private static IAntiforgery CreateAntiforgery()
    {
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery
            .Setup(service => service.ValidateRequestAsync(It.IsAny<HttpContext>()))
            .Returns(Task.CompletedTask);

        return antiforgery.Object;
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
            ], "Test")),
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
