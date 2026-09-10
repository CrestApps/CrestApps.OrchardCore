using System.Security.Claims;
using System.Threading.RateLimiting;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class VoiceIngressEndpointTests
{
    [Fact]
    public async Task HandleAsync_WhenTheCallerDisconnects_StillRoutesTheCallToCompletion()
    {
        // Routing an inbound call performs a sequence of durable writes that is not atomic. Binding that work to
        // the caller's connection lets a hang-up abandon it midway and strand the call, so the request abort
        // token must never reach the router.

        // Arrange
        var observedToken = default(CancellationToken?);

        var router = CreateRouter(token => observedToken = token);

        var httpContext = new DefaultHttpContext();

        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        httpContext.RequestAborted = aborted.Token;

        // Act
        var result = await VoiceIngressEndpoint.HandleAsync(
            new InboundVoiceEvent(),
            CreateAllowingAuthorizationService(),
            router.Object,
            CreateLimiter(isAcquired: true),
            CreateWorkManager(admits: true),
            httpContext);

        // Assert
        Assert.NotNull(observedToken);
        Assert.False(observedToken.Value.IsCancellationRequested);
        Assert.False(observedToken.Value.CanBeCanceled);
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);

        router.Verify(
            value => value.RouteInboundAsync(It.IsAny<InboundVoiceEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenIngressCapacityIsExhausted_RejectsInsteadOfQueueingUnbounded()
    {
        // Admission is what bounds resource consumption. Without it an authorized client or a stalled dependency
        // could accumulate indefinitely many in-flight routes.

        // Arrange
        var router = new Mock<IVoiceContactCenterCallRouter>();
        var httpContext = new DefaultHttpContext();

        // Act
        var result = await VoiceIngressEndpoint.HandleAsync(
            new InboundVoiceEvent(),
            CreateAllowingAuthorizationService(),
            router.Object,
            CreateLimiter(isAcquired: false),
            CreateWorkManager(admits: true),
            httpContext);

        // Assert
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, statusResult.StatusCode);

        router.Verify(
            value => value.RouteInboundAsync(It.IsAny<InboundVoiceEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenTheVoiceFeatureIsQuiescing_RejectsWithServiceUnavailable()
    {
        // Arrange
        var router = new Mock<IVoiceContactCenterCallRouter>();

        // Act
        var result = await VoiceIngressEndpoint.HandleAsync(
            new InboundVoiceEvent(),
            CreateAllowingAuthorizationService(),
            router.Object,
            CreateLimiter(isAcquired: true),
            CreateWorkManager(admits: false),
            new DefaultHttpContext());

        // Assert
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusResult.StatusCode);

        router.Verify(
            value => value.RouteInboundAsync(It.IsAny<InboundVoiceEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenTheCallerIsNotAuthorized_DoesNotRoute()
    {
        // Arrange
        var router = new Mock<IVoiceContactCenterCallRouter>();

        // Act
        var result = await VoiceIngressEndpoint.HandleAsync(
            new InboundVoiceEvent(),
            CreateDenyingAuthorizationService(),
            router.Object,
            CreateLimiter(isAcquired: true),
            CreateWorkManager(admits: true),
            new DefaultHttpContext());

        // Assert
        Assert.NotNull(result);

        router.Verify(
            value => value.RouteInboundAsync(It.IsAny<InboundVoiceEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenTheCallerIsNotAuthorized_DoesNotConsumeIngressCapacity()
    {
        // Authorization must be decided before capacity is taken, otherwise unauthenticated traffic can exhaust
        // admission for genuine calls.

        // Arrange
        var limiter = new Mock<IProviderWebhookIngressLimiter>();

        // Act
        await VoiceIngressEndpoint.HandleAsync(
            new InboundVoiceEvent(),
            CreateDenyingAuthorizationService(),
            new Mock<IVoiceContactCenterCallRouter>().Object,
            limiter.Object,
            CreateWorkManager(admits: true),
            new DefaultHttpContext());

        // Assert
        limiter.Verify(
            value => value.AcquireConcurrencyAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Mock<IVoiceContactCenterCallRouter> CreateRouter(Action<CancellationToken> onRoute)
    {
        var router = new Mock<IVoiceContactCenterCallRouter>();

        router
            .Setup(value => value.RouteInboundAsync(It.IsAny<InboundVoiceEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InboundVoiceEvent, CancellationToken>((_, token) => onRoute(token))
            .ReturnsAsync(new InboundVoiceRoutingResult());

        return router;
    }

    private static IProviderWebhookIngressLimiter CreateLimiter(bool isAcquired)
    {
        // A real limiter is used so the lease reflects genuine acquisition semantics rather than a stub.
        var limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = 1,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });

        if (!isAcquired)
        {
            // Consume the only permit so the lease handed to the endpoint is a rejection.
            _ = limiter.AttemptAcquire();
        }

        var mock = new Mock<IProviderWebhookIngressLimiter>();
        mock
            .Setup(value => value.AcquireConcurrencyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ProviderWebhookIngressLease(limiter.AttemptAcquire()));

        return mock.Object;
    }

    private static IContactCenterFeatureWorkManager CreateWorkManager(bool admits)
    {
        var manager = new Mock<IContactCenterFeatureWorkManager>();

        manager
            .Setup(value => value.TryEnter(It.IsAny<string>()))
            .Returns(admits ? new Mock<IContactCenterFeatureWorkLease>().Object : null);

        return manager.Object;
    }

    private static IAuthorizationService CreateAllowingAuthorizationService()
        => CreateAuthorizationService(AuthorizationResult.Success());

    private static IAuthorizationService CreateDenyingAuthorizationService()
        => CreateAuthorizationService(AuthorizationResult.Failed());

    private static IAuthorizationService CreateAuthorizationService(AuthorizationResult outcome)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(value => value.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(outcome);

        authorizationService
            .Setup(value => value.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<string>()))
            .ReturnsAsync(outcome);

        return authorizationService.Object;
    }
}
