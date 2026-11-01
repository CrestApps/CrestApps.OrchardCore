using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Resources;

internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("list-management-ui")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/scripts/list-management-ui.min.js",
                "~/CrestApps.OrchardCore.Resources/scripts/list-management-ui.js"
                )
            .SetVersion("1.0.0");

        _manifest
            .DefineScript("item-selector")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/scripts/item-selector.min.js",
                "~/CrestApps.OrchardCore.Resources/scripts/item-selector.js"
                )
            .SetVersion("1.0.0");

        _manifest
            .DefineScript("collapsible-panel")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/scripts/collapsible-panel.min.js",
                "~/CrestApps.OrchardCore.Resources/scripts/collapsible-panel.js"
                )
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle("item-selector")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/styles/item-selector.min.css",
                "~/CrestApps.OrchardCore.Resources/styles/item-selector.css"
                )
            .SetVersion("1.0.0");

        _manifest
            .DefineScript("easymde")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/easymde/js/easymde.min.js",
                "~/CrestApps.OrchardCore.Resources/vendors/easymde/js/easymde.js"
                )
            .SetVersion("2.21.0");

        _manifest
            .DefineStyle("easymde")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/easymde/css/easymde.min.css",
                "~/CrestApps.OrchardCore.Resources/vendors/easymde/css/easymde.css"
                )
            .SetVersion("2.21.0");

        // FullCalendar 7 ships one global build (no .min.js in the package); jsDelivr minifies it on request. The CSS is no
        // longer part of the script, so the skeleton, the classic theme and its palette are separate styles.
        _manifest
            .DefineScript("fullcalendar")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/fullcalendar/global.js",
                "~/CrestApps.OrchardCore.Resources/vendors/fullcalendar/global.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/fullcalendar@7.1.3/all/global.min.js",
                "https://cdn.jsdelivr.net/npm/fullcalendar@7.1.3/all/global.js")
            .SetVersion("7.1.3");

        _manifest
            .DefineScript("fullcalendar-locales")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/fullcalendar/locales-all.global.js",
                "~/CrestApps.OrchardCore.Resources/vendors/fullcalendar/locales-all.global.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/fullcalendar@7.1.3/locales-all/global.min.js",
                "https://cdn.jsdelivr.net/npm/fullcalendar@7.1.3/locales-all/global.js")
            .SetDependencies("fullcalendar")
            .SetVersion("7.1.3");

        // FullCalendar 7 draws nothing without a theme plugin; this registers the classic theme (pair it with the theme and palette styles).
        _manifest
            .DefineScript("fullcalendar-theme")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/fullcalendar/classic-theme.global.js",
                "~/CrestApps.OrchardCore.Resources/vendors/fullcalendar/classic-theme.global.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/fullcalendar@7.1.3/themes/classic/global.min.js",
                "https://cdn.jsdelivr.net/npm/fullcalendar@7.1.3/themes/classic/global.js")
            .SetDependencies("fullcalendar")
            .SetVersion("7.1.3");

        _manifest
            .DefineStyle("fullcalendar")
            .SetUrl("~/CrestApps.OrchardCore.Resources/vendors/fullcalendar/skeleton.css")
            .SetCdn("https://cdn.jsdelivr.net/npm/fullcalendar@7.1.3/skeleton.css")
            .SetVersion("7.1.3");

        _manifest
            .DefineStyle("fullcalendar-theme")
            .SetUrl("~/CrestApps.OrchardCore.Resources/vendors/fullcalendar/classic-theme.css")
            .SetCdn("https://cdn.jsdelivr.net/npm/fullcalendar@7.1.3/themes/classic/theme.css")
            .SetDependencies("fullcalendar")
            .SetVersion("7.1.3");

        _manifest
            .DefineStyle("fullcalendar-palette")
            .SetUrl("~/CrestApps.OrchardCore.Resources/vendors/fullcalendar/classic-palette.css")
            .SetCdn("https://cdn.jsdelivr.net/npm/fullcalendar@7.1.3/themes/classic/palette.css")
            .SetDependencies("fullcalendar-theme")
            .SetVersion("7.1.3");

        _manifest
            .DefineScript("chart.js")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/chartjs/chart.min.js",
                "~/CrestApps.OrchardCore.Resources/vendors/chartjs/chart.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/chart.js@4.5.1/dist/chart.umd.min.js",
                "https://cdn.jsdelivr.net/npm/chart.js@4.5.1/dist/chart.umd.js")
            .SetCdnIntegrity(
                "sha384-jb8JQMbMoBUzgWatfe6COACi2ljcDdZQ2OxczGA3bGNeWe+6DChMTBJemed7ZnvJ",
                "sha384-hfkuqrKeWFmnTMWN31VWyoe8xgdTADD11kgxmdpx2uyE6j5Az5uZq6u6AKYYmAOw")
            .SetVersion("4.5.1");

        // Enlarges a figure in a chat answer when it is clicked. The chat script wires it only when it is
        // present, so a surface that does not reference this simply keeps its thumbnails as they are. The
        // library injects the styles it needs itself, which is why there is no stylesheet beside it.
        _manifest
            .DefineScript("medium-zoom")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/medium-zoom/medium-zoom.min.js",
                "~/CrestApps.OrchardCore.Resources/vendors/medium-zoom/medium-zoom.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/medium-zoom@1.1.0/dist/medium-zoom.min.js",
                "https://cdn.jsdelivr.net/npm/medium-zoom@1.1.0/dist/medium-zoom.js")
            .SetCdnIntegrity(
                "sha384-o0gqi06am9fKVfa/jWO8/UE7OxHG6t+fgq/XaASsuwT8OBsFcxN7YhjtqyTfIxtS",
                "sha384-wiUiEpt8cFQq4r7ZbV/sSqPv6F+37YtOoAvtH26JDT4+LXPkihYbavRAc98qE1PB")
            .SetVersion("1.1.0");

        _manifest
            .DefineScript("marked")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/scripts/marked.min.js",
                "~/CrestApps.OrchardCore.Resources/scripts/marked.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/marked@18.0.14/lib/marked.umd.min.js",
                "https://cdn.jsdelivr.net/npm/marked@18.0.14/lib/marked.umd.js")
            .SetCdnIntegrity(
                "sha384-1KNqLSVIIDocc7NKjWP/vfNnoRSAenAfiLA3OnW7YOebcl46U/07fZMCkfzuBa+a",
                "sha384-2vpGtuKqJvFlwJqYnf/wUMuzUfhUnYBt9oay0e2yaFcq0Dh6/aEbQ8YAOeKGzlYo")
            .SetVersion("18.0.14");

        _manifest
            .DefineScript("flatpickr")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/flatpickr/js/flatpickr.min.js",
                "~/CrestApps.OrchardCore.Resources/vendors/flatpickr/js/flatpickr.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/flatpickr@4.6.13/dist/flatpickr.min.js",
                "https://cdn.jsdelivr.net/npm/flatpickr@4.6.13/dist/flatpickr.js")
            .SetCdnIntegrity(
                "sha384-6MRMrUEhJMa1+Lu30o5HJn4S0FFOEKnFZFWDfQ5RGRs7aiW7M1I/OpF4G7jxWLsw",
                "sha384-IYsDIK5FMbPNV17G3cUJm5KJ3w2tMrXt2z50E0x5YYGBVi8s2x/MLNW9pvVcITLF")
             .SetVersion("4.6.13");

        _manifest
            .DefineStyle("flatpickr")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/flatpickr/css/flatpickr.min.css",
                "~/CrestApps.OrchardCore.Resources/vendors/flatpickr/css/flatpickr.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/flatpickr@4.6.13/dist/flatpickr.min.css",
                "https://cdn.jsdelivr.net/npm/flatpickr@4.6.13/dist/flatpickr.css")
            .SetCdnIntegrity(
                "sha384-RvLlU3fMPPFGDiYrj9DXiCNv6wPcoG++9Ae/3doVdoh/y8GC6Ya+E4F1oOH+m6w+",
                "sha384-pBNX8OFzxVjH58gMO1pgMOJFt3NsWZTJhTLlyet+psDjRpz6jJhIVoKgvyf7KVJM")
             .SetVersion("4.6.13");

        _manifest
            .DefineScript("flatpickr-culture")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/scripts/flatpickr-culture.min.js",
                "~/CrestApps.OrchardCore.Resources/scripts/flatpickr-culture.js")
            .SetVersion("1.0.2");

        _manifest
            .DefineScript("date-range-picker")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/scripts/date-range-picker.min.js",
                "~/CrestApps.OrchardCore.Resources/scripts/date-range-picker.js")
            .SetDependencies("flatpickr", "flatpickr-culture")
            .SetVersion("1.0.0");

        _manifest
            .DefineScript("tool-instance-parameters")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/scripts/tool-instance-parameters.min.js",
                "~/CrestApps.OrchardCore.Resources/scripts/tool-instance-parameters.js")
            .SetVersion("1.0.0");

        _manifest
            .DefineScript("dompurify")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/dompurify/purify.min.js",
                "~/CrestApps.OrchardCore.Resources/vendors/dompurify/purify.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/dompurify@3.4.16/dist/purify.min.js",
                "https://cdn.jsdelivr.net/npm/dompurify@3.4.16/dist/purify.js")
            .SetCdnIntegrity(
                "sha384-a7SzOxErzJ3ZpQz0zJ32d67dSitNzPcbfybc/ykU9KJhMgZkwqfSxlhhdJRS+XGL",
                "sha384-5ljoOT1W/4gxqpYTLxnNi16VfKAgIDUQeOR8s/vRrSDtBVQmySEejDXexFM6L2+R")
            .SetVersion("3.4.16");

        _manifest
            .DefineScript("highlightjs")
            .SetCdn(
                "https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.12.0/highlight.min.js",
                "https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.12.0/highlight.min.js")
            .SetCdnIntegrity(
                "sha384-wjfDDhOPPdjtva8vWBhWeVprSpmxisEu5aYT3q1JyACqXpdKpo3PWZTMVq24MBix",
                "sha384-wjfDDhOPPdjtva8vWBhWeVprSpmxisEu5aYT3q1JyACqXpdKpo3PWZTMVq24MBix")
            .SetVersion("11.12.0");

        _manifest
            .DefineStyle("highlightjs")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/highlightjs/css/highlightjs.min.css",
                "~/CrestApps.OrchardCore.Resources/vendors/highlightjs/css/highlightjs.css")
            .SetCdn(
                "https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.12.0/styles/github.min.css",
                "https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.12.0/styles/github.css")
            .SetCdnIntegrity(
                "sha384-eFTL69TLRZTkNfYZOLM+G04821K1qZao/4QLJbet1pP4tcF+fdXq/9CdqAbWRl/L",
                "sha384-Uhn9VRzdRxBVYRT2aPFl8ECva7znqyZwWiqpE3v4GTBe8y2XrpwTWZtU1U5vujcN")
            .SetVersion("11.12.0");

        _manifest
            .DefineScript("technical-name-generator")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/crestapps/technical-name-generator.min.js",
                "~/CrestApps.OrchardCore.Resources/vendors/crestapps/technical-name-generator.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/technical-name-generator.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/technical-name-generator.js")
            .SetCdnIntegrity(
                "sha384-eU77/YrH3nUdXISkfRgAu6DYYUqh90P2OAKFelbSMaqzXMaqKiywg7Rw+svTujSx",
                "sha384-pF8a3IT+JHHVdR8k+2XmckVx5e77PWUkgglvqLeyBBuJtRFS6KgUnNoZRpHVyg2H")
            .SetVersion("2.0.0");

        _manifest
            .DefineScript("document-drop-zone")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/crestapps/document-drop-zone.min.js",
                "~/CrestApps.OrchardCore.Resources/vendors/crestapps/document-drop-zone.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/document-drop-zone.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/document-drop-zone.js")
            .SetCdnIntegrity(
                "sha384-PGbT6NVxGpZakG7YLoKE9J7Rxn1+XeVQ3i2I8cZbYVjBBkv5kKBTmF7Nhmoefm1b",
                "sha384-ZUFxZx5ISN4z2zzis3Da+O2tjdxTW/aJoi2jn/WzRQA0YeHiYT+HexPY0ueUGYYk")
            .SetVersion("2.0.0");

        _manifest
            .DefineStyle("document-drop-zone")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/crestapps/document-drop-zone.min.css",
                "~/CrestApps.OrchardCore.Resources/vendors/crestapps/document-drop-zone.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/document-drop-zone.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/document-drop-zone.css")
            .SetCdnIntegrity(
                "sha384-uhWXOg2ne5vxdN8I8NlkJHp4hYJRl0zo/MNL4WIqqKmQ99kyj2X+oa8qgyN1hJz4",
                "sha384-7EkQyDspjDERXdaZJSx2NXgCWPazJJvWSpTjFP87VLIBTNuSbX7mB2Dl1T4mgFo3")
            .SetVersion("2.0.0");

        // Shared marker reader (window.CoreAIChatMarkers), consumed by the AI chat and chat interaction
        // apps. A model writes [doc:N], [fig:N], [chart:{...}] and [tbl:N] rather than addresses, and this
        // turns those labels back into citations, pictures, charts and tables.
        //
        // It depends on chart.js because an expanded [chart:{...}] marker is drawn onto a canvas, and it
        // must load before the chat apps that read it -- which is what their own dependency on it ensures.
        _manifest
            .DefineScript("chat-markers")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/crestapps/chat-markers.min.js",
                "~/CrestApps.OrchardCore.Resources/vendors/crestapps/chat-markers.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/chat-markers.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/chat-markers.js")
            .SetCdnIntegrity(
                "sha384-JXI0CEgnKfLvNX6IBmQP5fSEeABpZuzK6xNWqEOvqMI2tzoAZXbv4Ctlcw53z0A4",
                "sha384-qkbjJ222M8XtPCHtcuKeK5N06UlmuJ0JXRTRtlN+KtvhaaxGIMc+cX0WTUF5LzK6")
            .SetDependencies("chart.js")
            .SetVersion("2.0.0");

        // Shared image carousel (window.CoreAIChatImageCarousel), consumed by the AI chat and chat interaction
        // apps. When an answer shows two or more pictures in a row -- the slides, sheets or pages a document
        // preview returns -- they are stacked into one swipeable carousel instead of running down the
        // conversation one full-size picture after another. The chat apps call it while rendering a message, so
        // it must load before them; their script and the AIChatApp style depend on these two resources.
        _manifest
            .DefineScript("chat-image-carousel")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/crestapps/chat-image-carousel.min.js",
                "~/CrestApps.OrchardCore.Resources/vendors/crestapps/chat-image-carousel.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/chat-image-carousel.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/chat-image-carousel.js")
            .SetCdnIntegrity(
                "sha384-T+6z9iUR5+ubssqsvSb0Ll5GqY7PcshMWDI3pBnfnmTRtvlaEtgQI9fEuHn+7+hC",
                "sha384-qTNJaUP2KLGJG5Xs9ek6Go6Px8gZj+UeV1Q8Mrv4qyoiu+Mm/Yv1x7pCYwUxp+oK")
            .SetVersion("2.0.0");

        _manifest
            .DefineStyle("chat-image-carousel")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/crestapps/chat-image-carousel.min.css",
                "~/CrestApps.OrchardCore.Resources/vendors/crestapps/chat-image-carousel.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/chat-image-carousel.min.css",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/chat-image-carousel.css")
            .SetCdnIntegrity(
                "sha384-XPf/H/4AElPqA5SilokHY367LEiUxxGmbiNqDvTYQ8hES11gMK2jc0mmz/PvIwM0",
                "sha384-DS8vKz5LDVJ+6SYgNE7cqC8nhYxo3Mj28PaoK0mLVo5Z0ayBSlkskgkRGQgNsn3a")
            .SetVersion("2.0.0");

        // Shared realtime (speech-to-speech) audio controller (window.CoreAIRealtime), consumed by the AI
        // chat and chat interaction apps. Registered once here; those features depend on this resource.
        _manifest
            .DefineScript("realtime-audio")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/crestapps/realtime-audio.min.js",
                "~/CrestApps.OrchardCore.Resources/vendors/crestapps/realtime-audio.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/realtime-audio.min.js",
                "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@2.0.0-preview.243/dist/realtime-audio.js")
            .SetCdnIntegrity(
                "sha384-5+ApzBhBQq+bdkHPDBhxX68XJRKN5wyPKejkPGm+MeS+zIAqWSNw5AR1/rzdb4lT",
                "sha384-1qVmWq3Uq8QlMrHIkDzkjcP2Cw0w4jv52gXikK6Gulhb9WnDgAD27v/3yw9/+vQX")
            .SetDependencies("signalr", "dompurify")
            .SetVersion("2.0.0");

        _manifest
            .DefineStyle("intl-tel-input")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/intl-tel-input/css/intlTelInput.min.css",
                "~/CrestApps.OrchardCore.Resources/vendors/intl-tel-input/css/intlTelInput.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/intl-tel-input@29.5.3/dist/css/intlTelInput.min.css",
                "https://cdn.jsdelivr.net/npm/intl-tel-input@29.5.3/dist/css/intlTelInput.css")
            .SetCdnIntegrity(
                "sha384-khdvUrzJNN6Jw9yHboN6430RRijNf+nAlKcZv0a5IrkTjwZpwgJ9fzD3kjbqhOyZ",
                "sha384-BPZgbBF5WUPWbgU/jIcv1eaHN6oD4pp2sywLufUYtHS90m6MqJnH687yeNPC/W/l")
            .SetVersion("29.5.3");

        _manifest
            .DefineScript("intl-tel-input")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/intl-tel-input/js/intlTelInputWithUtils.min.js",
                "~/CrestApps.OrchardCore.Resources/vendors/intl-tel-input/js/intlTelInputWithUtils.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/intl-tel-input@29.5.3/dist/js/intlTelInputWithUtils.min.js",
                "https://cdn.jsdelivr.net/npm/intl-tel-input@29.5.3/dist/js/intlTelInputWithUtils.js")
            .SetCdnIntegrity(
                "sha384-BaDPu1JPrKMlyt17EqDudDB7+Kii733b0P665729w1WD1izDFe9luGQ+afj/2Tpm",
                "sha384-N83jF0rz0F/vFCbfB2Xsk8rEusD+lnnKdj/PlkThBnO117YTpYeHF5wdoNQOczzB")
            .SetVersion("29.5.3");

        // SIP.js is distributed as ES modules only from 0.16+; this is a browser IIFE bundle of
        // sip.js@0.21.2 exposing window.SIP for the soft phone's WebRTC adapter, built from the module's
        // package.json by the "sip.js" group in Assets.json. No public CDN serves this exact bundle, so it
        // is vendored locally only.
        _manifest
            .DefineScript("sip.js")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/sip.js/sip.min.js",
                "~/CrestApps.OrchardCore.Resources/vendors/sip.js/sip.js")
            .SetVersion("0.21.2");

        // The Telnyx WebRTC SDK ships as CommonJS/ESM only; this is a browser IIFE bundle of
        // @telnyx/webrtc@2.27.9 exposing window.TelnyxWebRTC (with TelnyxRTC, Call, TELNYX_ICE_SERVERS, ...)
        // for the soft phone's Telnyx browser audio adapter, built from the module's package.json by the
        // "telnyx-webrtc" group in Assets.json. No public CDN serves this exact bundle, so it is vendored
        // locally only.
        _manifest
            .DefineScript("telnyx-webrtc")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/telnyx-webrtc/telnyx-webrtc.min.js",
                "~/CrestApps.OrchardCore.Resources/vendors/telnyx-webrtc/telnyx-webrtc.js")
            .SetVersion("2.27.9");
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
