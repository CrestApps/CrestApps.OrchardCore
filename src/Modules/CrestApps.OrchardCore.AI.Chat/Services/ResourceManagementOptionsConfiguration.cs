using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.AI.Chat.Services;

internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("AIChatApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat.js", "~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.20/dist/ai-chat.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.20/dist/ai-chat.js")
            .SetCdnIntegrity(
                "sha384-hQ0nlMO9O97slsZUhfjSmczU/SEuVx9lgk4dZFJH5D59MCw/8w41EvJJ+69rXuK+",
                "sha384-hQ0nlMO9O97slsZUhfjSmczU/SEuVx9lgk4dZFJH5D59MCw/8w41EvJJ+69rXuK+")
            .SetDependencies("vuejs:3", "signalr", "marked", "chart.js", "highlightjs", "dompurify")
            .SetVersion("1.0.0");

        _manifest
            .DefineScript("AIChatWidgetApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-widget.js", "~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-widget.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.20/dist/ai-chat-widget.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.20/dist/ai-chat-widget.js")
            .SetCdnIntegrity(
                "sha384-IkZmzaEN+JpydGY4pk5YnnxdkuwpGuayw9LH2cR+RalyU9f++vxLO26tXM+tRWBn",
                "sha384-IkZmzaEN+JpydGY4pk5YnnxdkuwpGuayw9LH2cR+RalyU9f++vxLO26tXM+tRWBn")
            .SetDependencies("AIChatApp")
            .SetVersion("1.0.0");

        _manifest
            .DefineScript("AIChatAppPatch")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-orchard-patch.js", "~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-orchard-patch.js")
            .SetDependencies("AIChatWidgetApp")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle("AIChatApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/css/ai-chat.min.css", "~/CrestApps.OrchardCore.AI.Chat/css/ai-chat.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.20/dist/ai-chat.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.20/dist/ai-chat.css")
            .SetCdnIntegrity(
                "sha384-bJjjvf0Oud5iyT9x+pKZv3EvgnlU3JZUnS4PG7Kp411hZQgbSSOwq6HRUkAz6cuM",
                "sha384-WHGMpco3LUlQBZwMttut8K0FJoBTvdU2VEmqxxKrvJSCC4UnEFvKY/0qCFawcYI7")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle("AIChatWidget")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/css/ai-chat-widget.min.css", "~/CrestApps.OrchardCore.AI.Chat/css/ai-chat-widget.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.20/dist/chat-widget.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.20/dist/chat-widget.css")
            .SetCdnIntegrity(
                "sha384-43TN/LFH2sUNlCODSfjwSCfWJ0QtvK0jN3POF+ZSXcvDKuhLQopGIpQ2CZlHf8nE",
                "sha384-kFIZQXfeh+eCNY+qzY0OOwy+G6bCSeo92lo3jbjumcNTj7mM2GdYsX47xlKA0Ecv")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle("SpeechToText")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/css/speech-to-text.min.css", "~/CrestApps.OrchardCore.AI.Chat/css/speech-to-text.css")
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
