using System.Net.WebSockets;
using System.Text;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskRealtimeVoiceListener : IAsyncDisposable
{
    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger<AsteriskRealtimeVoiceListener> _logger;
    private readonly Lock _lock = new();
    private CancellationTokenSource _listenerCancellationTokenSource;
    private Task _listenerTask;

    public AsteriskRealtimeVoiceListener(
        IShellHost shellHost,
        ShellSettings shellSettings,
        ILogger<AsteriskRealtimeVoiceListener> logger)
    {
        _shellHost = shellHost;
        _shellSettings = shellSettings;
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
        if (listeners.Count == 0)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            return;
        }

        await Task.WhenAll(listeners.Select(listener => RunListenerAsync(listener, cancellationToken)));
    }

    private async Task RunListenerAsync(AsteriskResolvedSettings settings, CancellationToken cancellationToken)
    {
        if (!AsteriskSettingsUtilities.HasRequiredConfiguration(settings))
        {
            return;
        }

        var failureCount = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ListenAsync(settings, cancellationToken);
                failureCount = 0;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                failureCount++;

                _logger.LogError(
                    ex,
                    "The Asterisk real-time voice listener for provider {ProviderName} failed unexpectedly.",
                    settings.ProviderName);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(GetReconnectDelay(failureCount), cancellationToken);
            }
        }
    }

    private async Task ListenAsync(AsteriskResolvedSettings settings, CancellationToken cancellationToken)
    {
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

        await ReconcileAsync(settings.ProviderName, cancellationToken);

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

            // A single malformed or unroutable event, or a transient tenant-scope failure while the shell is
            // reloading, must never tear down the live event stream. Isolate each dispatch so the socket keeps
            // receiving; any missed state change is still reconciled by the periodic provider-truth sweep.
            try
            {
                await DispatchAsync(settings.ProviderName, payload, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to dispatch an Asterisk real-time payload for provider {ProviderName}; the listener will continue processing subsequent events.",
                    settings.ProviderName);
            }
        }
    }

    private static TimeSpan GetReconnectDelay(int failureCount)
    {
        var exponent = Math.Min(Math.Max(failureCount, 0), 5);
        var seconds = Math.Min(Math.Pow(2, exponent), 30);
        var jitter = 0.8 + (Random.Shared.NextDouble() * 0.4);

        return TimeSpan.FromSeconds(seconds * jitter);
    }

    private async Task ReconcileAsync(string providerName, CancellationToken cancellationToken)
    {
        await ExecuteInTenantScopeAsync(async serviceProvider =>
        {
            var contactCenterSynchronizationService = serviceProvider
                .GetServices<IProviderCallStateSynchronizationService>()
                .FirstOrDefault();

            if (contactCenterSynchronizationService is not null)
            {
                try
                {
                    await contactCenterSynchronizationService.ReconcileProviderInteractionsAsync(providerName, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Contact Center provider-state reconciliation failed after reconnecting the Asterisk real-time listener for provider {ProviderName}.",
                        providerName);
                }
            }

            var telephonySynchronizationService = serviceProvider
                .GetServices<ITelephonyInteractionSynchronizationService>()
                .FirstOrDefault();

            if (telephonySynchronizationService is not null)
            {
                try
                {
                    await telephonySynchronizationService.ReconcileProviderInteractionsAsync(providerName, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Telephony interaction reconciliation failed after reconnecting the Asterisk real-time listener for provider {ProviderName}.",
                        providerName);
                }
            }
        });
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

        await ExecuteInTenantScopeAsync(async serviceProvider =>
        {
            var dispatcher = serviceProvider.GetRequiredService<AsteriskRealtimeVoiceEventDispatcher>();
            await dispatcher.HandleAsync(voiceEvent, cancellationToken);
        });
    }

    private async Task ExecuteInTenantScopeAsync(Func<IServiceProvider, Task> action)
    {
        // The listener is a tenant singleton whose captured shell settings can point at a shell that is being
        // reloaded or disposed. Acquiring a scope or resolving services from a half-built shell throws
        // ArgumentNullException for a null service provider, so guard every step and skip gracefully rather
        // than letting the failure bubble up and tear down the WebSocket receive loop.
        ShellScope scope;

        try
        {
            scope = await _shellHost.GetScopeAsync(_shellSettings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Skipped an Asterisk real-time dispatch because a tenant scope could not be acquired; the shell may be reloading.");

            return;
        }

        if (scope?.ServiceProvider is null)
        {
            _logger.LogWarning(
                "Skipped an Asterisk real-time dispatch because the tenant scope service provider was unavailable; the shell may be reloading.");

            return;
        }

        await scope.UsingAsync(
            shellScope => action(shellScope.ServiceProvider),
            activateShell: false);
    }
}
