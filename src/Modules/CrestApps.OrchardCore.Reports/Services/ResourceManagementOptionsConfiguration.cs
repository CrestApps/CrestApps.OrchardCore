using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Reports.Services;

/// <summary>
/// Registers the reusable report date-range picker script resource.
/// </summary>
internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("report-date-range-picker")
            .SetUrl(
                "~/CrestApps.OrchardCore.Reports/scripts/report-date-range-picker.min.js",
                "~/CrestApps.OrchardCore.Reports/scripts/report-date-range-picker.js")
            .SetDependencies("flatpickr", "flatpickr-culture")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
