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
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.197/dist/chat-interaction.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.197/dist/chat-interaction.js")
            .SetCdnIntegrity(
                "sha384-wxY1B0PY7jJY7aTF3ACcavd1esvCsA123WCStwF8OUVMtnqjFTjf4RoRLtgAWdpI",
                "sha384-LFMwjeYvQk/TI4rUwy2ClVtETBduFpdc8oNkf6leqRAoci3Cv6ArViFSMno3+pdp")
            .SetDependencies("vuejs:3", "signalr", "marked", "chart.js", "highlightjs", "dompurify", "realtime-audio", "chat-markers")
            .SetVersion("2.0.0");
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
