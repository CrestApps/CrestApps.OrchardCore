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
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/chat-interaction.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/chat-interaction.js")
            .SetCdnIntegrity(
                "sha384-awq6g7NbJiHqVRJKqdIbf868iiV9O+sNQnzCnBYzFlsVNwefhZ6G+K6MH1lmvaxH",
                "sha384-pUurVtvGuLP+5BD7oG/ekgAHdMRWaWgnln4nrx5ub8jLXZcnTGFeJDVyPVRIkt1r")
            .SetDependencies("vuejs:3", "signalr", "marked", "chart.js", "highlightjs", "dompurify", "realtime-audio", "chat-markers", "chat-image-carousel", "medium-zoom")
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
