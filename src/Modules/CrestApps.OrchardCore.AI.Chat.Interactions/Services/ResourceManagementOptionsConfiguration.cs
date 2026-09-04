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
            .DefineScript("RealtimeAudio")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat.Interactions/scripts/realtime-audio.min.js", "~/CrestApps.OrchardCore.AI.Chat.Interactions/scripts/realtime-audio.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.162/dist/realtime-audio.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.162/dist/realtime-audio.js")
            .SetCdnIntegrity(
                "sha384-fD4C17UUFI6dfWhbk26WmEVCiZZZ0mqYc/ga7UX/Mtqw0+Zlm4kXKxmeKBncQ3zQ",
                "sha384-1R2+ottVAdwsVFtVO64VTNlgOwWhNN5cIO4uFTFzQwy6Ai1vN1Za6x/1kUYtS/3k")
            .SetDependencies("signalr", "dompurify")
            .SetVersion("2.0.0");

        _manifest
            .DefineScript("ChatInteractionApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat.Interactions/scripts/chat-interaction.min.js", "~/CrestApps.OrchardCore.AI.Chat.Interactions/scripts/chat-interaction.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.162/dist/chat-interaction.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.162/dist/chat-interaction.js")
            .SetCdnIntegrity(
                "sha384-3zAOt4py/fOmLmkZ73x4fVxFqF7T+fg7bJlifpzgelUrGywTSKJVAO/WrbsWvzvV",
                "sha384-HFip9IKooJy2ovcBtp9SLpKSNEj9tGhgtnr5s6M7mN+5Ug2+JQkFyHUPpu+1ZxrV")
            .SetDependencies("vuejs:3", "signalr", "marked", "chart.js", "highlightjs", "dompurify", "RealtimeAudio")
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
