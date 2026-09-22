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
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.199/dist/ai-chat.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.199/dist/ai-chat.js")
            .SetCdnIntegrity(
                "sha384-O4IhNnJlWeQ4Nnv539YLSU6+Iiuce8rkfqTX9J+ztDItyKTpRuk2Wx1FWbmODVXy",
                "sha384-Y0/PwfTsd4XepLLiv5/hEBOtM7/W4zZ0XnkIEQmvOKAARVtgWUiJxCGE/Kpr3dST")
            .SetDependencies("vuejs:3", "signalr", "marked", "chart.js", "highlightjs", "dompurify", "realtime-audio", "chat-markers", "medium-zoom")
            .SetVersion("2.0.0");

        _manifest
            .DefineScript("AIChatWidgetApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-widget.min.js", "~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-widget.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.199/dist/ai-chat-widget.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.199/dist/ai-chat-widget.js")
            .SetCdnIntegrity(
                "sha384-JpfYxsQXZwg0H6LfwDj26TrcwcFGPKazqsQA83FP70hy2vCLwQWreKQIQ7HMrKXK",
                "sha384-+dvDdShn9ZUw9QDWQaDkyrV6sbNwVZcBLcjxD82nmOWYh5Q6YZ49OFKFz6glUycX")
            .SetDependencies("AIChatApp")
            .SetVersion("2.0.0");

        _manifest
            .DefineStyle("AIChatApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/css/ai-chat.min.css", "~/CrestApps.OrchardCore.AI.Chat/css/ai-chat.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.199/dist/ai-chat.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.199/dist/ai-chat.css")
            .SetCdnIntegrity(
                "sha384-2RGu/MdhfA+6zSK+kiPAerBR3PVDeI4TDIYHn0h8f9Dpu67hx6f2OaVUBoKl5A+F",
                "sha384-lYlWfcC0cLmmFGBW2ffzbd9FGbb5aKjnLciFXirK+J78tFirfgGcuJOHqNKEp8H6")
            .SetVersion("2.0.0");

        _manifest
            .DefineStyle("AIChatWidget")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/css/ai-chat-widget.min.css", "~/CrestApps.OrchardCore.AI.Chat/css/ai-chat-widget.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.199/dist/chat-widget.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.199/dist/chat-widget.css")
            .SetCdnIntegrity(
                "sha384-IlBke7eEglSKvkPKQUQVsa5bSaV+2uXk8Oadv2+6hddGcJrpb/+0/AYv6sBEfMJX",
                "sha384-i1dLsHGx7PRti8J+IwOxJt0TeRC3O/JEQmEsmgWLXA048wfH7WhrFAqDqjcKzkoe")
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
