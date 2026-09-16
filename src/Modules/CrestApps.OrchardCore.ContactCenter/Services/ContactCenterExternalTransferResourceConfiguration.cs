using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Registers the Contact Center external transfer settings editor script as a named resource so the
/// approved-destinations table on the settings screen is enhanced through the resource manager rather than
/// an inline script block.
/// </summary>
internal sealed class ContactCenterExternalTransferResourceConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ContactCenterExternalTransferResourceConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("contact-center-external-transfer-settings")
            .SetUrl(
                "~/CrestApps.OrchardCore.ContactCenter/scripts/contact-center-external-transfer-settings.min.js",
                "~/CrestApps.OrchardCore.ContactCenter/scripts/contact-center-external-transfer-settings.js")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
