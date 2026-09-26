using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Registers the script that shows a supervisor's own engagement in their soft phone.
/// </summary>
internal sealed class ContactCenterSupervisorSoftPhoneResourceConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ContactCenterSupervisorSoftPhoneResourceConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("contact-center-supervisor-phone")
            .SetUrl(
                "~/CrestApps.OrchardCore.ContactCenter/scripts/contact-center-supervisor-phone.min.js",
                "~/CrestApps.OrchardCore.ContactCenter/scripts/contact-center-supervisor-phone.js")
            .SetDependencies("telephony-soft-phone", "contact-center-realtime")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
