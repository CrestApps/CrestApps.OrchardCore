using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;

/// <summary>
/// Hosts a minimal web application that serves the real soft phone client and maps a test telephony
/// hub, so a browser-driven test can exercise the widget end to end.
/// </summary>
public sealed class SoftPhoneTestServer : IAsyncDisposable
{
    private WebApplication _app;

    public string BaseUrl { get; private set; }

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();

        builder.Services.AddSingleton<InMemoryTelephonyProvider>();
        builder.Services
            .AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

        app.MapHub<TestTelephonyHub>("/telephony");
        app.MapGet("/", () => Results.Content(BuildHtml(), "text/html; charset=utf-8"));
        app.MapGet("/soft-phone.js", () => ServeAsset("soft-phone.js"));
        app.MapGet("/signalr.js", () => ServeAsset("signalr.js"));

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

    private static IResult ServeAsset(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assets", name);

        return Results.File(path, "application/javascript");
    }

    private static string BuildHtml()
    {
        var config = new Dictionary<string, object>
        {
            ["hubUrl"] = "/telephony",
            ["capabilities"] = 511,
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
                ["transferPrompt"] = "Transfer to number",
                ["mergePrompt"] = "Second call id to merge",
            },
        };

        var configJson = JsonSerializer.Serialize(config);

        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8" />
            <title>Soft Phone Test</title>
        </head>
        <body>
            <div id="telephony-soft-phone" data-config='{{configJson}}'>
                <button type="button" data-telephony-toggle>Phone</button>
                <div data-telephony-panel hidden>
                    <span data-telephony-status>Ready</span>
                    <button type="button" data-telephony-close>Close</button>
                    <div data-telephony-unavailable hidden><span data-telephony-unavailable-text></span></div>
                    <div data-telephony-connect-panel hidden><button type="button" data-telephony-connect>Connect</button></div>
                    <div data-telephony-body>
                        <div data-telephony-view="keypad">
                            <input type="tel" data-telephony-number />
                            <div data-telephony-peer></div>
                            <div data-telephony-error hidden></div>
                            <button type="button" data-telephony-dial>Call</button>
                            <button type="button" data-telephony-hold hidden>Hold</button>
                            <button type="button" data-telephony-resume hidden>Resume</button>
                            <button type="button" data-telephony-mute hidden>Mute</button>
                            <button type="button" data-telephony-unmute hidden>Unmute</button>
                            <button type="button" data-telephony-transfer hidden>Transfer</button>
                            <button type="button" data-telephony-merge hidden>Merge</button>
                            <button type="button" data-telephony-hangup hidden>Hangup</button>
                        </div>
                        <div data-telephony-view="history" data-telephony-history hidden>
                            <div data-telephony-history-list></div>
                        </div>
                    </div>
                    <div data-telephony-footer hidden>
                        <button type="button" data-telephony-tab="keypad" aria-selected="true">Keypad</button>
                        <button type="button" data-telephony-tab="history" aria-selected="false">Recent</button>
                    </div>
                </div>
            </div>
            <script src="/signalr.js"></script>
            <script src="/soft-phone.js"></script>
        </body>
        </html>
        """;
    }
}
