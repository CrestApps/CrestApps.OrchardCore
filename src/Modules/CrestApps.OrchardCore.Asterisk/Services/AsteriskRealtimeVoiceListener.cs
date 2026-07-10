using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskRealtimeVoiceListener : IAsyncDisposable
{
    private readonly ILogger<AsteriskRealtimeVoiceListener> _logger;
    private readonly Lock _lock = new();
    private CancellationTokenSource _listenerCancellationTokenSource;
    private Task _listenerTask;

    public AsteriskRealtimeVoiceListener(ILogger<AsteriskRealtimeVoiceListener> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(IReadOnlyList<AsteriskResolvedSettings> listeners)
    {
        ArgumentNullException.ThrowIfNull(listeners);

        lock (_lock)
        {
            if (_listenerTask is not null)
            {
                return Task.CompletedTask;
            }

            _listenerCancellationTokenSource = new CancellationTokenSource();
            _listenerTask = RunAsync(listeners, _listenerCancellationTokenSource.Token);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        Task listenerTask;
        CancellationTokenSource cancellationTokenSource;

        lock (_lock)
        {
            listenerTask = _listenerTask;
            cancellationTokenSource = _listenerCancellationTokenSource;
            _listenerTask = null;
            _listenerCancellationTokenSource = null;
        }

        if (cancellationTokenSource is null || listenerTask is null)
        {
            return;
        }

        await cancellationTokenSource.CancelAsync();

        try
        {
            await listenerTask;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cancellationTokenSource.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private async Task RunAsync(IReadOnlyList<AsteriskResolvedSettings> listeners, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (listeners.Count == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

                continue;
            }

            try
            {
                await Task.WhenAll(listeners.Select(listener => ListenAsync(listener, cancellationToken)));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The Asterisk real-time voice listener failed unexpectedly.");
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task ListenAsync(AsteriskResolvedSettings settings, CancellationToken cancellationToken)
    {
        if (!AsteriskSettingsUtilities.HasRequiredConfiguration(settings))
        {
            return;
        }

        using var socket = new ClientWebSocket();
        var eventsUri = AsteriskSettingsUtilities.CreateEventsUri(settings);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Connecting the Asterisk real-time voice listener for provider {ProviderName} to {EventsUri}.",
                settings.ProviderName,
                eventsUri);
        }

        await socket.ConnectAsync(eventsUri, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Connected the Asterisk real-time voice listener for provider {ProviderName}.",
                settings.ProviderName);
        }

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
                    _logger.LogWarning(
                        "The Asterisk real-time voice listener for provider {ProviderName} received a close frame. Status={Status}, Description={Description}.",
                        settings.ProviderName,
                        result.CloseStatus,
                        result.CloseStatusDescription);

                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", cancellationToken);

                    return;
                }

                await message.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken);
            }
            while (!result.EndOfMessage);

            var payload = Encoding.UTF8.GetString(message.ToArray());

            await DispatchAsync(settings.ProviderName, payload, cancellationToken);
        }
    }

    private async Task DispatchAsync(string providerName, string payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        if (!AsteriskRealtimeVoiceEventMapper.TryMap(providerName, payload, out var voiceEvent))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Ignored an Asterisk real-time payload for provider {ProviderName} because it did not map to a voice-state update.",
                    providerName);
            }

            return;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Received Asterisk real-time event {EventType} for provider {ProviderName} call {CallId}; mapped to state {State}.",
                voiceEvent.EventType,
                voiceEvent.ProviderName,
                voiceEvent.CallId,
                voiceEvent.State);
        }

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<AsteriskRealtimeVoiceEventDispatcher>();
            await dispatcher.HandleAsync(voiceEvent, cancellationToken);
        });
    }
}
