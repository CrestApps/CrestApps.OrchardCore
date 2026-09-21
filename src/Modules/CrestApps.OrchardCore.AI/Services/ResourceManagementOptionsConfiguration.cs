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
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.193/dist/ai-chat.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.193/dist/ai-chat.js")
            .SetCdnIntegrity(
                "sha384-vfgMfUDUpZDxIoaVSrGHi0IPmbm87tFGRy0m2CK7sdkAYNae8ZaVIinGjgz+MmUH",
                "sha384-zfmrPmbxF7nBBHVmcy1cKZ3/1g8l2pAot7FSKaMfIJkJ2Q7cSm2Zl2cgiKQZzE4h")
            .SetDependencies("vuejs:3", "signalr", "marked", "chart.js", "highlightjs", "dompurify", "realtime-audio", "chat-markers")
            .SetVersion("2.0.0");

        _manifest
            .DefineScript("AIChatWidgetApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-widget.min.js", "~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-widget.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.193/dist/ai-chat-widget.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.193/dist/ai-chat-widget.js")
            .SetCdnIntegrity(
                "sha384-JpfYxsQXZwg0H6LfwDj26TrcwcFGPKazqsQA83FP70hy2vCLwQWreKQIQ7HMrKXK",
                "sha384-+dvDdShn9ZUw9QDWQaDkyrV6sbNwVZcBLcjxD82nmOWYh5Q6YZ49OFKFz6glUycX")
            .SetDependencies("AIChatApp")
            .SetVersion("2.0.0");

        _manifest
            .DefineStyle("AIChatApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/css/ai-chat.min.css", "~/CrestApps.OrchardCore.AI.Chat/css/ai-chat.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.193/dist/ai-chat.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.193/dist/ai-chat.css")
            .SetCdnIntegrity(
                "sha384-7/nERDOFg2nJQfxPjk55WP7mxwr86X3rGvYqoEeVnG38V2R3XZ7lWHHxQ9edeX5q",
                "sha384-KBAS9K0SoMvY70cKnocs6fHYBA5jjC8Mg+mQLJedfgzkg4rJxWA8uHwakNOnwRaJ")
            .SetVersion("2.0.0");

        _manifest
            .DefineStyle("AIChatWidget")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/css/ai-chat-widget.min.css", "~/CrestApps.OrchardCore.AI.Chat/css/ai-chat-widget.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.193/dist/chat-widget.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.193/dist/chat-widget.css")
            .SetCdnIntegrity(
                "sha384-+bEQ+hsZATs5TpYnxCL/fhDZ8HUeW4cp8J/Qv7jhAeo1di2Ys73W8/AU10kU/Xgd",
                "sha384-qpuTFjHkm+DcdTVnXo0w3ssV3PqHNNE9qLGuWEGZaHAAwuJ+Ij/PqN6j9NKTaNP8")
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
