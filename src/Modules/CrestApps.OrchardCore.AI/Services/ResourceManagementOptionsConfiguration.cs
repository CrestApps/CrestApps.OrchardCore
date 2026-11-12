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
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.258/dist/ai-chat.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.258/dist/ai-chat.js")
            .SetCdnIntegrity(
                "sha384-vI/dryZPA6fIg9iKf34K+zdS3Zz5FEgyxtln8TW2jPm2ZKyRVl3nFi2ePb6xTmtx",
                "sha384-r5VprqpaE5v5f7ttpkGZrh9T7l9Sy26lOerDk3zelA4sI8PtwEPdkNqIYlKjEOXw")
            .SetDependencies("vuejs:3", "signalr", "marked", "chart.js", "highlightjs", "dompurify", "realtime-audio", "chat-markers", "chat-image-carousel", "medium-zoom")
            .SetVersion("2.0.0");

        _manifest
            .DefineScript("AIChatWidgetApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-widget.min.js", "~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-widget.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.258/dist/ai-chat-widget.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.258/dist/ai-chat-widget.js")
            .SetCdnIntegrity(
                "sha384-T0ltSFbSlD/9MOxNFHsMZo9gI2Ic/uAsqME2VIGPMt1l6XP7+b2vjBN3+fvch358",
                "sha384-HFFdb1FSV0+KMlCsTtceavyXxrjz8fUa6Nhod7mMujGhx+/650QVJElWq16MWIio")
            .SetDependencies("AIChatApp")
            .SetVersion("2.0.0");

        _manifest
            .DefineStyle("AIChatApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/css/ai-chat.min.css", "~/CrestApps.OrchardCore.AI.Chat/css/ai-chat.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.258/dist/ai-chat.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.258/dist/ai-chat.css")
            .SetCdnIntegrity(
                "sha384-Wp+eUq72fPUp7GCBS+OhVh1JNPFU89ra/7IZJTWcmG0r0ZX7Rm2+DP7a77mhR0tB",
                "sha384-k7qd58/J384fqbzddBhToY/cr062hotZb+QrQzSbjLQkXJgyp8IcfXwiIkYslC2r")
            .SetDependencies("chat-image-carousel")
            .SetVersion("2.0.0");

        _manifest
            .DefineStyle("AIChatWidget")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/css/ai-chat-widget.min.css", "~/CrestApps.OrchardCore.AI.Chat/css/ai-chat-widget.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.258/dist/chat-widget.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.258/dist/chat-widget.css")
            .SetCdnIntegrity(
                "sha384-oIo/oi4xNxpnlQUXK8QdEf7DXOAGDLbabYSyXxMBnRgiAs2VvmD7WFkP3dKZKut1",
                "sha384-rUAxS5l44ITeubO5FMQi7qA/vD5SgZ1EOTbQOXbEvbRcoiBD88z5la/Q8aZpaKqb")
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
