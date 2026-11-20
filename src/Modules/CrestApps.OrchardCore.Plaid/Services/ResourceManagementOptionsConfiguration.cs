using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// Declares Plaid Link's script. Plaid requires it to be loaded from its own CDN, never copied, so it has no local copy.
/// </summary>
internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest = new();

    static ResourceManagementOptionsConfiguration()
    {
        _manifest
            .DefineScript("plaid-link")
            .SetUrl("https://cdn.plaid.com/link/v2/stable/link-initialize.js");
    }

    public void Configure(ResourceManagementOptions options)
        => options.ResourceManifests.Add(_manifest);
}
