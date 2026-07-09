using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Asterisk.Web.Services;

/// <summary>
/// Subscribes to the configured Asterisk Stasis application and forwards simulator-originated calls
/// into the Orchard Contact Center ingress endpoint when the matching channel enters Stasis.
/// </summary>
public sealed class AsteriskStasisEventForwarderService : BackgroundService
{
    private readonly AsteriskInboundSimulationCoordinator _coordinator;
    private readonly AsteriskDashboardBroadcastService _dashboardBroadcastService;
    private readonly AsteriskWebOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskStasisEventForwarderService"/> class.
    /// </summary>
    /// <param name="coordinator">The pending simulation coordinator.</param>
    /// <param name="dashboardBroadcastService">The live dashboard broadcaster.</param>
    /// <param name="options">The configured sample app options.</param>
    /// <param name="logger">The logger.</param>
    public AsteriskStasisEventForwarderService(
        AsteriskInboundSimulationCoordinator coordinator,
        AsteriskDashboardBroadcastService dashboardBroadcastService,
        IOptions<AsteriskWebOptions> options,
        ILogger<AsteriskStasisEventForwarderService> logger)
    {
        _coordinator = coordinator;
        _dashboardBroadcastService = dashboardBroadcastService;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!AsteriskAriConnectionUtilities.IsConfigured(_options) ||
                string.IsNullOrWhiteSpace(_options.AsteriskApplicationName))
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

                continue;
            }

            try
            {
                await ListenAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning(ex, "The Asterisk Stasis listener disconnected. Reconnecting shortly.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The Asterisk Stasis listener failed unexpectedly.");
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(AsteriskAriConnectionUtilities.CreateEventsUri(_options), cancellationToken);

        var buffer = new byte[8 * 1024];

        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                result = await socket.ReceiveAsync(buffer, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", cancellationToken);

                    return;
                }

                message.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            await HandleEventAsync(Encoding.UTF8.GetString(message.ToArray()), cancellationToken);
        }
    }

    private async Task HandleEventAsync(string payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        if (!root.TryGetProperty("type", out var typeElement))
        {
            return;
        }

        _dashboardBroadcastService.RequestRefresh();

        if (!string.Equals(typeElement.GetString(), "StasisStart", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!root.TryGetProperty("channel", out var channelElement) ||
            !channelElement.TryGetProperty("id", out var channelIdElement))
        {
            return;
        }

        var simulationKey = TryGetSimulationKey(root);

        if (string.IsNullOrWhiteSpace(simulationKey))
        {
            return;
        }

        await _coordinator.TryDispatchAsync(simulationKey, channelIdElement.GetString(), cancellationToken);
    }

    private static string TryGetSimulationKey(JsonElement root)
    {
        if (!root.TryGetProperty("args", out var argsElement) || argsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var arg in argsElement.EnumerateArray())
        {
            var value = arg.GetString();

            if (!string.IsNullOrWhiteSpace(value) &&
                value.StartsWith("sim:", StringComparison.OrdinalIgnoreCase))
            {
                return value["sim:".Length..];
            }
        }

        return null;
    }
}
