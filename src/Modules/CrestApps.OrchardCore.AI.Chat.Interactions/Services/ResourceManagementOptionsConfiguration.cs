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
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview-22-preview.2/dist/chat-interaction.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview-22-preview.2/dist/chat-interaction.js")
            .SetCdnIntegrity(
                "sha384-8GykboNgasCAcQmE2RlJCkmNSPuRf/mMPX2Lqpqp+jqjNoXHS2xTGNHX4OMywllG",
                "sha384-1SOM3eTH3r16ZYZNKUnxkCXbcuZJcLSPTfW2YKf5CPmSIMwDECClrhVFe/xMHfWu")
            .SetDependencies("vuejs:3", "signalr", "marked", "chart.js", "highlightjs", "dompurify")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
