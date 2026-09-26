using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;

/// <summary>
/// The Contact Center supervisor surfaces, served from the Contact Center module's built scripts: the live dashboard
/// page (its state endpoint and intervention endpoints answered in memory), and the engagement banner a supervisor's
/// soft phone shows (its hub replaced by a stand-in the test drives).
/// </summary>
public sealed class SupervisorHarness
{
    /// <summary>
    /// The page that renders the live dashboard.
    /// </summary>
    public const string DashboardUrl = "/supervisor-dashboard";

    /// <summary>
    /// The query string key that puts the supervisor's engagement banner in the soft phone page.
    /// </summary>
    public const string SupervisorPhoneQueryKey = "supervisorPhone";

    private const string ModuleUrlPrefix = "/CrestApps.OrchardCore.ContactCenter/";
    private const string ModuleRelativePath = "../CrestApps.OrchardCore.ContactCenter";
    private const string StateUrl = "/dashboard/state";
    private const string ActionUrlPrefix = "/dashboard/";

    private static readonly string[] _servedFiles =
    [
        "scripts/supervisor-dashboard.js",
        "scripts/contact-center-supervisor-phone.js",
        "styles/contact-center-workspace.css",
    ];

    private static readonly string[] _actions = ["engage", "stop", "switch", "takeover", "end-call", "transfer", "recording", "agent-state", "message"];

    /// <summary>
    /// Gets or sets the state the dashboard's state endpoint answers, as JSON.
    /// </summary>
    public JsonObject State { get; set; } = new();

    /// <summary>
    /// Gets every intervention the page posted, in order: the action and its form.
    /// </summary>
    public ConcurrentQueue<(string Action, Dictionary<string, string> Form)> Posts { get; } = new();

    /// <summary>
    /// Maps the harness's routes.
    /// </summary>
    public void Map(WebApplication app)
    {
        app.MapGet(DashboardUrl, () => Results.Content(BuildDashboardHtml(), "text/html; charset=utf-8"));
        app.MapGet(StateUrl, () => Results.Content(State.ToJsonString(), "application/json"));

        foreach (var action in _actions)
        {
            app.MapPost(ActionUrlPrefix + action, async (HttpContext context) =>
            {
                var form = await context.Request.ReadFormAsync();
                Posts.Enqueue((action, form.ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal)));

                return Results.Json(new { succeeded = true });
            });
        }

        app.MapGet(ModuleUrlPrefix + "{**path}", (string path) =>
        {
            if (!_servedFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                return Results.NotFound();
            }

            var file = Path.GetFullPath(Path.Combine(SoftPhoneAssets.ModuleDirectory, ModuleRelativePath, "wwwroot", path));

            return File.Exists(file)
                ? Results.File(file, path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ? "text/css" : "application/javascript")
                : Results.NotFound();
        });
    }

    /// <summary>
    /// The banner markup the Contact Center renders into a supervisor's soft phone (ContactCenterSupervisorMonitor.Banner.cshtml),
    /// the stand-in hub, and the script.
    /// </summary>
    public static string SupervisorPhoneMarkup()
        => """
            <div class="telephony-soft-phone__monitor" data-cc-monitor-banner data-cc-monitor-hub-url="/contact-center-hub" data-cc-monitor-strings="{}" role="region" aria-label="Supervisor monitoring" aria-live="polite" hidden></div>
            """;

    /// <summary>
    /// The scripts the supervisor's soft phone loads for the banner: a stand-in Contact Center hub the test drives through
    /// <c>window.fakeCcHub</c>, then the Contact Center's supervisor-phone bundle.
    /// </summary>
    public static string SupervisorPhoneScripts()
        => """
            <script>
            (function () {
                var handlers = {};
                var hub = window.fakeCcHub = { invocations: [], results: {} };
                hub.emit = function (name, payload) { (handlers[name] || []).forEach(function (handler) { handler(payload); }); };
                window.contactCenterRealTime = {
                    connect: function () {
                        return {
                            started: Promise.resolve(),
                            connection: {
                                on: function (name, handler) { (handlers[name] = handlers[name] || []).push(handler); },
                                invoke: function (method) {
                                    var args = Array.prototype.slice.call(arguments, 1);
                                    hub.invocations.push({ method: method, args: args });
                                    return Promise.resolve(hub.results[method] || { succeeded: true });
                                }
                            }
                        };
                    }
                };
            }());
            </script>
            <script src="/CrestApps.OrchardCore.ContactCenter/scripts/contact-center-supervisor-phone.js"></script>
            """;

    private static string BuildDashboardHtml()
    {
        var config = new Dictionary<string, object>
        {
            ["stateUrl"] = StateUrl,
            ["engageUrl"] = ActionUrlPrefix + "engage",
            ["antiForgeryToken"] = "token",
            ["interventionUrls"] = new Dictionary<string, string>
            {
                ["stop"] = ActionUrlPrefix + "stop",
                ["switch"] = ActionUrlPrefix + "switch",
                ["takeover"] = ActionUrlPrefix + "takeover",
                ["endCall"] = ActionUrlPrefix + "end-call",
                ["transfer"] = ActionUrlPrefix + "transfer",
                ["recording"] = ActionUrlPrefix + "recording",
                ["agentState"] = ActionUrlPrefix + "agent-state",
                ["message"] = ActionUrlPrefix + "message",
            },
            ["strings"] = new Dictionary<string, string>(),
        };

        var telephonyClient = SoftPhoneTestServer.ScriptUrls.Single(url => url.Contains("telephony-client", StringComparison.Ordinal));

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <title>Supervisor Dashboard Test</title>
                <link rel="stylesheet" href="{{ModuleUrlPrefix}}styles/contact-center-workspace.css" />
            </head>
            <body>
                <div class="cc-workspace cc-dashboard" data-cc-dashboard data-config='{{JsonSerializer.Serialize(config)}}'>
                    <span class="cc-connection" data-cc-connection role="status"></span>
                    <div class="cc-error" data-cc-error role="alert" hidden></div>
                    <div data-cc-quality-alerts></div>
                    <div data-cc-summary></div>
                    <div data-cc-tiles></div>
                    <div data-cc-intervention-panel hidden></div>
                    <div data-cc-intervention-status role="status"></div>
                    <div class="cc-board" data-cc-board role="status" aria-live="polite"></div>
                </div>
                <script src="{{telephonyClient}}"></script>
                <script src="{{ModuleUrlPrefix}}scripts/supervisor-dashboard.js"></script>
            </body>
            </html>
            """;
    }
}
