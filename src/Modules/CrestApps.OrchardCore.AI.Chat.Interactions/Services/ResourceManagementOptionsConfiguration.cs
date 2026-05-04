using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Services;

internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("ChatInteractionApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat.Interactions/scripts/chat-interaction.min.js", "~/CrestApps.OrchardCore.AI.Chat.Interactions/scripts/chat-interaction.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.20/dist/chat-interaction.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.20/dist/chat-interaction.js")
            .SetCdnIntegrity(
                "sha384-Zfts5ybZCX6YTQu1pDES5yQ735CQdA9PQl8T4x3tGqOnjxVFSn2rv5mlCEXms4NX",
                "sha384-tBvr5DYTg+j90nhZOEwjkKDD9SJ2HjrXEES3WHIWwlfy9wvQVobRKF5wfqnKMjQo")
            .SetDependencies("vuejs:3", "signalr", "marked", "chart.js", "highlightjs", "dompurify")
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
