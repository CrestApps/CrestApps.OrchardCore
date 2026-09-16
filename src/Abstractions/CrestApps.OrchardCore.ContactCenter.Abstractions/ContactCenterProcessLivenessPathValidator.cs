using Microsoft.Extensions.Hosting;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Fails host startup when any tenant has configured the shared health-check endpoint on the path reserved by
/// the process liveness probe.
/// </summary>
/// <remarks>
/// The liveness probe runs as host middleware ahead of routing, so it short-circuits before any tenant route is
/// considered. A tenant that maps its health endpoint on the same path does not produce a duplicate-route error
/// — its endpoint simply becomes unreachable and is replaced by an unconditional <c>200 Healthy</c>. A health
/// endpoint that can only report success is worse than no health endpoint, so the collision must be loud.
/// <para>
/// Tenant configuration is not host configuration, so the check performed when the middleware is added cannot
/// see it. This validator reads each tenant's own configuration instead. Reading through the tenant settings
/// indexer resolves the same value the shared health-check module resolves, including sources contributed by
/// per-tenant configuration files, and it matches either the underscore or the dotted key spelling.
/// </para>
/// <para>
/// A tenant created after startup is not covered here. Tenants that enable Contact Center are covered by the
/// shell-level guard; for tenants that do not, the reserved path is documented instead.
/// </para>
/// </remarks>
public sealed class ContactCenterProcessLivenessPathValidator : IHostedService
{
    private readonly IShellSettingsManager _shellSettingsManager;
    private readonly ContactCenterProcessLivenessOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterProcessLivenessPathValidator"/> class.
    /// </summary>
    /// <param name="shellSettingsManager">
    /// The manager used to enumerate the configured tenants, or <see langword="null"/> when the probe is hosted
    /// outside an Orchard Core application and there are no tenants to validate.
    /// </param>
    /// <param name="options">The configured process liveness options.</param>
    public ContactCenterProcessLivenessPathValidator(
        IShellSettingsManager shellSettingsManager,
        ContactCenterProcessLivenessOptions options)
    {
        _shellSettingsManager = shellSettingsManager;
        _options = options;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_shellSettingsManager is null)
        {
            return;
        }

        var livenessPath = _options.Path;

        var allSettings = await _shellSettingsManager.LoadSettingsAsync();

        foreach (var settings in allSettings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Reading the indexer builds the tenant configuration on first access, and it does so by blocking on
            // the asynchronous builder. Building it here keeps that work off the synchronous path so a host with
            // many tenants does not consume a thread per tenant while starting.
            await settings.EnsureConfigurationAsync();

            ContactCenterProcessHealthApplicationBuilderExtensions.ThrowIfShadowsSharedHealthEndpoint(
                livenessPath,
                settings["OrchardCore_HealthChecks:Url"],
                settings.Name);
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
