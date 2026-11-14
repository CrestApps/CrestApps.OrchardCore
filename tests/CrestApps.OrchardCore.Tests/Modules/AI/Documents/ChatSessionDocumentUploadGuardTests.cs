using System.Security.Claims;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Security;
using CrestApps.OrchardCore.AI.Documents.Endpoints;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Documents;

/// <summary>
/// SEC-A5 (b): the package's upload endpoint creates and saves a session for a profile id before it authorizes, with
/// no session-start limit and no cap on the number of documents.
/// </summary>
public sealed class ChatSessionDocumentUploadGuardTests
{
    [Fact]
    public async Task InvokeAsync_WithoutAccess_RefusesBeforeTheEndpointCreatesASession()
    {
        var (httpContext, _) = CreateContext(authorized: false, files: 1);
        var called = false;

        var result = await new ChatSessionDocumentUploadGuard().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
        {
            called = true;

            return ValueTask.FromResult<object>(Results.Ok());
        });

        Assert.False(called);
        var json = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, json.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenSessionStartsAreThrottled_Answers429()
    {
        var (httpContext, _) = CreateContext(authorized: true, files: 1, throttled: true);
        var called = false;

        await new ChatSessionDocumentUploadGuard().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
        {
            called = true;

            return ValueTask.FromResult<object>(Results.Ok());
        });

