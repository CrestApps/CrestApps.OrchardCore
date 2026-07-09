using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Registers the Contact Center soft-phone helper script.
/// </summary>
internal sealed class ContactCenterSoftPhoneResourceConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ContactCenterSoftPhoneResourceConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("contact-center-soft-phone")
            .SetUrl("~/CrestApps.OrchardCore.ContactCenter/scripts/contact-center-soft-phone.js")
            .SetDependencies("telephony-soft-phone")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
