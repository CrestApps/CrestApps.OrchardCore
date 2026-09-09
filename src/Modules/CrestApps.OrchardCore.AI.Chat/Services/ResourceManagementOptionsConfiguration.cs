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
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat.min.js", "~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.168/dist/ai-chat.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.168/dist/ai-chat.js")
            .SetCdnIntegrity(
                "sha384-YU42TPz1v7YnfBynPz9M4cOwR/H6P0xux+GsQdXYKA0ULpiVA7JCsmAjhjV/et9j",
                "sha384-Vpr5eUY5j9/JLW0HM1RN4wUKLgdxR+PCMYOV4/oYzW7L1y8Dk1DxoSoHjbiHAJYj")
            .SetDependencies("vuejs:3", "signalr", "marked", "chart.js", "highlightjs", "dompurify", "realtime-audio")
            .SetVersion("2.0.0");

        _manifest
            .DefineScript("AIChatWidgetApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-widget.min.js", "~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-widget.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.168/dist/ai-chat-widget.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.168/dist/ai-chat-widget.js")
            .SetCdnIntegrity(
                "sha384-JpfYxsQXZwg0H6LfwDj26TrcwcFGPKazqsQA83FP70hy2vCLwQWreKQIQ7HMrKXK",
                "sha384-+dvDdShn9ZUw9QDWQaDkyrV6sbNwVZcBLcjxD82nmOWYh5Q6YZ49OFKFz6glUycX")
            .SetDependencies("AIChatApp")
            .SetVersion("2.0.0");

        _manifest
            .DefineStyle("AIChatApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/css/ai-chat.min.css", "~/CrestApps.OrchardCore.AI.Chat/css/ai-chat.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.168/dist/ai-chat.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.168/dist/ai-chat.css")
            .SetCdnIntegrity(
                "sha384-KKZ8hVlHa3DGMa6QV4PPzbSyMsxF76YT6+dRHsiI+cBJ3wt+m9cSBW07CQlKUq87",
                "sha384-ItWpot1IYdNGFGwJxcWg+uJ42DWXOXkl/WeVy1Byf0LfcKgFjiV8kuKlSkcf/Z4Z")
            .SetVersion("2.0.0");

        _manifest
            .DefineStyle("AIChatWidget")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/css/ai-chat-widget.min.css", "~/CrestApps.OrchardCore.AI.Chat/css/ai-chat-widget.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.168/dist/chat-widget.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.168/dist/chat-widget.css")
            .SetCdnIntegrity(
                "sha384-ETUZFWfTXtm2hDzz71g3rPZQSSA9R0njFfXiYaBT6QTb2+bLMuZNQKChN52Q+pzU",
                "sha384-8SMfXg8HrWLJjzqfwxowYAz0HVAsaN2o5Y1X46je0Fa3HrwobAKBTbPOrPI0gfz/")
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
