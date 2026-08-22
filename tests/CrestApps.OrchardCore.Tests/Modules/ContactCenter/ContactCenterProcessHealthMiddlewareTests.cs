using System.Net;
using CrestApps.OrchardCore.ContactCenter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Verifies the host-level process liveness probe.
/// </summary>
/// <remarks>
/// Liveness answers "should this process be restarted". It must therefore be answered by the process itself,
/// ahead of any tenant routing: a tenant-scoped probe returns 404 whenever the tenant is disabled, renamed, or
/// given a different request URL prefix, and an orchestrator reads 404 as a probe failure. These tests pin the
/// two properties that make the middleware safe — it answers before anything downstream runs, and it answers
/// only the probe's own path and verbs.
/// </remarks>
public sealed class ContactCenterProcessHealthMiddlewareTests
{
    [Fact]
    public async Task Liveness_Answers_BeforeTheRestOfThePipelineRuns()
    {
        // The regression: if the probe were a mapped endpoint rather than short-circuiting middleware, the
        // downstream pipeline would run first and could answer 404 or redirect the probe.
        await using var host = await ProbePipeline.StartAsync();

        var response = await host.GetAsync(
            ContactCenterConstants.HealthChecks.ProcessLivenessPath,
            TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
        Assert.False(host.DownstreamWasInvoked);
    }

    [Fact]
    public async Task Liveness_IsNotCacheable()
    {
        await using var host = await ProbePipeline.StartAsync();

        var response = await host.GetAsync(
            ContactCenterConstants.HealthChecks.ProcessLivenessPath,
            TestContext.Current.CancellationToken);

        Assert.True(response.Headers.CacheControl.NoStore);
        Assert.True(response.Headers.CacheControl.NoCache);
    }

    [Fact]
    public async Task Liveness_AnswersHeadRequests()
    {
        // Orchestrators and load balancers commonly probe with HEAD.
        await using var host = await ProbePipeline.StartAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Head,
            ContactCenterConstants.HealthChecks.ProcessLivenessPath);

        var response = await host.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(host.DownstreamWasInvoked);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Liveness_DoesNotAnswerMutatingVerbs(string method)
    {
        // Answering every verb would turn the path into a silent 200 for requests an operator would expect to
        // be rejected, and would mask a routing mistake.
        await using var host = await ProbePipeline.StartAsync();

        using var request = new HttpRequestMessage(
            new HttpMethod(method),
            ContactCenterConstants.HealthChecks.ProcessLivenessPath);

        await host.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.True(host.DownstreamWasInvoked);
    }

    [Fact]
    public async Task Liveness_PassesThroughUnrelatedPaths()
    {
        await using var host = await ProbePipeline.StartAsync();

        var response = await host.GetAsync("/some/other/path", TestContext.Current.CancellationToken);

        Assert.True(host.DownstreamWasInvoked);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Liveness_MatchesThePathCaseInsensitively()
    {
        await using var host = await ProbePipeline.StartAsync();

        var response = await host.GetAsync("/Health/PROCESS", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(host.DownstreamWasInvoked);
    }

    [Fact]
    public async Task Liveness_DoesNotAnswerAPrefixedVariant()
    {
        // A tenant-prefixed request is a different path and must reach the normal pipeline, otherwise the
        // probe would shadow a tenant route.
        await using var host = await ProbePipeline.StartAsync();

        await host.GetAsync(
            $"/support{ContactCenterConstants.HealthChecks.ProcessLivenessPath}",
            TestContext.Current.CancellationToken);

        Assert.True(host.DownstreamWasInvoked);
    }

    [Fact]
    public async Task Liveness_HonorsACustomPath()
    {
        await using var host = await ProbePipeline.StartAsync("/probe/alive");

        var custom = await host.GetAsync("/probe/alive", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, custom.StatusCode);
        Assert.False(host.DownstreamWasInvoked);
    }

    [Fact]
    public async Task Liveness_DoesNotAnswerTheDefaultPath_WhenACustomPathIsConfigured()
    {
        await using var host = await ProbePipeline.StartAsync("/probe/alive");

        await host.GetAsync(
            ContactCenterConstants.HealthChecks.ProcessLivenessPath,
            TestContext.Current.CancellationToken);

        Assert.True(host.DownstreamWasInvoked);
    }

    [Fact]
    public async Task Liveness_DoesNotShadowTheOrchardHealthChecksDefaultRoute()
    {
        // The regression for the worst failure this middleware could cause. It short-circuits before routing,
        // so taking /health/live would silently replace the OrchardCore.HealthChecks module's endpoint with an
        // unconditional 200 Healthy — for every tenant in the process, including tenants that never enable
        // Contact Center. A health endpoint that can only report success is worse than none at all.
        await using var host = await ProbePipeline.StartAsync();

        await host.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.True(host.DownstreamWasInvoked);
        Assert.NotEqual("/health/live", ContactCenterConstants.HealthChecks.ProcessLivenessPath);
    }

    [Fact]
    public void UseContactCenterProcessLiveness_FailsFast_WhenThePathCollidesWithTheSharedHealthEndpoint()
    {
        // An operator can still create the collision explicitly. It must fail startup, not shadow silently.
        var exception = Assert.Throws<InvalidOperationException>(
            () => ContactCenterProcessHealthApplicationBuilderExtensions.ThrowIfShadowsSharedHealthEndpoint(
                "/health/aggregate",
                "/health/aggregate",
                tenantName: null));

        Assert.Contains("shadow", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/health/live", null)]
    [InlineData("/health/live", "")]
    [InlineData("health/live", "/health/live")]
    [InlineData("/Health/Live", "/health/live")]
    public void CollisionDetection_TreatsTheOrchardDefaultAndSlashAndCaseVariantsAsTheSameRoute(
        string livenessPath,
        string sharedRoute)
    {
        // An unset shared route means the module uses its default, so the collision must still be detected.
        Assert.Throws<InvalidOperationException>(
            () => ContactCenterProcessHealthApplicationBuilderExtensions.ThrowIfShadowsSharedHealthEndpoint(
                livenessPath,
                sharedRoute,
                tenantName: null));
    }

    [Fact]
    public void CollisionDetection_NamesTheTenant_WhenTheRouteCameFromTenantConfiguration()
    {
        // Tenant configuration is not host configuration. An operator needs to know which tenant to fix.
        var exception = Assert.Throws<InvalidOperationException>(
            () => ContactCenterProcessHealthApplicationBuilderExtensions.ThrowIfShadowsSharedHealthEndpoint(
                "/health/process",
                "/health/process",
                tenantName: "Default"));

        Assert.Contains("Default", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/health/process", null)]
    [InlineData("/health/process", "/health/live")]
    [InlineData("/health/live", "/health/aggregate")]
    public void CollisionDetection_AllowsDistinctRoutes(string livenessPath, string sharedRoute)
    {
        ContactCenterProcessHealthApplicationBuilderExtensions.ThrowIfShadowsSharedHealthEndpoint(
            livenessPath,
            sharedRoute,
            tenantName: null);
    }

    [Fact]
    public void UseContactCenterProcessLiveness_FailsFast_WhenTheServicesWereNotRegistered()
    {
        // Without the registration the tenant-configuration validator never runs, so the parameterless
        // overload must not silently fall back to the default path.
        var builder = WebApplication.CreateBuilder();

        using var application = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => application.UseContactCenterProcessLiveness());

        Assert.Contains("AddContactCenterProcessLiveness", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddContactCenterProcessLiveness_RejectsAnEmptyPath()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddContactCenterProcessLiveness("  "));
    }

    [Fact]
    public void AddContactCenterProcessLiveness_FailsFast_WhenRegisteredTwiceOnDifferentPaths()
    {
        // The middleware answers on exactly one path, and only the last registration would take effect. The
        // earlier path would still be the one the tenant validator had run against, so the probe could answer
        // on a path no tenant was ever checked for.
        var services = new ServiceCollection();

        services.AddContactCenterProcessLiveness("/probe/alive");

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddContactCenterProcessLiveness("/probe/other"));

        Assert.Contains("/probe/alive", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddContactCenterProcessLiveness_IsIdempotent_WhenRegisteredTwiceOnTheSamePath()
    {
        var services = new ServiceCollection();

        services.AddContactCenterProcessLiveness("/probe/alive");
        services.AddContactCenterProcessLiveness("/probe/alive");

        Assert.Single(services, descriptor
            => descriptor.ServiceType == typeof(ContactCenterProcessLivenessOptions));
    }

    /// <summary>
    /// Hosts the liveness middleware on a real Kestrel server with a downstream terminal middleware, so the
    /// tests observe the actual pipeline ordering rather than a simulation of it.
    /// </summary>
    private sealed class ProbePipeline : IAsyncDisposable
    {
        private WebApplication _application;
        private HttpClient _client;

        public bool DownstreamWasInvoked { get; private set; }

        public static async Task<ProbePipeline> StartAsync(string path = null)
        {
            var host = new ProbePipeline();

            var builder = WebApplication.CreateBuilder();

            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            if (path is null)
            {
                builder.Services.AddContactCenterProcessLiveness();
            }
            else
            {
                builder.Services.AddContactCenterProcessLiveness(path);
            }

            var application = builder.Build();

            application.UseContactCenterProcessLiveness();

            application.Run(async context =>
            {
                host.DownstreamWasInvoked = true;
                context.Response.StatusCode = StatusCodes.Status202Accepted;

                await context.Response.WriteAsync("downstream", context.RequestAborted);
            });

            await application.StartAsync(TestContext.Current.CancellationToken);

            var address = application.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()
                .Addresses
                .First();

            host._application = application;
            host._client = new HttpClient { BaseAddress = new Uri(address) };

            return host;
        }

        public Task<HttpResponseMessage> GetAsync(string path, CancellationToken cancellationToken)
            => _client.GetAsync(path, cancellationToken);

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _client.SendAsync(request, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            _client?.Dispose();

            if (_application is not null)
            {
                await _application.StopAsync(CancellationToken.None);
                await _application.DisposeAsync();
            }
        }
    }
}
