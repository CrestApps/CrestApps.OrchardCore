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
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.263/dist/chat-interaction.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.263/dist/chat-interaction.js")
            .SetCdnIntegrity(
                "sha384-cXswXOl8W2uQ/1K4B/zvcwmQbD2TOg0hUXtbWU500UynOCXrKYmYcUG4wjZ20BEB",
                "sha384-M1OvyjpSB3N5EbTonYVN2w9iAFraPfa0LGShRiE63MUXD6lwAfRI3hemeTzmTkXB")
            .SetDependencies("vuejs:3", "signalr", "marked", "chart.js", "highlightjs", "dompurify", "chat-output-guard", "realtime-audio", "chat-markers", "chat-image-carousel", "medium-zoom")
            .SetVersion("2.0.0");

        _manifest
            .DefineScript("ProfileBuilderApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat.Interactions/scripts/profile-builder.min.js", "~/CrestApps.OrchardCore.AI.Chat.Interactions/scripts/profile-builder.js")
            .SetDependencies("ChatInteractionApp")
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
