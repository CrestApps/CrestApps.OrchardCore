using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.AI.Services;

internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("AIChatApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat.min.js", "~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/ai-chat.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/ai-chat.js")
            .SetCdnIntegrity(
                "sha384-8RkAbQpBsjJRalpBNzL/q707SCdxLRxk+GQU1KNoDHQRizInYjWnUgBLtfWJWW63",
                "sha384-xqwVRD69sGscXAiY0zGvqZnv3wYh9zm07VbGNjqsAHjHlai0tXwibmlQ3G0rEvC6")
            .SetDependencies("vuejs:3", "signalr", "marked", "chart.js", "highlightjs", "dompurify", "realtime-audio", "chat-markers", "chat-image-carousel", "medium-zoom")
            .SetVersion("2.0.0");

        _manifest
            .DefineScript("AIChatWidgetApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-widget.min.js", "~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-widget.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/ai-chat-widget.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/ai-chat-widget.js")
            .SetCdnIntegrity(
                "sha384-T0ltSFbSlD/9MOxNFHsMZo9gI2Ic/uAsqME2VIGPMt1l6XP7+b2vjBN3+fvch358",
                "sha384-HFFdb1FSV0+KMlCsTtceavyXxrjz8fUa6Nhod7mMujGhx+/650QVJElWq16MWIio")
            .SetDependencies("AIChatApp")
            .SetVersion("2.0.0");

        _manifest
            .DefineStyle("AIChatApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/css/ai-chat.min.css", "~/CrestApps.OrchardCore.AI.Chat/css/ai-chat.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/ai-chat.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/ai-chat.css")
            .SetCdnIntegrity(
                "sha384-8JlLr4C/lf7nyt6/xVqHI0B8xHraW3KCGvhNs9Q+H4Ol4iQXtUKUV+TKVXLv1mVr",
                "sha384-TsoR6c+xP86Zf1tk7L62qxv72ISHdprwfxy8nJzvQC8z8B6GUrLGfi7pGFkoCINF")
            .SetDependencies("chat-image-carousel")
            .SetVersion("2.0.0");

        _manifest
            .DefineStyle("AIChatWidget")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/css/ai-chat-widget.min.css", "~/CrestApps.OrchardCore.AI.Chat/css/ai-chat-widget.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/chat-widget.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/chat-widget.css")
            .SetCdnIntegrity(
                "sha384-WwgzuCtEQJq/V6vJYe1YU4P+tkPmVkFVt7h0rznZPgInsMJg92WZcxvc3k0IshjU",
                "sha384-tIjxz4a6eZlptaX/iKtfbZHRYQfz1B1gfLgUFT6ItuvZw2voaGihiCANikYi5ptA")
            .SetVersion("2.0.0");

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
