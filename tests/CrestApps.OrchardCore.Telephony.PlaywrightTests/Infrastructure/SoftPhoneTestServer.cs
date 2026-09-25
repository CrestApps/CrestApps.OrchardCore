using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCoreSignalRStartup = OrchardCore.SignalR.Startup;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;

/// <summary>
/// Hosts a minimal web application that serves the real soft phone client and maps a test telephony
/// hub, so a browser-driven test can exercise the widget end to end.
/// </summary>
public sealed class SoftPhoneTestServer : IAsyncDisposable
{
    private const string SignalRResourceName = "OrchardCore.SignalR.wwwroot>Scripts>signalr.js";

    /// <summary>
    /// The query string key that runs the page on one of the phone's own media adapters (for example
    /// <c>telnyx-webrtc</c>, against a stand-in provider SDK the test installs) instead of the in-memory one. It is
    /// carried onto the hub URL too, so the hub's credentials name the same adapter.
    /// </summary>
    public const string MediaAdapterQueryKey = "mediaAdapter";

    /// <summary>
    /// The query string key that loads intl-tel-input, the country-flag number input the widget depends on, so the
    /// keypad and the transfer panel read and check numbers as they do in the site. It is left out otherwise (see
    /// <see cref="SoftPhoneAssets.OmittedDependencies"/>).
    /// </summary>
    public const string IntlTelInputQueryKey = "intlTelInput";

    private const string IntlTelInputScriptUrl = "/vendors/intl-tel-input/intlTelInputWithUtils.min.js";
    private const string IntlTelInputStylesheetUrl = "/vendors/intl-tel-input/intlTelInput.min.css";

    private WebApplication _app;

    public string BaseUrl { get; private set; }

    /// <summary>
    /// Gets the voicemails the harness lists on the Voicemail tab.
    /// </summary>
    public TestVoicemailInbox VoicemailInbox => _app.Services.GetRequiredService<TestVoicemailInbox>();

    /// <summary>
    /// Gets the provider the harness hub routes to.
    /// </summary>
    public InMemoryTelephonyProvider Provider => _app.Services.GetRequiredService<InMemoryTelephonyProvider>();

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<InMemoryTelephonyProvider>();
        builder.Services.AddSingleton<TestVoicemailInbox>();
        builder.Services.AddSingleton<BrowserCallLog>();
        builder.Services.AddSingleton<TestTransferService>();
        builder.Services
            .AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

        app.MapHub<TestTelephonyHub>("/telephony");
        app.MapGet("/", (HttpContext context) =>
        {
            var attendedTransfer = context.Request.Query.ContainsKey("attendedTransfer");

            if (attendedTransfer)
            {
                context.RequestServices.GetRequiredService<InMemoryTelephonyProvider>().EnableAttendedTransfer();
            }

            return Results.Content(
                BuildHtml(
                    context.Request.Query.ContainsKey("browserAudio"),
                    context.Request.Query.ContainsKey("embedded"),
                    context.Request.Query["answerCallId"],
                    voicemail: context.Request.Query.ContainsKey("voicemail"),
                    styled: context.Request.Query.ContainsKey("styled"),
                    attendedTransfer: attendedTransfer,
                    mediaAdapter: context.Request.Query[MediaAdapterQueryKey],
                    browserMediaAdapterName: context.RequestServices.GetRequiredService<InMemoryTelephonyProvider>().BrowserMediaAdapterName,
                    transferService: context.Request.Query.ContainsKey("transferService"),
                    intlTelInput: context.Request.Query.ContainsKey(IntlTelInputQueryKey),
                    widget: context.Request.Query.ContainsKey("widget"),
                    dark: context.Request.Query.ContainsKey("dark"),
                    host: context.Request.Query.ContainsKey("host")),
                "text/html; charset=utf-8");
        });

        // The country-flag number input the widget depends on, served from the Resources module's vendored copy for a
        // page that asks for it (?intlTelInput).
        app.MapGet(IntlTelInputScriptUrl, () => ServeVendorAsset("js/intlTelInputWithUtils.min.js", "application/javascript"));
        app.MapGet(IntlTelInputStylesheetUrl, () => ServeVendorAsset("css/intlTelInput.min.css", "text/css"));

        // The Contact Center's soft-phone transfer endpoints, answered in memory.
        app.Services.GetRequiredService<TestTransferService>().Map(app);

