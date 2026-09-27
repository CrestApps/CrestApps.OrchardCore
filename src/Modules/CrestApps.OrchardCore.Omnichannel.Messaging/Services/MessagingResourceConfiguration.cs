using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Registers the messaging notifications script, which every admin page but the workspace carries, as a named resource
/// that depends on the SignalR client library.
/// </summary>
internal sealed class MessagingResourceConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    /// <summary>
    /// The name of the script that raises messaging toasts and keeps the Messaging menu count current.
    /// </summary>
    public const string NotificationsScript = "messaging-notifications";

    private static readonly ResourceManifest _manifest;

    static MessagingResourceConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript(NotificationsScript)
            .SetUrl(
                "~/CrestApps.OrchardCore.Omnichannel.Messaging/scripts/messaging-notifications.min.js",
                "~/CrestApps.OrchardCore.Omnichannel.Messaging/scripts/messaging-notifications.js")
            .SetDependencies("signalr")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
