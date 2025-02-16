using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.SignalR.Services;
internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest.DefineScript("signalr")
            .SetUrl("~/CrestApps.OrchardCore.SignalR/js/signalr.min.js", "~/CrestApps.OrchardCore.SignalR/js/signalr.js")
            .SetCdn("https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.7/signalr.min.js", "https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.7/signalr.js")
            .SetCdnIntegrity("sha512-7SRCYIJtR6F8ocwW7UxW6wGKqbSyqREDbfCORCbGLatU0iugBLwyOXpzhkPyHIFdBO0K2VCu57fvP2Twgx1o2A==", "sha512-FzakzcmrNSXS5+DuuYSO6+5DcUZ417Na0vH1oAIo49mMBA8rHSgkKSjE2ALFOxdQ/kPqF3HZRzb0HQ+AvwXttg==")
            .SetVersion("8.0.7");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
