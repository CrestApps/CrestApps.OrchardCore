using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Registers the soft phone script and style resources.
/// </summary>
internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("telephony-soft-phone")
            .SetUrl(
                "~/CrestApps.OrchardCore.Telephony/scripts/soft-phone.min.js",
                "~/CrestApps.OrchardCore.Telephony/scripts/soft-phone.js")
            .SetDependencies("signalr")
            .SetVersion("1.0.0");

        _manifest
            .DefineScript("telephony-phone-field")
            .SetUrl(
                "~/CrestApps.OrchardCore.Telephony/scripts/phone-field-dialer.min.js",
                "~/CrestApps.OrchardCore.Telephony/scripts/phone-field-dialer.js")
            .SetDependencies("telephony-soft-phone")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle("telephony-soft-phone")
            .SetUrl(
                "~/CrestApps.OrchardCore.Telephony/styles/soft-phone.min.css",
                "~/CrestApps.OrchardCore.Telephony/styles/soft-phone.css")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
