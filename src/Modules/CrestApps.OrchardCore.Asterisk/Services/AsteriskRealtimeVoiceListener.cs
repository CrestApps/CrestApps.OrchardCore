using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskRealtimeVoiceListener : IAsteriskRealtimeVoiceListener, IAsyncDisposable
{
    private static readonly TimeSpan _healthyConnectionResetThreshold = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan _minBufferDrainBudget = TimeSpan.FromSeconds(30);

    private readonly IShellHost _shellHost;
    private readonly ShellSettings _shellSettings;
    private readonly AsteriskCoordinationOptions _coordinationOptions;
    private readonly ILogger<AsteriskRealtimeVoiceListener> _logger;
    private readonly Lock _lock = new();
    private CancellationTokenSource _listenerCancellationTokenSource;
    private Task _listenerTask;

    public AsteriskRealtimeVoiceListener(
        IShellHost shellHost,
        ShellSettings shellSettings,
        IOptions<AsteriskCoordinationOptions> coordinationOptions,
        ILogger<AsteriskRealtimeVoiceListener> logger)
    {
        _shellHost = shellHost;
        _shellSettings = shellSettings;
        _coordinationOptions = coordinationOptions.Value;
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
                var (backpressureTimedOut, receiveUptime) = await ListenAsync(settings, cancellationToken);

                // A clean disconnect is a fresh start, so reset the backoff. A backpressure timeout means the
                // dispatcher could not keep up; treat it as a failure so repeated saturation backs off exponentially
                // instead of hot-looping reconnect and reconcile. An isolated timeout on a connection that had been
                // receiving for a meaningful period is not a hot loop, so it also resets rather than pinning the
                // ceiling. The uptime measures only the receive loop, not the post-disconnect drain.
                if (!backpressureTimedOut || receiveUptime >= _healthyConnectionResetThreshold)
                {
                    failureCount = 0;
                }
                else
                {
                    failureCount++;
                }
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
                AsteriskDiagnostics.RecordRealtimeReconnectAttempted(settings.ProviderName);

                await Task.Delay(GetReconnectDelay(failureCount), cancellationToken);
            }
        }
    }

    private async Task<(bool BackpressureTimedOut, TimeSpan ReceiveUptime)> ListenAsync(AsteriskResolvedSettings settings, CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        var eventsUri = AsteriskSettingsUtilities.CreateEventsUri(settings);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Connecting the Asterisk real-time voice listener for provider {ProviderName} to {EventsUri}.",
                settings.ProviderName,
                AsteriskSettingsUtilities.CreateEventsUriForLogging(settings));
        }

        await socket.ConnectAsync(eventsUri, cancellationToken);

        AsteriskDiagnostics.RecordRealtimeConnected(settings.ProviderName);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Connected the Asterisk real-time voice listener for provider {ProviderName}.",
                settings.ProviderName);
        }

        await ReconcileAsync(settings.ProviderName, cancellationToken);

        // Measure only how long the connection stays up receiving events, started after reconciliation and captured
        // before the post-disconnect drain, so the reconnect backoff sees genuine receive uptime rather than time
        // spent reconciling or draining a slow buffer.
        var receiveStopwatch = Stopwatch.StartNew();

        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(_coordinationOptions.RealtimeEventBufferCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

        var ingestion = new AsteriskRealtimeIngestionWriter(
            channel.Writer,
            channel.Reader,
            settings.ProviderName,
            _coordinationOptions.RealtimeEventBackpressureTimeout,
            _logger);

        // The worker drains the buffer on its own cancellation source so a bounded post-disconnect drain can stop a
        // wedged dispatch without waiting on the shell-shutdown token.
        using var workerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var worker = ProcessBufferedPayloadsAsync(workerCts.Token);
        var buffer = new byte[8 * 1024];
        var backpressureTimedOut = false;

        try
        {
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
                            result.CloseStatusDescription.SanitizeLogValue());

                        await CloseSocketSafelyAsync(socket, WebSocketCloseStatus.NormalClosure, "Closed", cancellationToken);

                        return (false, receiveStopwatch.Elapsed);
                    }

                    if (message.Length + result.Count > _coordinationOptions.MaxRealtimeMessageBytes)
                    {
                        _logger.LogWarning(
                            "The Asterisk real-time voice listener for provider {ProviderName} abandoned a message that exceeded the {MaxBytes}-byte limit and closed the socket. A peer that never sets the end-of-message flag cannot be allowed to grow the reassembly buffer without bound.",
                            settings.ProviderName,
                            _coordinationOptions.MaxRealtimeMessageBytes);

                        // The peer that produced the oversized message is presumed hostile, so use CloseOutputAsync
                        // under a bounded token: it sends the close frame without waiting for the peer's close reply,
                        // and the timeout guarantees even the send cannot stall the listener if the peer stops reading.
                        await CloseSocketSafelyAsync(socket, WebSocketCloseStatus.MessageTooBig, "Message too large", cancellationToken);

                        return (false, receiveStopwatch.Elapsed);
                    }

                    await message.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken);
                }
                while (!result.EndOfMessage);

                var payload = Encoding.UTF8.GetString(message.ToArray());

                // Apply real backpressure rather than dropping the connection on the first full write: the receive
                // loop stops draining the socket while the buffer is full, so TCP flow control slows the provider.
                // Only if the reader cannot catch up within the bounded window do we reconnect and reconcile.
                var writeResult = await ingestion.WriteAsync(payload, cancellationToken);

                if (writeResult == AsteriskRealtimeIngestionWriteResult.BackpressureTimedOut)
                {
                    backpressureTimedOut = true;

                    break;
                }
            }

            // Capture the receive uptime before the finally drain runs so the backoff excludes drain time.
            return (backpressureTimedOut, receiveStopwatch.Elapsed);
        }
        finally
        {
            channel.Writer.TryComplete();

            var drainBudget = _coordinationOptions.RealtimeEventBackpressureTimeout > _minBufferDrainBudget
                ? _coordinationOptions.RealtimeEventBackpressureTimeout
                : _minBufferDrainBudget;

            await AsteriskRealtimeIngestionDrainer.DrainAsync(
                worker,
                channel.Reader,
                workerCts,
                _coordinationOptions.RealtimeEventBackpressureTimeout,
                drainBudget,
                settings.ProviderName,
                _logger,
                cancellationToken);
        }

        async Task ProcessBufferedPayloadsAsync(CancellationToken workerCancellationToken)
        {
            await foreach (var payload in channel.Reader.ReadAllAsync(workerCancellationToken))
            {
                // A single malformed or unroutable event, or a transient tenant-scope failure while the shell is
                // reloading, must never tear down the live event stream. Isolate each dispatch so the socket keeps
                // receiving; any missed state change is still reconciled by the periodic provider-truth sweep.
                try
                {
                    await DispatchAsync(settings.ProviderName, payload, workerCancellationToken);
                }
                catch (OperationCanceledException) when (workerCancellationToken.IsCancellationRequested)
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
    }

    private static TimeSpan GetReconnectDelay(int failureCount)
    {
        var exponent = Math.Min(Math.Max(failureCount, 0), 5);
        var seconds = Math.Min(Math.Pow(2, exponent), 30);
        var jitter = 0.8 + (Random.Shared.NextDouble() * 0.4);

        return TimeSpan.FromSeconds(seconds * jitter);
    }

    private async Task CloseSocketSafelyAsync(
        ClientWebSocket socket,
        WebSocketCloseStatus closeStatus,
        string statusDescription,
        CancellationToken cancellationToken)
    {
        // Bound the close so a peer that never completes (or reads) the close handshake cannot stall the listener.
        using var closeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        closeCts.CancelAfter(TimeSpan.FromSeconds(AsteriskConstants.RealtimeCloseHandshakeTimeoutSeconds));

        try
        {
            // CloseOutputAsync only sends the close frame; it does not wait for the peer's close reply, so a peer
            // that stops responding cannot block the listener beyond the send itself, which the token bounds.
            await socket.CloseOutputAsync(closeStatus, statusDescription, closeCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Abandoned the Asterisk real-time voice WebSocket close handshake after {TimeoutSeconds}s because the peer did not complete it.",
                    AsteriskConstants.RealtimeCloseHandshakeTimeoutSeconds);
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug(ex, "The Asterisk real-time voice WebSocket close frame could not be sent.");
        }
    }

    private async Task ReconcileAsync(string providerName, CancellationToken cancellationToken)
    {
        await ExecuteInTenantScopeAsync(async serviceProvider =>
        {
            foreach (var reconciler in serviceProvider.GetServices<IAsteriskProviderStateReconciler>())
            {
                try
                {
                    await reconciler.ReconcileAsync(providerName, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Provider-state reconciliation failed after reconnecting the Asterisk real-time listener for provider {ProviderName}.",
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
                voiceEvent.EventType.SanitizeLogValue(),
                voiceEvent.ProviderName,
                voiceEvent.CallId.SanitizeLogValue(),
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
