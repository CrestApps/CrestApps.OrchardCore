using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Registers the inbound entry point's visual IVR menu editor script as a named resource, so the editor view loads it
/// through the resource manager rather than an inline script block.
/// </summary>
internal sealed class ContactCenterIvrMenuEditorResourceConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ContactCenterIvrMenuEditorResourceConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("contact-center-ivr-menu-editor")
            .SetUrl(
                "~/CrestApps.OrchardCore.ContactCenter/scripts/ivr-menu-editor.min.js",
                "~/CrestApps.OrchardCore.ContactCenter/scripts/ivr-menu-editor.js")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
