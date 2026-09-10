using System.Net;
using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Redis;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Verifies that the readiness and dependency probes select and execute the intended health checks.
/// </summary>
/// <remarks>
/// The failures these tests exist to catch are silent and severe.
/// <list type="bullet">
/// <item><description>If a registration tag and a probe predicate drift apart, the probe selects nothing and
/// reports healthy with nothing checked.</description></item>
/// <item><description>If liveness were wired to a dependency, a degraded dependency would restart healthy
/// nodes.</description></item>
/// <item><description>If readiness were wired to a dependency, every node would fail readiness at the same
/// moment, because a shared dependency evaluates identically everywhere. That converts a degraded dependency
/// into a total outage, so readiness must select node-local state and static support verdicts only, never a
/// live dependency probe.</description></item>
/// </list>
/// Both the registrations and, for behavior, real HTTP responses are asserted.
/// </remarks>
public sealed class ContactCenterHealthEndpointsTests
{
    [Fact]
    public void ReadinessPredicate_SelectsOnlyNodeLocalChecksAndTheStaticSupportVerdicts()
    {
        // Arrange
        var registrations = GetRegisteredHealthChecks(services => services
            .AddContactCenterHealthChecks()
            .AddContactCenterVoiceHealthChecks());

        // Act
        var selected = SelectNames(registrations, ContactCenterHealthEndpoints.IsReadinessCheck);

        // Assert
        // Two are node-local: one reflects this node's lifetime, the other this node's own ability to reach the
        // store. Neither reports a verdict that every node would share.
        //
        // The topology and base-voice verification checks are the two deliberate exceptions, and the distinction
        // is between a live dependency and a static verdict. A dependency probe is transient and self-healing, so
        // draining every node on it turns a recoverable blip into a total outage. A topology violation, or an
        // unverified base-voice media path, is fixed configuration that no amount of waiting repairs, and
        // continuing to serve on such a deployment is the exact failure being prevented, so draining is the
        // intended outcome rather than collateral damage. The narrower invariant that survives is asserted below:
        // readiness never selects a dependency check.
        Assert.Equal(
            [
                ContactCenterConstants.HealthChecks.BaseVoiceVerificationCheckName,
                ContactCenterConstants.HealthChecks.NodeCheckName,
                ContactCenterConstants.HealthChecks.NodeServingCheckName,
                ContactCenterConstants.HealthChecks.TopologyCheckName,
            ],
            selected);
    }

    [Fact]
    public void ReadinessPredicate_SelectsNoDependencyCheck_SoASharedOutageCannotDrainTheFleet()
    {
        // Arrange
        var registrations = GetRegisteredHealthChecks(services => services
            .AddContactCenterHealthChecks()
            .AddContactCenterVoiceHealthChecks());

        // Act
        var selected = registrations
            .Where(ContactCenterHealthEndpoints.IsReadinessCheck)
            .ToArray();

        // Assert
        Assert.All(
            selected,
            registration => Assert.DoesNotContain(
                ContactCenterConstants.HealthChecks.DependencyTag,
                registration.Tags));
    }

    [Fact]
    public void DependencyPredicate_SelectsEveryDependencyCheckTheBaseFeatureRegisters()
    {
        // Arrange
        var registrations = GetRegisteredHealthChecks(services => services.AddContactCenterHealthChecks());

        // Act
        var selected = SelectNames(registrations, ContactCenterHealthEndpoints.IsDependencyCheck);

        // Assert
        Assert.Equal(
            [
                ContactCenterConstants.HealthChecks.ActiveCallsCheckName,
                ContactCenterConstants.HealthChecks.OutboxCheckName,
                ContactCenterConstants.HealthChecks.StorageCheckName,
            ],
            selected);
    }

