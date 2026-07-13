using CrestApps.OrchardCore.Asterisk.Web;
using CrestApps.OrchardCore.Asterisk.Web.Hubs;
using CrestApps.OrchardCore.Asterisk.Web.Models;
using CrestApps.OrchardCore.Asterisk.Web.Services;
using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddHttpClient();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<AsteriskWebOptions>(builder.Configuration.GetSection("AsteriskWeb"));
builder.Services.AddSingleton<OrchardSignInClient>();
builder.Services.AddSingleton<AsteriskInboundSimulationCoordinator>();
builder.Services.AddSingleton<InboundCallSimulatorService>();
builder.Services.AddSingleton<AsteriskTwoPartyCallSimulatorService>();
builder.Services.AddSingleton<AsteriskDiagnosticsService>();
builder.Services.AddSingleton<AsteriskDashboardBroadcastService>();
builder.Services.AddHostedService(static serviceProvider => serviceProvider.GetRequiredService<AsteriskDashboardBroadcastService>());
builder.Services.AddHostedService<AsteriskStasisEventForwarderService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.MapGet(
    "/api/asterisk/dashboard",
    async Task<Ok<AsteriskDiagnosticsSnapshot>> (AsteriskDiagnosticsService diagnosticsService, CancellationToken cancellationToken) =>
        TypedResults.Ok(await diagnosticsService.GetSnapshotAsync(cancellationToken)));
app.MapDelete(
    "/api/asterisk/channels/{channelId}",
    async Task<Results<NoContent, BadRequest<string>>> (
        string channelId,
        AsteriskDiagnosticsService diagnosticsService,
        AsteriskDashboardBroadcastService dashboardBroadcastService,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return TypedResults.BadRequest("A channel id is required.");
        }

        await diagnosticsService.DisconnectChannelAsync(channelId, cancellationToken);
        dashboardBroadcastService.RequestRefresh("dashboard disconnect action");

        return TypedResults.NoContent();
    });
app.MapDelete(
    "/api/asterisk/bridges/{bridgeId}",
    async Task<Results<NoContent, BadRequest<string>>> (
        string bridgeId,
        AsteriskDiagnosticsService diagnosticsService,
        AsteriskDashboardBroadcastService dashboardBroadcastService,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(bridgeId))
        {
            return TypedResults.BadRequest("A bridge id is required.");
        }

        await diagnosticsService.DisconnectBridgeAsync(bridgeId, cancellationToken);
        dashboardBroadcastService.RequestRefresh("dashboard bridge disconnect action");

        return TypedResults.NoContent();
    });
app.MapPost(
    "/api/asterisk/simulations/two-party",
    async Task<Results<Ok<TwoPartyCallSimulationResult>, BadRequest<string>>> (
        TwoPartyCallSimulationInputModel input,
        AsteriskTwoPartyCallSimulatorService simulatorService,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(input.PartyAEndpoint) ||
            string.IsNullOrWhiteSpace(input.PartyBEndpoint))
        {
            return TypedResults.BadRequest("Both Asterisk party endpoints are required.");
        }

        return TypedResults.Ok(await simulatorService.SimulateAsync(input, cancellationToken));
    });
app.MapHub<AsteriskDashboardHub>("/hubs/asterisk-dashboard");
app.MapRazorPages();

await app.RunAsync();
