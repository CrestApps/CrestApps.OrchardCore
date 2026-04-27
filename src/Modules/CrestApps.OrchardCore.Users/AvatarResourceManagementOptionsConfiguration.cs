using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Represents the avatar resource management options configuration.
/// </summary>
public sealed class AvatarResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static AvatarResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("media2")
            .SetUrl("~/OrchardCore.Media/Scripts/media.min.js", "~/OrchardCore.Media/Scripts/media.js")
            .SetDependencies("vuejs:2", "Sortable", "vue-draggable:2", "jQuery-ui", "credential-helpers", "bootstrap")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle("user-profile-avatar")
            .SetUrl("~/CrestApps.OrchardCore.Users/styles/user-profile-avatar.min.css", "~/CrestApps.OrchardCore.Users/styles/user-profile-avatar.css")
            .SetVersion("1.0.0");
    }

    /// <summary>
    /// Configures the .
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
