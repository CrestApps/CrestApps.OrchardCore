using CrestApps.OrchardCore.ContactCenter;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Verifies that a tenant cannot silently shadow the host process liveness probe.
/// </summary>
/// <remarks>
/// Tenant configuration is not host configuration, so the check performed when the middleware is added cannot
/// see a route configured inside a shell. Without this validator a tenant could map the shared health-check
/// endpoint on the reserved path, and rather than a routing error the operator would get a health endpoint that
/// answers an unconditional success forever.
/// </remarks>
public sealed class ContactCenterProcessLivenessPathValidatorTests
{
    [Fact]
    public async Task Startup_Fails_WhenATenantMapsTheSharedEndpointOnTheReservedPath()
    {
        var colliding = new ShellSettings { Name = "Default" };

        colliding["OrchardCore_HealthChecks:Url"] = ContactCenterConstants.HealthChecks.ProcessLivenessPath;

        var validator = CreateValidator(colliding);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Default", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_Fails_WhenTheCollidingTenantIsNotTheFirstOne()
    {
        var first = new ShellSettings { Name = "Default" };
        var second = new ShellSettings { Name = "Support" };

        second["OrchardCore_HealthChecks:Url"] = ContactCenterConstants.HealthChecks.ProcessLivenessPath;

        var validator = CreateValidator(first, second);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Support", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_Succeeds_WhenTenantsUseTheDefaultSharedEndpointRoute()
    {
        // The reserved path deliberately differs from the module's default, so the common case must not fail.
        var first = new ShellSettings { Name = "Default" };
        var second = new ShellSettings { Name = "Support" };

        second["OrchardCore_HealthChecks:Url"] = "/health/live";

        var validator = CreateValidator(first, second);

        await validator.StartAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Startup_Fails_WhenATenantWritesTheRouteWithTheDottedKeySpelling()
    {
        // Tenant configuration accepts a key with either separator, and the underscore form falls back to the
        // dotted form on read. A tenant that shadows the reserved path using the dotted spelling must still fail.
        var colliding = new ShellSettings { Name = "Support" };

        colliding["OrchardCore.HealthChecks:Url"] = ContactCenterConstants.HealthChecks.ProcessLivenessPath;

        var validator = CreateValidator(colliding);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Support", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_Succeeds_WhenThereIsNoShellSettingsManager()
    {
        // The probe can be hosted outside an Orchard Core application, where there are no tenants to validate.
        var validator = new ContactCenterProcessLivenessPathValidator(
            shellSettingsManager: null,
            new ContactCenterProcessLivenessOptions());

        await validator.StartAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Startup_ValidatesTheConfiguredPath_NotOnlyTheDefault()
    {
        var colliding = new ShellSettings { Name = "Default" };

        colliding["OrchardCore_HealthChecks:Url"] = "/probe/alive";

        var validator = new ContactCenterProcessLivenessPathValidator(
            new StubShellSettingsManager(colliding),
            new ContactCenterProcessLivenessOptions { Path = "/probe/alive" });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => validator.StartAsync(TestContext.Current.CancellationToken));
    }

    private static ContactCenterProcessLivenessPathValidator CreateValidator(params ShellSettings[] settings)
        => new(
            new StubShellSettingsManager(settings),
            new ContactCenterProcessLivenessOptions());

    private sealed class StubShellSettingsManager : IShellSettingsManager
    {
        private readonly ShellSettings[] _settings;

        public StubShellSettingsManager(params ShellSettings[] settings)
        {
            _settings = settings;
        }

        public ShellSettings CreateDefaultSettings() => new();

        public Task<IEnumerable<ShellSettings>> LoadSettingsAsync()
            => Task.FromResult<IEnumerable<ShellSettings>>(_settings);

        public Task<IEnumerable<string>> LoadSettingsNamesAsync()
            => Task.FromResult(_settings.Select(setting => setting.Name));

        public Task<ShellSettings> LoadSettingsAsync(string tenant)
            => Task.FromResult(_settings.FirstOrDefault(setting => setting.Name == tenant));

        public Task SaveSettingsAsync(ShellSettings settings) => Task.CompletedTask;

        public Task RemoveSettingsAsync(ShellSettings settings) => Task.CompletedTask;
    }
}
