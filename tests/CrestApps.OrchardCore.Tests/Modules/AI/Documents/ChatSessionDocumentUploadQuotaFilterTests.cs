using System.Security.Claims;
using CrestApps.Core.AI.Ingestion;
using CrestApps.OrchardCore.AI.Documents;
using CrestApps.OrchardCore.AI.Documents.Endpoints;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Documents;

/// <summary>
/// CrestApps.Core's chat session upload endpoint authorizes before it saves a session, limits session starts and caps the
/// documents of a session (CrestApps.Core #322, covered by its own tests). What stays here is the per-caller ceiling on
/// every upload, which the package does not have, and the tenant setting of the document cap.
/// </summary>
public sealed class ChatSessionDocumentUploadQuotaFilterTests
{
    /// <summary>
    /// The package's session-start limiter counts anonymous visitors only, so without Orchard Core's Rate Limits a
    /// signed-in caller could upload in a loop. Each signed-in user is held to the
    /// <see cref="ChatSessionDocumentUploadOptions.UploadPolicyName"/> attempt policy.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ASignedInCallerPastTheQuota_Answers429()
    {
        var limiter = new Mock<IAttemptLimiter>();
        limiter
            .Setup(l => l.AcquireAsync(ChatSessionDocumentUploadOptions.UploadPolicyName, "user:user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttemptLimitResult(false, 0, TimeSpan.FromMinutes(10)));

        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test"));
        var httpContext = CreateContext(user: user, attemptLimiter: limiter.Object);
        var called = false;

        await new ChatSessionDocumentUploadQuotaFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
        {
            called = true;

            return ValueTask.FromResult<object>(Results.Ok());
        });

        Assert.False(called);
        Assert.Equal(StatusCodes.Status429TooManyRequests, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ASignedInCallersUpload_IsCountedByTheUser()
    {
        var limiter = new Mock<IAttemptLimiter>();
        limiter
            .Setup(l => l.AcquireAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttemptLimitResult(true, 10, TimeSpan.Zero));

        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test"));
        var httpContext = CreateContext(user: user, attemptLimiter: limiter.Object);
        var called = false;

        await new ChatSessionDocumentUploadQuotaFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
        {
            called = true;

            return ValueTask.FromResult<object>(Results.Ok());
        });

        Assert.True(called);
        limiter.Verify(l => l.AcquireAsync(ChatSessionDocumentUploadOptions.UploadPolicyName, "user:user-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// M14: an anonymous visitor on a public profile could upload, remove and upload again to one session without a
    /// count, each upload running extraction and embeddings. Uploads are counted by the visitor's network, with the real
    /// attempt limiter.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_AnAnonymousVisitorsUploads_AreCountedByNetwork()
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
            var httpContext = CreateContext(attemptLimiter: limiter);
            httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.7");

            await new ChatSessionDocumentUploadQuotaFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
            {
                calls++;

                return ValueTask.FromResult<object>(Results.Ok());
            });

            statuses.Add(httpContext.Response.StatusCode);
        }

        var otherNetwork = CreateContext(attemptLimiter: limiter);
        otherNetwork.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("198.51.100.9");
        await new ChatSessionDocumentUploadQuotaFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(otherNetwork), _ =>
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
    public async Task InvokeAsync_WithoutAForm_RefusesInsteadOfFailingInTheEndpoint()
    {
        var httpContext = CreateContext();
        httpContext.Request.ContentType = "application/json";
        httpContext.Features.Set<IFormFeature>(new FormFeature(httpContext.Request));
        var called = false;

        var result = await new ChatSessionDocumentUploadQuotaFilter().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ =>
        {
            called = true;

            return ValueTask.FromResult<object>(Results.Ok());
        });

        Assert.False(called);
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, status.StatusCode);
    }

    /// <summary>
    /// The package endpoint enforces <see cref="ChatDocumentsOptions.MaxDocumentsPerSession"/>; the tenant's
    /// <c>CrestApps_AI:ChatSessionDocuments:MaxFilesPerSession</c> sets it, and leaving it out keeps 20. The package reads
    /// 0 or less as "no limit", so a value below 1 is held at 1 rather than removing the cap.
    /// </summary>
    [Theory]
    [InlineData(null, 20)]
    [InlineData("5", 5)]
    [InlineData("1", 1)]
    [InlineData("0", 1)]
    [InlineData("-3", 1)]
    public void TheTenantSetting_SetsThePackagesDocumentCap(string configured, int expected)
    {
        var settings = new Dictionary<string, string>();

        if (configured is not null)
        {
            settings[ChatSessionDocumentUploadOptions.SectionName + ":MaxFilesPerSession"] = configured;
        }

        var shellConfiguration = new ShellConfiguration(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        var services = new ServiceCollection();

        new ChatSessionDocumentsStartup(shellConfiguration).ConfigureServices(services);

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<ChatDocumentsOptions>>().Value;

        Assert.Equal(expected, options.MaxDocumentsPerSession);
    }

    private static DefaultHttpContext CreateContext(ClaimsPrincipal user = null, IAttemptLimiter attemptLimiter = null)
    {
        var services = new ServiceCollection()
            .AddSingleton(typeof(Microsoft.Extensions.Localization.IStringLocalizer<>), typeof(CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<>))
            .AddSingleton(attemptLimiter ?? Mock.Of<IAttemptLimiter>(limiter => limiter.AcquireAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult(new AttemptLimitResult(true, 10, TimeSpan.Zero))))
            .BuildServiceProvider();

        var formFiles = new FormFileCollection
        {
            new FormFile(Stream.Null, 0, 0, "files", "file0.txt"),
        };

        var httpContext = new DefaultHttpContext { RequestServices = services, User = user ?? new ClaimsPrincipal(new ClaimsIdentity()) };
        httpContext.Request.ContentType = "multipart/form-data; boundary=x";
        httpContext.Features.Set<IFormFeature>(new FormFeature(new FormCollection(new Dictionary<string, StringValues> { ["profileId"] = "profile-1" }, formFiles)));

        return httpContext;
    }
}