        // The voicemail delete endpoint, answering a refusal the way the site's cookie authentication did before
        // the endpoint wrote its own 403: a redirect to a sign-in page that itself answers 200.
        app.MapPost("/voicemail/{interactionId}/delete", async (string interactionId, TestVoicemailInbox inbox) =>
            await inbox.TryDeleteAsync(interactionId)
                ? Results.Ok()
                : Results.Redirect("/login"));
        app.MapGet("/login", () => Results.Content("<!DOCTYPE html><html><body>Sign in</body></html>", "text/html; charset=utf-8"));

        // The module's own scripts, from its built wwwroot, at the URLs its resource manifest gives them.
        app.MapGet(SoftPhoneAssets.ModuleUrlPrefix + "{**path}", (string path) => ServeModuleAsset(path));
        app.MapGet(SoftPhoneAssets.SignalRUrl, ServeSignalRAsset);

        await app.StartAsync();

        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses;
        BaseUrl = addresses.First();
        _app = app;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    /// <summary>
    /// Gets the script URLs the harness page loads, in order: the widget's resource dependencies, resolved from the
    /// module's resource manifest (see <see cref="SoftPhoneAssets"/>).
    /// </summary>
    public static IReadOnlyList<string> ScriptUrls { get; } = SoftPhoneAssets.ResolveHarnessScriptUrls();

    /// <summary>
    /// Gets the URL of the soft phone's own stylesheet, which a styled harness page (<c>?styled</c>) links so a test can
    /// check how the phone's panels are laid out and not only what they contain.
    /// </summary>
    public static string StylesheetUrl { get; } = SoftPhoneAssets.ModuleUrlPrefix + "styles/soft-phone.css";

    // Only the files the harness page loads are served, looked up by the URL the manifest gives them, so a request's
    // path never becomes a file path.
    private static readonly Dictionary<string, string> _moduleAssets = ScriptUrls
        .Append(StylesheetUrl)
        .Where(url => url.StartsWith(SoftPhoneAssets.ModuleUrlPrefix, StringComparison.Ordinal))
        .Select(url => url[SoftPhoneAssets.ModuleUrlPrefix.Length..])
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToDictionary(relative => relative, SoftPhoneAssets.ResolveModuleFile, StringComparer.OrdinalIgnoreCase);

    // The widget's Voicemail view, as SoftPhoneWidget.cshtml renders it.
    private const string VoicemailViewMarkup = """
        <div class="telephony-soft-phone__view telephony-soft-phone__voicemail" data-telephony-view="voicemail" hidden>
            <div data-telephony-voicemail-player hidden>
                <div data-telephony-voicemail-player-info></div>
                <audio data-telephony-voicemail-audio controls preload="none"></audio>
            </div>
            <div data-telephony-voicemail-toolbar hidden>
                <label><input type="checkbox" data-telephony-voicemail-select-all><span>Select all</span></label>
                <button type="button" data-telephony-voicemail-delete disabled>Delete</button>
            </div>
            <div data-telephony-voicemail-error role="alert" hidden></div>
            <div data-telephony-voicemail-list></div>
        </div>
        """;

    private static IResult ServeModuleAsset(string path)
    {
        if (string.IsNullOrEmpty(path) || !_moduleAssets.TryGetValue(path, out var file) || !File.Exists(file))
        {
            return Results.NotFound();
        }

        var contentType = file.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ? "text/css" : "application/javascript";

        return Results.File(file, contentType);
    }

    private static IResult ServeVendorAsset(string relativePath, string contentType)
    {
        var file = Path.GetFullPath(Path.Combine(
            SoftPhoneAssets.ModuleDirectory,
            "..",
            "CrestApps.OrchardCore.Resources",
            "wwwroot",
            "vendors",
            "intl-tel-input",
            relativePath));

        return File.Exists(file) ? Results.File(file, contentType) : Results.NotFound();
    }

    private static IResult ServeSignalRAsset()
    {
        var stream = typeof(OrchardCoreSignalRStartup).Assembly.GetManifestResourceStream(SignalRResourceName);
        if (stream is null)
        {
            return Results.NotFound();
        }

        return Results.Stream(stream, "application/javascript");
    }