        Assert.False(called);
        Assert.Equal(StatusCodes.Status429TooManyRequests, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// The package's session-start limiter counts anonymous visitors only, so without Orchard Core's Rate Limits a
    /// signed-in caller could start upload sessions in a loop. Each signed-in user is held to the
    /// <see cref="ChatSessionDocumentUploadOptions.UploadPolicyName"/> attempt policy.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ASignedInCallerPastTheSessionStartQuota_Answers429()
    {
        var limiter = new Mock<IAttemptLimiter>();
        limiter
            .Setup(l => l.AcquireAsync(ChatSessionDocumentUploadOptions.UploadPolicyName, "user:user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttemptLimitResult(false, 0, TimeSpan.FromMinutes(10)));

        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test"));
        var (httpContext, _) = CreateContext(authorized: true, files: 1, user: user, attemptLimiter: limiter.Object);
        var called = false;

        await new ChatSessionDocumentUploadGuard().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
        {
            called = true;

            return ValueTask.FromResult<object>(Results.Ok());
        });

        Assert.False(called);
        Assert.Equal(StatusCodes.Status429TooManyRequests, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// The hub lets a signed-in caller start sessions without a limit, so counting only the uploads that start a session
    /// left a loop of new sessions with the most documents each without a ceiling. Every upload is counted.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ASignedInCallerPastTheQuota_IsRefusedForAnExistingSessionToo()
    {
        var limiter = new Mock<IAttemptLimiter>();
        limiter
            .Setup(l => l.AcquireAsync(ChatSessionDocumentUploadOptions.UploadPolicyName, "user:user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttemptLimitResult(false, 0, TimeSpan.FromMinutes(10)));

        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test"));
        var (httpContext, _) = CreateContext(authorized: true, files: 1, sessionDocuments: 0, user: user, attemptLimiter: limiter.Object);
        var called = false;

        await new ChatSessionDocumentUploadGuard().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
        {
            called = true;

            return ValueTask.FromResult<object>(Results.Ok());
        });

        Assert.False(called);
        Assert.Equal(StatusCodes.Status429TooManyRequests, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ASignedInCallersUploadToAnExistingSession_IsCounted()
    {
        var limiter = new Mock<IAttemptLimiter>();
        limiter
            .Setup(l => l.AcquireAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttemptLimitResult(true, 10, TimeSpan.Zero));

        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test"));
        var (httpContext, _) = CreateContext(authorized: true, files: 1, sessionDocuments: 0, user: user, attemptLimiter: limiter.Object);

        await new ChatSessionDocumentUploadGuard().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ => ValueTask.FromResult<object>(Results.Ok()));

        limiter.Verify(l => l.AcquireAsync(ChatSessionDocumentUploadOptions.UploadPolicyName, "user:user-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// M14: an anonymous visitor on a public profile could upload, remove and upload again to one session without a
    /// count, each upload running extraction and embeddings. Uploads are now counted by the visitor's network, with the
    /// real attempt limiter.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_AnAnonymousVisitorsUploadsToAnExistingSession_AreCountedByNetwork()
    {
        // Arrange
        var options = new AttemptLimitOptions();
        options.AddPolicy(ChatSessionDocumentUploadOptions.UploadPolicyName, 3, TimeSpan.FromHours(1));
        var limiter = new DistributedAttemptLimiter(
            new Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache(Options.Create(new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions())),
            new global::OrchardCore.Locking.LocalLock(Microsoft.Extensions.Logging.Abstractions.NullLogger<global::OrchardCore.Locking.LocalLock>.Instance),
            new global::OrchardCore.Environment.Shell.ShellSettings { Name = "Default" },
            Mock.Of<global::OrchardCore.Modules.IClock>(clock => clock.UtcNow == DateTime.UtcNow),
            Options.Create(options),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DistributedAttemptLimiter>.Instance);
        var calls = 0;
        var statuses = new List<int>();

        // Act
        for (var i = 0; i < 4; i++)
        {
            var (httpContext, _) = CreateContext(authorized: true, files: 1, sessionDocuments: 0, attemptLimiter: limiter);
            httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.7");

            await new ChatSessionDocumentUploadGuard().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
            {
                calls++;

                return ValueTask.FromResult<object>(Results.Ok());
            });

            statuses.Add(httpContext.Response.StatusCode);
        }

        var (otherNetwork, _) = CreateContext(authorized: true, files: 1, sessionDocuments: 0, attemptLimiter: limiter);
        otherNetwork.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("198.51.100.9");
        await new ChatSessionDocumentUploadGuard().InvokeAsync(new DefaultEndpointFilterInvocationContext(otherNetwork), _ =>
        {
            calls++;

            return ValueTask.FromResult<object>(Results.Ok());
        });

        // Assert
        Assert.Equal(4, calls);
        Assert.Equal(StatusCodes.Status429TooManyRequests, statuses[^1]);
        Assert.NotEqual(StatusCodes.Status429TooManyRequests, otherNetwork.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheSessionWouldHoldTooManyDocuments_RefusesTheUpload()
    {
        var (httpContext, _) = CreateContext(authorized: true, files: 2, sessionDocuments: 19);
        var called = false;

        var result = await new ChatSessionDocumentUploadGuard().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
        {
            called = true;

            return ValueTask.FromResult<object>(Results.Ok());
        });

        Assert.False(called);
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, status.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WithAccessAndRoom_CallsTheEndpoint()
    {
        var (httpContext, _) = CreateContext(authorized: true, files: 1);
        var called = false;

        await new ChatSessionDocumentUploadGuard().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
        {
            called = true;

            return ValueTask.FromResult<object>(Results.Ok());
        });

        Assert.True(called);
    }

    [Fact]
    public async Task InvokeAsync_WithASessionIdThatDoesNotExist_StillAuthorizesTheProfile()
    {
        // A made-up session id must not skip the checks: the endpoint would start a new session for the profile.
        var (httpContext, _) = CreateContext(authorized: false, files: 1, unknownSessionId: true);
        var called = false;

        var result = await new ChatSessionDocumentUploadGuard().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
        {
            called = true;

            return ValueTask.FromResult<object>(Results.Ok());
        });

        Assert.False(called);
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ToAnExistingSessionWithoutAccess_Refuses()
    {
        var (httpContext, _) = CreateContext(authorized: false, files: 1, sessionDocuments: 0);
        var called = false;

        var result = await new ChatSessionDocumentUploadGuard().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
        {
            called = true;

            return ValueTask.FromResult<object>(Results.Ok());
        });

        Assert.False(called);
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WithoutAForm_RefusesInsteadOfSkippingTheChecks()
    {
        var (httpContext, _) = CreateContext(authorized: false, files: 1);
        httpContext.Request.ContentType = "application/json";
        httpContext.Features.Set<IFormFeature>(new FormFeature(httpContext.Request));
        var called = false;

        var result = await new ChatSessionDocumentUploadGuard().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
        {
            called = true;

            return ValueTask.FromResult<object>(Results.Ok());
        });

        Assert.False(called);
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, status.StatusCode);
    }

    private static (DefaultHttpContext HttpContext, Mock<IAIChatSessionManager> SessionManager) CreateContext(bool authorized, int files, bool throttled = false, int? sessionDocuments = null, bool unknownSessionId = false, ClaimsPrincipal user = null, IAttemptLimiter attemptLimiter = null)
    {
        var profile = new AIProfile { ItemId = "profile-1" };
        var profileManager = new Mock<IAIProfileManager>();
        profileManager.Setup(manager => manager.FindByIdAsync("profile-1", It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var sessionManager = new Mock<IAIChatSessionManager>();

        if (sessionDocuments is not null)
        {
            sessionManager
                .Setup(manager => manager.FindAsync("session-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AIChatSession
                {
                    SessionId = "session-1",
                    ProfileId = "profile-1",
                    Documents = [.. Enumerable.Range(0, sessionDocuments.Value).Select(i => new ChatDocumentInfo { DocumentId = "d" + i })],
                });
        }

        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var sessionStartLimiter = new Mock<IChatSessionStartRateLimiter>();
        sessionStartLimiter
            .Setup(limiter => limiter.EvaluateAsync(It.IsAny<PromptSecurityContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(throttled ? RateLimitResult.Throttled(60, 5, 5) : RateLimitResult.Allowed);

        var services = new ServiceCollection()
            .AddSingleton(profileManager.Object)
            .AddSingleton(sessionManager.Object)
            .AddSingleton(authorizationService.Object)
            .AddSingleton(sessionStartLimiter.Object)
            .AddSingleton(Options.Create(new ChatSessionDocumentUploadOptions()))
            .AddSingleton(typeof(Microsoft.Extensions.Localization.IStringLocalizer<>), typeof(CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<>))
            .AddSingleton(attemptLimiter ?? Mock.Of<IAttemptLimiter>(limiter => limiter.AcquireAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult(new AttemptLimitResult(true, 10, TimeSpan.Zero))))
            .BuildServiceProvider();

        var fields = new Dictionary<string, StringValues>();

        if (sessionDocuments is not null)
        {
            fields["sessionId"] = "session-1";
        }
        else
        {
            fields["profileId"] = "profile-1";
        }

        if (unknownSessionId)
        {
            fields["sessionId"] = "made-up";
        }

        var formFiles = new FormFileCollection();

        for (var i = 0; i < files; i++)
        {
            formFiles.Add(new FormFile(Stream.Null, 0, 0, "files", $"file{i}.txt"));
        }

        var httpContext = new DefaultHttpContext { RequestServices = services, User = user ?? new ClaimsPrincipal(new ClaimsIdentity()) };
        httpContext.Request.ContentType = "multipart/form-data; boundary=x";
        httpContext.Features.Set<IFormFeature>(new FormFeature(new FormCollection(fields, formFiles)));

        return (httpContext, sessionManager);
    }
}
