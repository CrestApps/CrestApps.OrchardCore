using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// Verifies that the health probes produce a verdict on real tenants rather than an error.
/// </summary>
/// <remarks>
/// A health check registered by a feature that does not own its dependencies constructs only on tenants that
/// happen to enable the owning feature too. Selecting the checks in a unit test cannot catch that, because the
/// dependency is resolved when the check runs. These tests execute the real aggregates inside real shells with
/// different feature sets.
/// </remarks>
public sealed class ContactCenterHealthProbeActivationTests
{
    private static readonly string[] _baseOnlyFeatures =
    [
        "CrestApps.OrchardCore.ContactCenter",
    ];

    [Fact]
    public async Task DependencyProbe_ProducesAVerdict_OnATenantWithoutVoice()
    {
        // Arrange
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var profile = new ContactCenterTenantProfile
        {
            Id = "base-only",
            ProviderProfile = "asterisk-ga-core",
            Features = _baseOnlyFeatures,
        };

        var tenant = await host.CreateTenantAsync(profile);

        // Act
        var report = await host.ExecuteInTenantScopeAsync(
            tenant,
            services => services
                .GetRequiredService<HealthCheckService>()
                .CheckHealthAsync(
                    registration => registration.Tags.Contains(ContactCenterConstants.HealthChecks.DependencyTag),
                    TestContext.Current.CancellationToken));

        // Assert
        Assert.DoesNotContain(
            ContactCenterConstants.HealthChecks.ProviderIngressCheckName,
            report.Entries.Keys);

        Assert.All(
            report.Entries,
            entry => Assert.Null(entry.Value.Exception));
    }

