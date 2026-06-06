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
            .DefineStyle("intl-tel-input")
            .SetUrl(
                "~/CrestApps.OrchardCore.ContentFields/vendors/intl-tel-input/css/intlTelInput.min.css",
                "~/CrestApps.OrchardCore.ContentFields/vendors/intl-tel-input/css/intlTelInput.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/intl-tel-input@25.12.4/build/css/intlTelInput.min.css",
                "https://cdn.jsdelivr.net/npm/intl-tel-input@25.12.4/build/css/intlTelInput.css")
            .SetVersion("25.12.4");

        _manifest
            .DefineStyle("international-telephone-editor")
            .SetUrl(
                "~/CrestApps.OrchardCore.ContentFields/styles/international-telephone-editor.min.css",
                "~/CrestApps.OrchardCore.ContentFields/styles/international-telephone-editor.css")
            .SetVersion("1.0.0");

        _manifest
            .DefineScript("intl-tel-input")
            .SetUrl(
                "~/CrestApps.OrchardCore.ContentFields/vendors/intl-tel-input/js/intlTelInputWithUtils.min.js",
                "~/CrestApps.OrchardCore.ContentFields/vendors/intl-tel-input/js/intlTelInputWithUtils.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/intl-tel-input@25.12.4/build/js/intlTelInputWithUtils.min.js",
                "https://cdn.jsdelivr.net/npm/intl-tel-input@25.12.4/build/js/intlTelInputWithUtils.js")
            .SetVersion("25.12.4");

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
