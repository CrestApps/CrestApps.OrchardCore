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
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.96/dist/ai-chat.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.96/dist/ai-chat.js")
            .SetCdnIntegrity(
                "sha384-e6ddZeCoQh9qPWdCevbHOrrW60seZI5HsRZ3PkYYvwr31Lk3WyhomSMnG7yKSbFT",
                "sha384-jGML6FAkXMf7ONdelHyA+eX2zAfw+zC+NAz/gzgQuUHfIZ5XuE3Rgk+keN0xp9ud")
            .SetDependencies("vuejs:3", "signalr", "marked", "chart.js", "highlightjs", "dompurify")
            .SetVersion("1.0.0");

        _manifest
            .DefineScript("AIChatWidgetApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-widget.min.js", "~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat-widget.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.96/dist/ai-chat-widget.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.96/dist/ai-chat-widget.js")
            .SetCdnIntegrity(
                "sha384-BqfuUqCoHGZqSRMFABvzYYm5+Sceqi4ody5I1g8S52dDzkXGb3C0yPzPcl5sNgKY",
                "sha384-1QWUphDwh+WoWeiUU0tq/fGEDgogp+gVOGWfeuds0Wr+pKnxAldG/H3bF3vmMyx3")
            .SetDependencies("AIChatApp")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle("AIChatApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/css/ai-chat.min.css", "~/CrestApps.OrchardCore.AI.Chat/css/ai-chat.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.96/dist/ai-chat.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.96/dist/ai-chat.css")
            .SetCdnIntegrity(
                "sha384-t/j6TAXmyaWvlHu6+pm+WwIN6H5rG/8WFGgohu5Zq8utgwHRv/rKjhJhPIDQfiU5",
                "sha384-0bpTpHX8ajOj3uwwics1QHr5xtrpAKYdT1UXNpfPSdA9Nq2Zm8czmHdTO3MxqExZ")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle("AIChatWidget")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/css/ai-chat-widget.min.css", "~/CrestApps.OrchardCore.AI.Chat/css/ai-chat-widget.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.96/dist/chat-widget.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@1.0.0-preview.96/dist/chat-widget.css")
            .SetCdnIntegrity(
                "sha384-auFZ9Jc/esRN07i3YjQiqidPLTGuW7iSU50kVNUcd9NuIZzG3X/JlpCiQdLtpmBo",
                "sha384-Dm8nm91DzdsYXwDeYl1N/b4l5+zZkFZm+tE4UzH5t/W1uIDOpvt9bzFqSqxDM92V")
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