    [Fact]
    public async Task DependencyProbe_IncludesTheProviderIngressCheck_OnATenantWithVoice()
    {
        // Arrange
        var matrix = await ContactCenterSupportMatrix.LoadAsync();
        var profile = matrix.TenantProfiles.Single(profile => profile.Id == "ga-core-asterisk");

        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(profile);

        // Act
        var report = await host.ExecuteInTenantScopeAsync(
            tenant,
            services => services
                .GetRequiredService<HealthCheckService>()
                .CheckHealthAsync(
                    registration => registration.Tags.Contains(ContactCenterConstants.HealthChecks.DependencyTag),
                    TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains(ContactCenterConstants.HealthChecks.ProviderIngressCheckName, report.Entries.Keys);
        Assert.Contains(ContactCenterConstants.HealthChecks.OutboxCheckName, report.Entries.Keys);
        Assert.Contains(ContactCenterConstants.HealthChecks.StorageCheckName, report.Entries.Keys);

        Assert.All(
            report.Entries,
            entry => Assert.Null(entry.Value.Exception));
    }

    [Fact]
    public async Task Readiness_IncludesTheBaseVoiceCheck_OnATenantWithVoice()
    {
        // The base-voice check is registered by the Voice feature and tagged for readiness, so it participates
        // only on a tenant that enables Voice. Selecting it in a unit test cannot prove it constructs, because
        // it resolves IHostEnvironment and its options from the tenant container; this runs the real readiness
        // aggregate on a real Voice shell. The default host reports the development environment, so an
        // unacknowledged deployment is tolerated and the verdict is healthy here rather than fail-closed.
        var matrix = await ContactCenterSupportMatrix.LoadAsync();
        var profile = matrix.TenantProfiles.Single(profile => profile.Id == "ga-core-asterisk");

        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(profile);

        var readiness = await host.ExecuteInTenantScopeAsync(
            tenant,
            services => services
                .GetRequiredService<HealthCheckService>()
                .CheckHealthAsync(
                    registration => registration.Tags.Contains(ContactCenterConstants.HealthChecks.ReadyTag),
                    TestContext.Current.CancellationToken));

        Assert.Contains(ContactCenterConstants.HealthChecks.BaseVoiceVerificationCheckName, readiness.Entries.Keys);

        var baseVoiceEntry = readiness.Entries[ContactCenterConstants.HealthChecks.BaseVoiceVerificationCheckName];

        Assert.Null(baseVoiceEntry.Exception);
        Assert.Equal(HealthStatus.Healthy, baseVoiceEntry.Status);
        Assert.Equal(HealthStatus.Healthy, readiness.Status);
    }

    [Fact]
    public async Task Readiness_StaysHealthy_WhenADependencyCheckIsUnhealthy()
    {
        // The fleet-wide-drain regression, asserted on a real shell. Every node observes the same shared
        // dependency, so a readiness probe that consulted one would evict every node at the same moment.
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var profile = new ContactCenterTenantProfile
        {
            Id = "base-only",
            ProviderProfile = "asterisk-ga-core",
            Features = _baseOnlyFeatures,
        };

        var tenant = await host.CreateTenantAsync(profile);

        var readiness = await host.ExecuteInTenantScopeAsync(
            tenant,
            services => services
                .GetRequiredService<HealthCheckService>()
                .CheckHealthAsync(
                    registration => registration.Tags.Contains(ContactCenterConstants.HealthChecks.ReadyTag),
                    TestContext.Current.CancellationToken));

        Assert.Equal(
            [
                ContactCenterConstants.HealthChecks.TopologyCheckName,
                ContactCenterConstants.HealthChecks.NodeServingCheckName,
                ContactCenterConstants.HealthChecks.NodeCheckName,
            ],
            readiness.Entries.Keys);

        // The structural invariant behind the regression: no check that observes a shared dependency may
        // participate in readiness. Pinning the key set alone would still pass if a dependency check were
        // renamed into the list, so the tags are asserted independently.
        var dependencyChecks = await host.ExecuteInTenantScopeAsync(
            tenant,
            services => services
                .GetRequiredService<HealthCheckService>()
                .CheckHealthAsync(
                    registration => registration.Tags.Contains(ContactCenterConstants.HealthChecks.DependencyTag),
                    TestContext.Current.CancellationToken));

        Assert.NotEmpty(dependencyChecks.Entries);

        foreach (var dependencyCheck in dependencyChecks.Entries.Keys)
        {
            Assert.DoesNotContain(dependencyCheck, readiness.Entries.Keys);
        }

        // The test is named for staying healthy, so it has to assert it rather than only assert which
        // checks ran.
        Assert.Equal(HealthStatus.Healthy, readiness.Status);
    }

    [Fact]
    public async Task TenantProbes_AreServedUnderTheTenantRequestUrlPrefix()
    {
        // The tenant probes are mapped inside the tenant shell, so their physical URL includes the tenant
        // prefix. An operator that probes the unprefixed route reaches the default shell instead, which does
        // not enable Contact Center and therefore answers 404.
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var profile = new ContactCenterTenantProfile
        {
            Id = "base-only",
            ProviderProfile = "asterisk-ga-core",
            Features = _baseOnlyFeatures,
        };

        var tenant = await host.CreateTenantAsync(profile);

        using var client = new HttpClient { BaseAddress = host.BaseAddress };

        var prefix = tenant.Settings.RequestUrlPrefix;

        var prefixedReadiness = await client.GetAsync(
            $"{prefix}/{ContactCenterConstants.HealthChecks.ReadinessRoute}",
            TestContext.Current.CancellationToken);

        var unprefixedReadiness = await client.GetAsync(
            ContactCenterConstants.HealthChecks.ReadinessRoute,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, prefixedReadiness.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unprefixedReadiness.StatusCode);
    }

    [Fact]
    public async Task ProcessLiveness_AnswersWithoutATenantPrefix()
    {
        // Liveness must not be tenant-scoped. This is the regression for the failure it prevents: a probe on
        // the unprefixed path must return 200 even though Contact Center runs only on a prefixed tenant. A
        // tenant-mapped liveness route answers 404 here, and an orchestrator restarts a healthy process for a
        // tenant-level problem, forever.
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var profile = new ContactCenterTenantProfile
        {
            Id = "base-only",
            ProviderProfile = "asterisk-ga-core",
            Features = _baseOnlyFeatures,
        };

        var tenant = await host.CreateTenantAsync(profile);

        using var client = new HttpClient { BaseAddress = host.BaseAddress };

        var liveness = await client.GetAsync(
            ContactCenterConstants.HealthChecks.ProcessLivenessPath,
            TestContext.Current.CancellationToken);

        // The Orchard pipeline never sees the request, so the prefixed variant is a normal tenant 404.
        var prefixedLiveness = await client.GetAsync(
            $"{tenant.Settings.RequestUrlPrefix}{ContactCenterConstants.HealthChecks.ProcessLivenessPath}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);
        Assert.Equal("Healthy", await liveness.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.NotEqual(HttpStatusCode.OK, prefixedLiveness.StatusCode);
    }

    [Fact]
    public async Task ProcessLiveness_DoesNotShadowTheSharedHealthEndpoint_OnATenantWithoutContactCenter()
    {
        // The regression for the worst failure the host middleware could cause. It short-circuits before
        // routing, so if it took /health/live it would silently replace the OrchardCore.HealthChecks module's
        // endpoint with an unconditional 200 Healthy for every tenant in the process — including this one,
        // which never enables Contact Center and cannot be protected by the Contact Center startup guard.
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var profile = new ContactCenterTenantProfile
        {
            Id = "no-contact-center",
            ProviderProfile = "asterisk-ga-core",
            Features = ["OrchardCore.HealthChecks"],
        };

        var tenant = await host.CreateTenantAsync(profile);

        using var client = new HttpClient { BaseAddress = host.BaseAddress };

        var prefix = tenant.Settings.RequestUrlPrefix;

        // The shared endpoint is mapped inside the shell, so it answers under the tenant prefix.
        var prefixedShared = await client.GetAsync(
            $"{prefix}/health/live",
            TestContext.Current.CancellationToken);

        // The discriminator: host middleware short-circuits before routing, so a shadowing implementation
        // answers 200 on the *unprefixed* path too, where no shell maps anything. A correct implementation
        // lets that request fall through to the default shell, which does not enable the module.
        var unprefixedShared = await client.GetAsync(
            "/health/live",
            TestContext.Current.CancellationToken);

        var liveness = await client.GetAsync(
            ContactCenterConstants.HealthChecks.ProcessLivenessPath,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, prefixedShared.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, unprefixedShared.StatusCode);
        Assert.Equal(HttpStatusCode.OK, liveness.StatusCode);
        Assert.NotEqual("/health/live", ContactCenterConstants.HealthChecks.ProcessLivenessPath);
    }

    [Fact]
    public async Task DependencyProbe_RequiresAuthorization()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var profile = new ContactCenterTenantProfile
        {
            Id = "base-only",
            ProviderProfile = "asterisk-ga-core",
            Features = _baseOnlyFeatures,
        };

        var tenant = await host.CreateTenantAsync(profile);

        using var client = new HttpClient { BaseAddress = host.BaseAddress };

        var response = await client.GetAsync(
            $"{tenant.Settings.RequestUrlPrefix}/{ContactCenterConstants.HealthChecks.DependenciesRoute}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
