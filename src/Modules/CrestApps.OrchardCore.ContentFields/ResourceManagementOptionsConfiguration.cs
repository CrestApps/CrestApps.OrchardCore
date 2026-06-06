using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.ContentFields;

internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineStyle("international-telephone-editor")
            .SetUrl(
                "~/CrestApps.OrchardCore.ContentFields/styles/international-telephone-editor.min.css",
                "~/CrestApps.OrchardCore.ContentFields/styles/international-telephone-editor.css")
            .SetVersion("1.0.0");

        _manifest
            .DefineScript("international-telephone-editor")
            .SetUrl(
                "~/CrestApps.OrchardCore.ContentFields/scripts/international-telephone-editor.min.js",
                "~/CrestApps.OrchardCore.ContentFields/scripts/international-telephone-editor.js")
            .SetDependencies("intl-tel-input")
            .SetVersion("1.0.0");
    }

    /// <summary>
    /// Configures the resource manifests for this feature.
    /// </summary>
    /// <param name="options">The resource management options.</param>
    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
