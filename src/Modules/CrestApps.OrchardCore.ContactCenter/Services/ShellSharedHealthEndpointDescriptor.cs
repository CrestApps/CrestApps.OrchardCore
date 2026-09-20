using CrestApps.Core.ContactCenter.HealthChecks;
using CrestApps.OrchardCore.ContactCenter;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Describes this host's shared aggregate health endpoint from the tenant's configuration.
/// </summary>
/// <remarks>
/// The key the route lives under, the route it falls back to when the key is unset, and the key an operator
/// sets to accept the hazard are all Orchard's. Answering them here is what lets the framework's check take
/// the answers as a dependency rather than go looking for them.
/// </remarks>
internal sealed class ShellSharedHealthEndpointDescriptor : ISharedHealthEndpointDescriptor
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellSharedHealthEndpointDescriptor"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The tenant configuration.</param>
    public ShellSharedHealthEndpointDescriptor(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    /// <inheritdoc/>
    public string Route => ContactCenterProcessHealthServiceCollectionExtensions.ResolveSharedHealthEndpointRoute(
        _shellConfiguration[ContactCenterProcessHealthServiceCollectionExtensions.SharedHealthEndpointConfigurationKey]);

    /// <inheritdoc/>
    public bool IsAcknowledged => string.Equals(
        _shellConfiguration[ContactCenterProcessHealthServiceCollectionExtensions.SharedHealthEndpointAcknowledgementConfigurationKey],
        bool.TrueString,
        StringComparison.OrdinalIgnoreCase);
}
