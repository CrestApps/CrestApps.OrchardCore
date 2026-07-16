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

        _manifest
            .DefineScript("contact-center-agent-workspace")
            .SetUrl("~/CrestApps.OrchardCore.ContactCenter/scripts/agent-workspace.js")
            .SetDependencies("contact-center-realtime")
            .SetVersion("1.0.0");

        _manifest
            .DefineScript("contact-center-supervisor-dashboard")
            .SetUrl("~/CrestApps.OrchardCore.ContactCenter/scripts/supervisor-dashboard.js")
            .SetDependencies("contact-center-realtime")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle("contact-center-workspace")
            .SetUrl("~/CrestApps.OrchardCore.ContactCenter/styles/contact-center-workspace.css")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
