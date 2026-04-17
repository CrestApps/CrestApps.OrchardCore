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
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.12/dist/ai-chat.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.12/dist/ai-chat.js")
            .SetCdnIntegrity(
                "sha384-s43xyzKh/siOhFCcHlm+WVlX2K1IQXXq9IbqaGj1j+iUbOgc4VEk70brV0u51jid",
                "sha384-s43xyzKh/siOhFCcHlm+WVlX2K1IQXXq9IbqaGj1j+iUbOgc4VEk70brV0u51jid")
            .SetDependencies("vuejs:3", "signalr", "marked", "chart.js", "highlightjs", "dompurify")
            .SetVersion("1.0.0");

        _manifest
            .DefineScript("AIChatWidgetApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-widget.js", "~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-widget.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.12/dist/ai-chat-widget.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.12/dist/ai-chat-widget.js")
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
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.12/dist/ai-chat.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.12/dist/ai-chat.css")
            .SetCdnIntegrity(
                "sha384-MzWz9Vr1nHpdxYXs/Q3BZ9c5t3J+rWnNkDgiIKuRZ+KRLtSBqpN3mPkixadUwI1x",
                "sha384-lWW1vUVlgsEF0EGFmxTVmwEFt49FtHTMYhawcrmW5yD8tNseGWxyetqJd36Z7ffm")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle("AIChatWidget")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/css/ai-chat-widget.min.css", "~/CrestApps.OrchardCore.AI.Chat/css/ai-chat-widget.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.12/dist/chat-widget.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.12/dist/chat-widget.css")
            .SetCdnIntegrity(
                "sha384-43TN/LFH2sUNlCODSfjwSCfWJ0QtvK0jN3POF+ZSXcvDKuhLQopGIpQ2CZlHf8nE",
                "sha384-43TN/LFH2sUNlCODSfjwSCfWJ0QtvK0jN3POF+ZSXcvDKuhLQopGIpQ2CZlHf8nE")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle("SpeechToText")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/css/speech-to-text.min.css", "~/CrestApps.OrchardCore.AI.Chat/css/speech-to-text.css")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