    [Fact]
    public void RedisHealthChecks_RegisterTheThreeDistributedDependencyProbes_AllTaggedDependency()
    {
        // Arrange
        var registrations = GetRegisteredHealthChecks(services =>
        {
            services.AddSingleton(Mock.Of<IRedisService>());
            services.AddContactCenterRedisHealthChecks();
        });

        // Act
        var selected = SelectNames(registrations, ContactCenterHealthEndpoints.IsDependencyCheck);

        // Assert
        // These three probe services only OrchardCore.Redis registers, so they are gated behind that feature and
        // never appear in the base-feature set. Each carries the dependency tag so a shared Redis outage alerts
        // without draining the fleet through readiness.
        Assert.Equal(
            [
                ContactCenterConstants.HealthChecks.BackplaneCheckName,
                ContactCenterConstants.HealthChecks.DistributedLockCheckName,
                ContactCenterConstants.HealthChecks.RedisConnectivityCheckName,
            ],
            selected);

        Assert.All(registrations, registration =>
        {
            Assert.Contains(ContactCenterConstants.HealthChecks.AreaTag, registration.Tags);
            Assert.Contains(ContactCenterConstants.HealthChecks.DependencyTag, registration.Tags);
            Assert.DoesNotContain(ContactCenterConstants.HealthChecks.ReadyTag, registration.Tags);
        });
    }

    [Fact]
    public void QueuesHealthChecks_RegisterTheQueueBacklogProbe_TaggedDependency()
    {
        // Arrange
        var registrations = GetRegisteredHealthChecks(services => services.AddContactCenterQueuesHealthChecks());

        // Act
        var selected = SelectNames(registrations, ContactCenterHealthEndpoints.IsDependencyCheck);

        // Assert
        // The queue-backlog gauge reads the queue item store only the Queues feature registers, so it is owned by
        // that feature and never appears in the base-feature set. It carries the dependency tag so it surfaces as
        // an alerting gauge and never gates readiness.
        Assert.Equal(
            [
                ContactCenterConstants.HealthChecks.QueueBacklogCheckName,
            ],
            selected);

        Assert.All(registrations, registration =>
        {
            Assert.Contains(ContactCenterConstants.HealthChecks.AreaTag, registration.Tags);
            Assert.Contains(ContactCenterConstants.HealthChecks.DependencyTag, registration.Tags);
            Assert.DoesNotContain(ContactCenterConstants.HealthChecks.ReadyTag, registration.Tags);
        });
    }

