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
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.34/dist/ai-chat.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.34/dist/ai-chat.js")
            .SetCdnIntegrity(
                "sha384-0TQVfTiEATa7LWBIdbRmwHwKMrvtAtn6Wav2U6H54SCL1rFk1JVcLyFeECV3zm9K",
                "sha384-074ZH7UQ+8z810HgxTTqsdGcdhM6OdBZijV0TZTSkhC3Q+LOQNqUdHtwFJ+E/FvJ")
            .SetDependencies("vuejs:3", "signalr", "marked", "chart.js", "highlightjs", "dompurify")
            .SetVersion("1.0.0");

        _manifest
            .DefineScript("AIChatWidgetApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-widget.min.js", "~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-widget.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.34/dist/ai-chat-widget.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.34/dist/ai-chat-widget.js")
            .SetCdnIntegrity(
                "sha384-TqvBSdAs+N5dWRSPwrRafxmoKY7Fs8t/XiCR2nAYLzHZs7bOWcMj0cbuB5VGsTvF",
                "sha384-vQO0QA9M9d4rHI5FwCAh8RBhc5KIXrtMG26irgjE+Fftd+f1BrWbZ+7tuezA4BKG")
            .SetDependencies("AIChatApp")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle("AIChatApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/css/ai-chat.min.css", "~/CrestApps.OrchardCore.AI.Chat/css/ai-chat.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.34/dist/ai-chat.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.34/dist/ai-chat.css")
            .SetCdnIntegrity(
                "sha384-bJjjvf0Oud5iyT9x+pKZv3EvgnlU3JZUnS4PG7Kp411hZQgbSSOwq6HRUkAz6cuM",
                "sha384-WHGMpco3LUlQBZwMttut8K0FJoBTvdU2VEmqxxKrvJSCC4UnEFvKY/0qCFawcYI7")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle("AIChatWidget")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/css/ai-chat-widget.min.css", "~/CrestApps.OrchardCore.AI.Chat/css/ai-chat-widget.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.34/dist/chat-widget.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.34/dist/chat-widget.css")
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
