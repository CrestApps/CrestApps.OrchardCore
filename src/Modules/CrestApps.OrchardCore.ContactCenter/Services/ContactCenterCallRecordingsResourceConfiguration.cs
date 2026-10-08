using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Registers the call recording player script, which plays the recording from a transcript line, follows the
/// playback through the transcript and copies lines, as a named resource.
/// </summary>
internal sealed class ContactCenterCallRecordingsResourceConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ContactCenterCallRecordingsResourceConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("contact-center-call-recording")
            .SetUrl(
                "~/CrestApps.OrchardCore.ContactCenter/scripts/contact-center-call-recording.min.js",
                "~/CrestApps.OrchardCore.ContactCenter/scripts/contact-center-call-recording.js")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
