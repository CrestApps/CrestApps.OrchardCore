using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests.DependencyInjection;

/// <summary>
/// Records each tenant's service collection so the dependency-injection snapshot test can compare it
/// against an approved baseline.
/// </summary>
/// <remarks>
/// This works because the test assembly is itself the Orchard application module: the host passes its
/// full name as the application name, so a <see cref="StartupBase"/> declared here runs for every
/// tenant. It registers nothing, so it cannot perturb what it is measuring, and it runs last so it
/// sees everything the other modules registered.
/// </remarks>
public sealed class DiSnapshotStartup : StartupBase
{
    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiSnapshotStartup"/> class.
    /// </summary>
    /// <param name="shellSettings">The settings of the tenant being configured.</param>
    public DiSnapshotStartup(ShellSettings shellSettings)
    {
        _shellSettings = shellSettings;
    }

    /// <summary>
    /// Gets the order this startup runs in. It is last so the capture sees the complete collection.
    /// </summary>
    public override int Order => int.MaxValue;

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => TenantServiceCollectionCapture.Capture(_shellSettings.Name, services);
}
