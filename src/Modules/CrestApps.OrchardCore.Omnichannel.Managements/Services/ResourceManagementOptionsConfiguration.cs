using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Registers the Omnichannel Management script resources.
/// </summary>
internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("subject-action-assignment-toggle")
            .SetUrl(
                "~/CrestApps.OrchardCore.Omnichannel.Managements/scripts/subject-action-assignment-toggle.min.js",
                "~/CrestApps.OrchardCore.Omnichannel.Managements/scripts/subject-action-assignment-toggle.js")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
