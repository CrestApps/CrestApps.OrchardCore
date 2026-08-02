using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Asterisk.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskRealtimeVoiceListenerReconnectMetricTests
{
    [Fact]
    public async Task RunListener_WhenTheConnectionCannotBeEstablished_EmitsTheReconnectCounter()
    {
        // Arrange
        const string providerName = "asterisk-reconnect-wiring";
        var port = GetClosedLoopbackPort();
        var settings = new AsteriskResolvedSettings
        {
            IsEnabled = true,
            ProviderName = providerName,
            BaseUrl = $"http://127.0.0.1:{port}",
            UserName = "user",
            Password = "secret",
            ApplicationName = "reconnect-app",
        };

        using var probe = new ReconnectCounterProbe(providerName);

        await using var listener = new AsteriskRealtimeVoiceListener(
            Mock.Of<IShellHost>(),
            new ShellSettings(),
            Options.Create(new AsteriskCoordinationOptions()),
            NullLogger<AsteriskRealtimeVoiceListener>.Instance);

        // Act
        await listener.StartAsync([settings]);

        await probe.FirstMeasurement.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        await listener.StopAsync();

        // Assert
        Assert.True(probe.Count >= 1, "The listener should record a reconnect attempt when the ARI connection fails.");
    }

    private static int GetClosedLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return port;
    }

    private sealed class ReconnectCounterProbe : IDisposable
    {
        private readonly MeterListener _listener;
        private readonly string _providerName;
        private readonly TaskCompletionSource _firstMeasurement =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private long _count;

        public ReconnectCounterProbe(string providerName)
        {
            _providerName = providerName;

            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == AsteriskDiagnostics.MeterName &&
                        instrument.Name == "asterisk.realtime.reconnect_attempted")
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                },
            };

            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                foreach (var tag in tags)
                {
                    if (tag.Key == "provider" && Equals(tag.Value, _providerName))
                    {
                        Interlocked.Add(ref _count, measurement);
                        _firstMeasurement.TrySetResult();
                    }
                }
            });

            _listener.Start();
        }

        public Task FirstMeasurement => _firstMeasurement.Task;

        public long Count => Interlocked.Read(ref _count);

        public void Dispose() => _listener.Dispose();
    }
}
