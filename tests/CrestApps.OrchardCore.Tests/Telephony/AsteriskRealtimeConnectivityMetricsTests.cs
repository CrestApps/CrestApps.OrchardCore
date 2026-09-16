using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Asterisk.Telemetry;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskRealtimeConnectivityMetricsTests
{
    [Fact]
    public void RecordRealtimeConnected_EmitsConnectedCounterForProvider()
    {
        // Arrange
        const string providerName = "asterisk-a";
        using var probe = new CounterProbe("asterisk.realtime.connected", providerName);

        // Act
        AsteriskDiagnostics.RecordRealtimeConnected(providerName);
        AsteriskDiagnostics.RecordRealtimeConnected(providerName);

        // Assert
        Assert.Equal(2, probe.Count);
    }

    [Fact]
    public void RecordRealtimeReconnectAttempted_EmitsReconnectCounterForProvider()
    {
        // Arrange
        const string providerName = "asterisk-b";
        using var probe = new CounterProbe("asterisk.realtime.reconnect_attempted", providerName);

        // Act
        AsteriskDiagnostics.RecordRealtimeReconnectAttempted(providerName);

        // Assert
        Assert.Equal(1, probe.Count);
    }

    [Fact]
    public void RecordRealtimeConnected_TagsMeasurementWithProvider_SoAnotherProviderIsNotCounted()
    {
        // Arrange
        using var probe = new CounterProbe("asterisk.realtime.connected", "asterisk-watched");

        // Act
        AsteriskDiagnostics.RecordRealtimeConnected("asterisk-other");

        // Assert
        Assert.Equal(0, probe.Count);
    }

    private sealed class CounterProbe : IDisposable
    {
        private readonly MeterListener _listener;
        private readonly string _instrumentName;
        private readonly string _providerName;
        private long _count;

        public CounterProbe(string instrumentName, string providerName)
        {
            _instrumentName = instrumentName;
            _providerName = providerName;

            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == AsteriskDiagnostics.MeterName &&
                        instrument.Name == _instrumentName)
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
                    }
                }
            });

            _listener.Start();
        }

        public long Count => Interlocked.Read(ref _count);

        public void Dispose() => _listener.Dispose();
    }
}
