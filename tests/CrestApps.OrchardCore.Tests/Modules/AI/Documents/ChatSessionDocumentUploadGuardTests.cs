using System.Security.Claims;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Security;
using CrestApps.OrchardCore.AI.Documents.Endpoints;
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
    /// <see cref="ChatSessionDocumentUploadOptions.SessionStartPolicyName"/> attempt policy.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ASignedInCallerPastTheSessionStartQuota_Answers429()
    {
        var limiter = new Mock<IAttemptLimiter>();
        limiter
            .Setup(l => l.AcquireAsync(ChatSessionDocumentUploadOptions.SessionStartPolicyName, "user:user-1", It.IsAny<CancellationToken>()))
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