    [Fact]
    public void RedisHealthChecks_RegisterNothing_WhenRedisServiceIsNotRegistered()
    {
        // Enabling OrchardCore.Redis is not enough: Orchard skips registering IRedisService when the Redis
        // configuration string is missing or invalid. Registering the probes anyway would make the dependency
        // endpoint throw while constructing a check whose mandatory dependency cannot be resolved, so the probes
        // must not register until IRedisService is present. Returning before AddHealthChecks means no health-check
        // infrastructure is added at all.
        var services = new ServiceCollection();

        services.AddContactCenterRedisHealthChecks();

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(HealthCheckService));
    }

    [Fact]
    public void BaseFeature_DoesNotRegisterAnyRedisDependentCheck()
    {
        // The distributed lock, Redis connectivity, and backplane probes take a mandatory Redis-owned
        // dependency. Registering them in the base feature would make the dependency probe throw on a tenant
        // that enables Contact Center without Redis.
        var registrations = GetRegisteredHealthChecks(services => services
            .AddContactCenterHealthChecks()
            .AddContactCenterVoiceHealthChecks());

        string[] redisDependentNames =
        [
            ContactCenterConstants.HealthChecks.DistributedLockCheckName,
            ContactCenterConstants.HealthChecks.RedisConnectivityCheckName,
            ContactCenterConstants.HealthChecks.BackplaneCheckName,
        ];

        Assert.DoesNotContain(
            registrations,
            registration => redisDependentNames.Contains(registration.Name));
    }

    [Fact]
    public void DependencyPredicate_SelectsTheVoiceCheck_WhenTheVoiceFeatureIsEnabled()
    {
        // Arrange
        var registrations = GetRegisteredHealthChecks(services => services
            .AddContactCenterHealthChecks()
            .AddContactCenterVoiceHealthChecks());

        // Act
        var selected = SelectNames(registrations, ContactCenterHealthEndpoints.IsDependencyCheck);

        // Assert
        Assert.Equal(
            [
                ContactCenterConstants.HealthChecks.ActiveCallsCheckName,
                ContactCenterConstants.HealthChecks.OutboxCheckName,
                ContactCenterConstants.HealthChecks.ProviderIngressCheckName,
                ContactCenterConstants.HealthChecks.StorageCheckName,
            ],
            selected);
    }

    [Fact]
    public void BaseFeature_DoesNotRegisterTheProviderIngressCheck()
    {
        // The provider ingress check reads a store only the Voice feature registers. Registering it in the
        // base feature makes the dependency probe throw on a tenant that enables Contact Center without Voice.
        var registrations = GetRegisteredHealthChecks(services => services.AddContactCenterHealthChecks());

        Assert.DoesNotContain(
            registrations,
            registration => registration.Name == ContactCenterConstants.HealthChecks.ProviderIngressCheckName);
    }

    [Fact]
    public void ReadinessPredicate_SelectsNothing_WhenNoCheckIsTaggedReady()
    {
        // Arrange
        var registration = new HealthCheckRegistration(
            "unrelated-module-check",
            _ => new StubHealthCheck(HealthCheckResult.Healthy()),
            HealthStatus.Unhealthy,
            tags: ["someothermodule"]);

        // Act
        var isSelected = ContactCenterHealthEndpoints.IsReadinessCheck(registration);

        // Assert
        Assert.False(isSelected);
    }

    [Fact]
    public void ReadinessPredicate_IgnoresAForeignCheckTaggedWithTheConventionalReadyTag()
    {
        // The bare "ready" tag is the ASP.NET Core convention, so another module may well contribute a Redis
        // or database check carrying it. Such a check is shared by every node: if it joined this readiness
        // verdict, one degraded dependency would drain the whole fleet. Namespacing the tag is what prevents
        // that from happening silently.
        var registration = new HealthCheckRegistration(
            "foreign-redis-check",
            _ => new StubHealthCheck(HealthCheckResult.Unhealthy("redis is unreachable")),
            HealthStatus.Unhealthy,
            tags: ["ready"]);

        // Act
        var isSelected = ContactCenterHealthEndpoints.IsReadinessCheck(registration);

        // Assert
        Assert.False(isSelected);
        Assert.NotEqual("ready", ContactCenterConstants.HealthChecks.ReadyTag);
    }

    [Fact]
    public void EveryRegisteredCheck_CarriesTheAreaTagAndExactlyOneProbeTag()
    {
        // Arrange
        var registrations = GetRegisteredHealthChecks(services => services
            .AddContactCenterHealthChecks()
            .AddContactCenterVoiceHealthChecks());

        // Act & Assert
        Assert.Equal(8, registrations.Length);

        Assert.All(registrations, registration =>
        {
            Assert.Contains(ContactCenterConstants.HealthChecks.AreaTag, registration.Tags);

            var isReady = registration.Tags.Contains(ContactCenterConstants.HealthChecks.ReadyTag);
            var isDependency = registration.Tags.Contains(ContactCenterConstants.HealthChecks.DependencyTag);

            Assert.True(isReady ^ isDependency, registration.Name);
        });
    }

    [Fact]
    public void AddContactCenterHealthEndpoints_MapsAllThreeProbes()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddContactCenterHealthChecks();

        using var application = builder.Build();

        // Act
        application.AddContactCenterHealthEndpoints();

        var routes = GetMappedEndpoints(application)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        // Assert
        // Liveness is deliberately absent: a tenant-scoped route cannot answer "restart this process".
        Assert.DoesNotContain(ContactCenterConstants.HealthChecks.ProcessLivenessPath.TrimStart('/'), routes);
        Assert.Equal(
            [
                ContactCenterConstants.HealthChecks.DependenciesRoute,
                ContactCenterConstants.HealthChecks.ReadinessRoute,
            ],
            routes.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AddContactCenterHealthEndpoints_AllowsAnonymousOrchestratorProbesOnly()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddContactCenterHealthChecks();

        using var application = builder.Build();

        // Act
        application.AddContactCenterHealthEndpoints();

        var endpoints = GetMappedEndpoints(application);

        var anonymous = endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Assert
        // The dependency route discloses per-check detail, so it must never be in this set.
        Assert.Equal([ContactCenterConstants.HealthChecks.ReadinessRoute], anonymous);
    }

    [Fact]
    public async Task Readiness_ReportsHealthy_WhileADependencyIsFailing()
    {
        // This is the fleet-wide-drain regression, and it is the most damaging of the three. A shared
        // dependency reports the same verdict on every node, so a readiness probe that consults one removes
        // every node from the load balancer at the same moment.
        await using var probe = await ProbeHost.StartAsync(
            dependencyResult: HealthCheckResult.Unhealthy("primary database is unreachable"));

        var response = await probe.GetAsync(ContactCenterConstants.HealthChecks.ReadinessRoute, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HealthStatus.Healthy.ToString(), body);
    }

    [Fact]
    public async Task Readiness_ReportsUnhealthy_WhenTheNodeLocalCheckFails()
    {
        await using var probe = await ProbeHost.StartAsync(
            dependencyResult: HealthCheckResult.Healthy(),
            nodeResult: HealthCheckResult.Unhealthy("this node is shutting down and should be drained"));

        var response = await probe.GetAsync(ContactCenterConstants.HealthChecks.ReadinessRoute, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(HealthStatus.Unhealthy.ToString(), body);
    }

    [Fact]
    public async Task Readiness_ReportsHealthy_WhenTheNodeLocalCheckPasses()
    {
        await using var probe = await ProbeHost.StartAsync(dependencyResult: HealthCheckResult.Healthy());

        var response = await probe.GetAsync(ContactCenterConstants.HealthChecks.ReadinessRoute, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HealthStatus.Healthy.ToString(), body);
    }

    [Fact]
    public async Task Readiness_IgnoresChecksThatAreNotTaggedReady()
    {
        // An untagged check that would fail must not influence readiness, otherwise another module's
        // unrelated dependency could evict this node from the load balancer.
        await using var probe = await ProbeHost.StartAsync(
            dependencyResult: HealthCheckResult.Healthy(),
            untaggedResult: HealthCheckResult.Unhealthy("unrelated module is down"));

        var response = await probe.GetAsync(ContactCenterConstants.HealthChecks.ReadinessRoute, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HealthStatus.Healthy.ToString(), body);
    }

    [Fact]
    public async Task Probes_AreNotCacheable()
    {
        await using var probe = await ProbeHost.StartAsync(dependencyResult: HealthCheckResult.Healthy());

        string[] routes = [ContactCenterConstants.HealthChecks.ReadinessRoute];

        foreach (var route in routes)
        {
            var response = await probe.GetAsync(route, TestContext.Current.CancellationToken);

            Assert.True(response.Headers.CacheControl.NoStore, route);
            Assert.True(response.Headers.CacheControl.NoCache, route);
        }
    }

    [Fact]
    public async Task Readiness_DoesNotDiscloseWhichCheckFailed()
    {
        await using var probe = await ProbeHost.StartAsync(
            dependencyResult: HealthCheckResult.Healthy(),
            nodeResult: HealthCheckResult.Unhealthy("rejected by node sql-primary-eastus"));

        var response = await probe.GetAsync(ContactCenterConstants.HealthChecks.ReadinessRoute, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("sql-primary-eastus", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HealthStatus.Unhealthy.ToString(), body);
    }

    [Fact]
    public async Task Dependencies_Returns401_ForAnAnonymousCaller()
    {
        await using var probe = await ProbeHost.StartAsync(
            dependencyResult: HealthCheckResult.Unhealthy("primary database is unreachable"),
            authorize: false);

        var response = await probe.GetAsync(ContactCenterConstants.HealthChecks.DependenciesRoute, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(body);
    }

    [Fact]
    public async Task Dependencies_ReportsPerCheckDetail_ForAnAuthorizedCaller()
    {
        // The dependency probe exists so an operator can see which dependency degraded, which is exactly the
        // detail the anonymous probes must never disclose.
        await using var probe = await ProbeHost.StartAsync(
            dependencyResult: HealthCheckResult.Degraded("outbox backlog is above the degraded threshold"),
            authorize: true);

        var response = await probe.GetAsync(ContactCenterConstants.HealthChecks.DependenciesRoute, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("probe-dependency-check", body, StringComparison.Ordinal);
        Assert.Contains("outbox backlog is above the degraded threshold", body, StringComparison.Ordinal);
        Assert.Contains(HealthStatus.Degraded.ToString(), body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dependencies_DoesNotReportTheNodeLocalCheck()
    {
        await using var probe = await ProbeHost.StartAsync(
            dependencyResult: HealthCheckResult.Healthy(),
            authorize: true);

        var response = await probe.GetAsync(ContactCenterConstants.HealthChecks.DependenciesRoute, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("probe-node-check", body, StringComparison.Ordinal);
    }

    private const string SecretValue = "hunter2";

    private const string SecretBearingMessage = "Server=sql-primary-eastus;Pass" + "word=" + SecretValue;

    [Fact]
    public async Task Dependencies_NeverDisclosesExceptionDetail()
    {
        // A dependency failure exception can carry a connection string, so only the authored description is
        // written even for an authorized caller.
        await using var probe = await ProbeHost.StartAsync(
            dependencyResult: new HealthCheckResult(
                HealthStatus.Unhealthy,
                "Contact Center storage is unreachable.",
                new InvalidOperationException(SecretBearingMessage)),
            authorize: true);

        var response = await probe.GetAsync(ContactCenterConstants.HealthChecks.DependenciesRoute, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(SecretBearingMessage, body, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretValue, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sql-primary-eastus", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(InvalidOperationException), body, StringComparison.Ordinal);

        // Asserting the authored description is still reported proves the assertions above measure redaction
        // rather than an empty response.
        Assert.Contains("Contact Center storage is unreachable.", body, StringComparison.Ordinal);
    }

    private static string[] SelectNames(
        HealthCheckRegistration[] registrations,
        Func<HealthCheckRegistration, bool> predicate)
        => registrations
            .Where(predicate)
            .Select(registration => registration.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static RouteEndpoint[] GetMappedEndpoints(IEndpointRouteBuilder builder)
        => builder.DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

    private static HealthCheckRegistration[] GetRegisteredHealthChecks(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();

        register(services);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations.ToArray();
    }

    private sealed class StubHealthCheck : IHealthCheck
    {
        private readonly HealthCheckResult _result;

        public StubHealthCheck(HealthCheckResult result)
        {
            _result = result;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private sealed class StubAuthorizationService : IAuthorizationService
    {
        private readonly bool _succeed;

        public StubAuthorizationService(bool succeed)
        {
            _succeed = succeed;
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(_succeed ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(_succeed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
    }

    /// <summary>
    /// Hosts the probes over real HTTP so the assertions cover the mapped predicates and the framework's
    /// status-code and response mapping rather than only the delegates in isolation.
    /// </summary>
    private sealed class ProbeHost : IAsyncDisposable
    {
        private readonly WebApplication _application;
        private readonly HttpClient _client;

        private ProbeHost(WebApplication application, HttpClient client)
        {
            _application = application;
            _client = client;
        }

        public static async Task<ProbeHost> StartAsync(
            HealthCheckResult dependencyResult,
            HealthCheckResult? nodeResult = null,
            HealthCheckResult? untaggedResult = null,
            bool authorize = false)
        {
            var builder = WebApplication.CreateBuilder();

            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton<IAuthorizationService>(new StubAuthorizationService(authorize));

            var checks = builder.Services
                .AddHealthChecks()
                .AddCheck(
                    "probe-node-check",
                    new StubHealthCheck(nodeResult ?? HealthCheckResult.Healthy()),
                    failureStatus: HealthStatus.Unhealthy,
                    tags: [ContactCenterConstants.HealthChecks.ReadyTag])
                .AddCheck(
                    "probe-dependency-check",
                    new StubHealthCheck(dependencyResult),
                    failureStatus: HealthStatus.Unhealthy,
                    tags: [ContactCenterConstants.HealthChecks.DependencyTag]);

            if (untaggedResult.HasValue)
            {
                checks.AddCheck("probe-untagged-check", new StubHealthCheck(untaggedResult.Value));
            }

            var application = builder.Build();

            application.AddContactCenterHealthEndpoints();

            await application.StartAsync();

            var address = application.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()
                .Addresses
                .First();

            return new ProbeHost(application, new HttpClient { BaseAddress = new Uri(address) });
        }

        public Task<HttpResponseMessage> GetAsync(string route, CancellationToken cancellationToken)
            => _client.GetAsync(route, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();

            await _application.StopAsync();
            await _application.DisposeAsync();
        }
    }
}