    private static string BuildHtml(bool browserAudio, bool embedded = false, string answerCallId = null, bool voicemail = false, bool styled = false, bool attendedTransfer = false, string mediaAdapter = null, string browserMediaAdapterName = "in-memory", bool transferService = false, bool intlTelInput = false, bool widget = false, bool dark = false, bool host = false)
    {
        var adapterName = !string.IsNullOrEmpty(mediaAdapter)
            ? mediaAdapter
            : string.IsNullOrEmpty(browserMediaAdapterName) ? "in-memory" : browserMediaAdapterName;
        var config = new Dictionary<string, object>
        {
            ["hubUrl"] = string.IsNullOrEmpty(mediaAdapter)
                ? "/telephony"
                : $"/telephony?{MediaAdapterQueryKey}={Uri.EscapeDataString(mediaAdapter)}",
            ["capabilities"] = attendedTransfer ? 2047 | 2048 : 2047,
            ["audioCapabilities"] = browserAudio ? 1 : 2,
            ["audioMode"] = browserAudio ? 1 : 2,
            ["browserMediaAdapterName"] = browserAudio ? adapterName : null,
            ["strings"] = new Dictionary<string, string>
            {
                ["idle"] = "Ready",
                ["connecting"] = "Connecting...",
                ["ringing"] = "Ringing...",
                ["connected"] = "In call",
                ["onHold"] = "On hold",
                ["disconnected"] = "Call ended",
                ["failed"] = "Call failed",
                ["disconnectedHub"] = "Disconnected",
                ["invalidNumber"] = "Enter a phone number to call.",
                ["transfer"] = "Transfer",
                ["keypad"] = "Keypad",
                ["directoryEmpty"] = "No directory entries are available.",
                ["activeCalls"] = "Active calls",
                ["selectCallsToMerge"] = "Select two calls to conference.",
                ["conference"] = "Conference selected calls",
                ["disconnectAll"] = "Disconnect all calls",
                ["microphoneUnavailable"] = "The microphone is unavailable.",
                ["browserAudioUnavailable"] = "The browser audio adapter is unavailable.",
            },
        };

        if (transferService)
        {
            // As SoftPhoneWidget.cshtml renders it when the Contact Center publishes its transfer endpoints.
            config["antiForgeryToken"] = TestTransferService.AntiForgeryToken;
            config["transferService"] = new Dictionary<string, string>
            {
                ["targetsUrl"] = TestTransferService.TargetsUrl,
                ["transferUrl"] = TestTransferService.TransferUrl,
                ["consultUrl"] = TestTransferService.ConsultUrl,
                ["consultCompleteUrl"] = TestTransferService.ConsultCompleteUrl,
                ["consultCancelUrl"] = TestTransferService.ConsultCancelUrl,
            };
        }

        if (intlTelInput)
        {
            // As the site configures it: the country the keypad starts on.
            config["defaultCountryCode"] = "us";
        }

        if (voicemail)
        {
            config["voicemailDeleteEnabled"] = true;
            config["voicemailDeleteUrlTemplate"] = "/voicemail/__INTERACTION_ID__/delete";
        }

        var configJson = JsonSerializer.Serialize(config);

        // When the standalone /softphone page hosts the phone (the browser extension case), the real Index view
        // wraps the widget in an element flagged data-softphone-embedded and, on an answer handoff, carries the
        // requested call id in data-softphone-answer-call-id. Reproduce that wrapper so the client's embedded and
        // auto-answer behavior can be exercised against the real script.
        var encodedAnswerCallId = System.Net.WebUtility.HtmlEncode(answerCallId ?? string.Empty);
        var embeddedOpen = embedded
            ? $"<div class=\"softphone-standalone\" data-softphone-embedded=\"true\" data-softphone-answer-call-id=\"{encodedAnswerCallId}\">"
            : string.Empty;
        var embeddedClose = embedded ? "</div>" : string.Empty;
        var scriptUrls = intlTelInput ? ScriptUrls.Prepend(IntlTelInputScriptUrl) : ScriptUrls;
        var scripts = string.Join(Environment.NewLine + "    ", scriptUrls.Select(url => $"<script src=\"{url}\"></script>"));

        // The real widget's markup, floating or wrapped as the standalone page wraps it (see SoftPhoneWidgetPage).
        if (widget)
        {
            return SoftPhoneWidgetPage.Build(configJson, scripts, StylesheetUrl, standalone: embedded, dark, host);
        }
        // A styled page gives the root the widget's class, so the stylesheet's variables apply, but keeps it in the page
        // flow rather than floating in a corner, so a test can measure what it draws.
        var stylesheet = styled
            ? $"<link rel=\"stylesheet\" href=\"{StylesheetUrl}\" /><style>#telephony-soft-phone.telephony-soft-phone {{ position: static; }}</style>"
            : string.Empty;

        if (intlTelInput)
        {
            stylesheet = $"<link rel=\"stylesheet\" href=\"{IntlTelInputStylesheetUrl}\" />" + stylesheet;
        }
        var rootClass = styled ? " class=\"telephony-soft-phone\"" : string.Empty;

        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8" />
            <title>Soft Phone Test</title>
            {{stylesheet}}
        </head>
        <body>
            {{embeddedOpen}}
            <div id="telephony-soft-phone"{{rootClass}} data-config='{{configJson}}'>
                <button type="button" data-telephony-toggle><i class="fa-solid fa-phone" data-telephony-toggle-icon></i></button>
                <div data-telephony-panel hidden>
                    <audio data-telephony-remote-audio autoplay></audio>
                    <span data-telephony-status>Ready</span>
                    <button type="button" data-telephony-close>Close</button>
                    <div data-telephony-unavailable hidden><span data-telephony-unavailable-text></span></div>
                    <div data-telephony-connect-panel hidden><button type="button" data-telephony-connect>Connect</button></div>
                    <div data-telephony-body>
                        <div data-telephony-view="keypad">
                            <input type="tel" data-telephony-number />
                            <button type="button" data-telephony-dial-mode-toggle aria-pressed="false"><span data-telephony-dial-mode-label>Dial extension</span></button>
                            <div data-telephony-extension-hint hidden></div>
                            <div class="telephony-soft-phone__keypad-results" data-telephony-keypad-results hidden></div>
                            <div data-telephony-error hidden></div>
                            <div class="telephony-soft-phone__active-calls" data-telephony-active-calls hidden>
                                <div class="telephony-soft-phone__active-calls-list" data-telephony-active-calls-list></div>
                            </div>
                            <div class="telephony-soft-phone__transfer-panel" data-telephony-transfer-panel hidden></div>
                            <div data-telephony-keypad-panel>
                                <button type="button" data-telephony-key="1">1</button>
                                <button type="button" data-telephony-key="2">2</button>
                            </div>
                            <button type="button" data-telephony-dial>Call</button>
                            <button type="button" data-telephony-hold hidden>Hold</button>
                            <button type="button" data-telephony-resume hidden>Resume</button>
                            <button type="button" data-telephony-mute hidden>Mute</button>
                            <button type="button" data-telephony-unmute hidden>Unmute</button>
                            <button type="button" data-telephony-transfer hidden>
                                <i data-telephony-transfer-icon></i>
                                <span data-telephony-transfer-label>Transfer</span>
                            </button>
                            <button type="button" data-telephony-keypad-toggle aria-pressed="false" hidden>Keypad</button>
                            <button type="button" data-telephony-add-call hidden>Add call</button>
                            <button type="button" data-telephony-add-call-cancel hidden>Back to call</button>
                            <button type="button" data-telephony-hangup hidden>Hangup</button>
                            <button type="button" data-telephony-hangup-all hidden>Disconnect all</button>
                        </div>
                        <div data-telephony-incoming hidden>
                            <div data-telephony-incoming-caller></div>
                            <div data-telephony-incoming-queue hidden></div>
                            <div data-telephony-incoming-cards hidden></div>
                            <button type="button" data-telephony-incoming-answer>Answer</button>
                            <button type="button" data-telephony-incoming-voicemail hidden>Voicemail</button>
                            <button type="button" data-telephony-incoming-ignore>Ignore</button>
                        </div>
                        <div data-telephony-view="history" data-telephony-history hidden>
                            <div data-telephony-history-list></div>
                        </div>
                        {{(voicemail ? VoicemailViewMarkup : string.Empty)}}
                        <div data-telephony-view="contact-center" hidden>
                            <span>Contact Center Work</span>
                        </div>
                    </div>
                    <div data-telephony-footer hidden>
                        <button type="button" data-telephony-tab="keypad" aria-selected="true">Keypad</button>
                        <button type="button" data-telephony-tab="history" aria-selected="false">Recent</button>
                        {{(voicemail ? "<button type=\"button\" data-telephony-tab=\"voicemail\" aria-selected=\"false\">Voicemail</button>" : string.Empty)}}
                        <button type="button" data-telephony-tab="contact-center" aria-selected="false">Work</button>
                    </div>
                </div>
            </div>
            {{embeddedClose}}
            {{scripts}}
        </body>
        </html>
        """;
    }
}
