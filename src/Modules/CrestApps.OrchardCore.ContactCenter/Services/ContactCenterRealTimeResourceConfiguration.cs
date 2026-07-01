using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Registers the Contact Center real-time client script as a named resource that depends on the SignalR
/// client library.
/// </summary>
internal sealed class ContactCenterRealTimeResourceConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ContactCenterRealTimeResourceConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("contact-center-realtime")
            .SetUrl("~/CrestApps.OrchardCore.ContactCenter/scripts/contact-center-realtime.js")
            .SetDependencies("signalr")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
