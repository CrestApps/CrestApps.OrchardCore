using System.Diagnostics.Metrics;
using System.Threading.Channels;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Asterisk.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskRealtimeIngestionWriterTests
{
    [Fact]
    public async Task WriteAsync_WhenBufferHasRoom_WritesImmediatelyWithoutRecordingSaturation()
    {
        // Arrange
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(2)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

        var providerName = Guid.NewGuid().ToString("N");
        var writer = CreateWriter(channel, providerName, TimeSpan.FromSeconds(5));

        using var saturation = new SaturationMeterProbe(providerName);

        // Act
        var result = await writer.WriteAsync("event", CancellationToken.None);

        // Assert
        Assert.Equal(AsteriskRealtimeIngestionWriteResult.Written, result);
        Assert.Equal(0, saturation.Count);
        Assert.True(channel.Reader.TryRead(out var buffered));
        Assert.Equal("event", buffered);
    }

    [Fact]
    public async Task WriteAsync_WhenBufferIsFull_AppliesBackpressureUntilTheReaderDrains()
    {
        // Arrange
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

        var providerName = Guid.NewGuid().ToString("N");
        var writer = CreateWriter(channel, providerName, TimeSpan.FromSeconds(5));

        // Fill the single-slot buffer so the next write must wait.
        Assert.Equal(AsteriskRealtimeIngestionWriteResult.Written, await writer.WriteAsync("first", CancellationToken.None));

        // Act
        var pending = writer.WriteAsync("second", CancellationToken.None);

        // The write must not complete while the buffer is full: this is real backpressure, not a drop.
        await Task.Delay(150, TestContext.Current.CancellationToken);
        Assert.False(pending.IsCompleted);

        // Draining a slot releases the backpressure and the pending write completes.
        Assert.True(channel.Reader.TryRead(out var firstBuffered));
        var result = await pending;

        // Assert
        Assert.Equal("first", firstBuffered);
        Assert.Equal(AsteriskRealtimeIngestionWriteResult.Written, result);
        Assert.True(channel.Reader.TryRead(out var secondBuffered));
        Assert.Equal("second", secondBuffered);
    }

    [Fact]
    public async Task WriteAsync_WhenBufferSaturates_RecordsSaturationMetricOncePerEpisode()
    {
        // Arrange
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

        var providerName = Guid.NewGuid().ToString("N");
        var writer = CreateWriter(channel, providerName, TimeSpan.FromSeconds(5));

        using var saturation = new SaturationMeterProbe(providerName);

        // Act — fill the buffer, then two waiting writes across one sustained saturation episode.
        await writer.WriteAsync("a", CancellationToken.None);

        var firstWait = writer.WriteAsync("b", CancellationToken.None);
        await Task.Delay(150, TestContext.Current.CancellationToken);

        // The metric must increment on the first wait, before the wait is even released.
        Assert.Equal(1, saturation.Count);

        Assert.True(channel.Reader.TryRead(out _));
        Assert.Equal(AsteriskRealtimeIngestionWriteResult.Written, await firstWait);

        // A second wait while the buffer is still saturated is the same episode and must not re-record.
        var secondWait = writer.WriteAsync("c", CancellationToken.None);
        await Task.Delay(150, TestContext.Current.CancellationToken);
        Assert.Equal(1, saturation.Count);

        Assert.True(channel.Reader.TryRead(out _));
        Assert.Equal(AsteriskRealtimeIngestionWriteResult.Written, await secondWait);

        // Assert — one sustained episode recorded exactly once.
        Assert.Equal(1, saturation.Count);
    }

    [Fact]
    public async Task WriteAsync_WhenBufferRecoversThenSaturatesAgain_RecordsANewEpisode()
    {
        // Arrange
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

        var providerName = Guid.NewGuid().ToString("N");
        var writer = CreateWriter(channel, providerName, TimeSpan.FromSeconds(5));

        using var saturation = new SaturationMeterProbe(providerName);

        // First episode.
        await writer.WriteAsync("a", CancellationToken.None);
        var firstWait = writer.WriteAsync("b", CancellationToken.None);
        await Task.Delay(150, TestContext.Current.CancellationToken);
        Assert.True(channel.Reader.TryRead(out _));
        await firstWait;
        Assert.Equal(1, saturation.Count);

        // Buffer recovers: a fast write succeeds and ends the episode.
        Assert.True(channel.Reader.TryRead(out _));
        Assert.Equal(AsteriskRealtimeIngestionWriteResult.Written, await writer.WriteAsync("c", CancellationToken.None));

        // Act — saturate again, which is a new episode.
        var secondWait = writer.WriteAsync("d", CancellationToken.None);
        await Task.Delay(150, TestContext.Current.CancellationToken);
        Assert.True(channel.Reader.TryRead(out _));
        await secondWait;

        // Assert
        Assert.Equal(2, saturation.Count);
    }

    [Fact]
    public async Task WriteAsync_WhenBufferStaysFullPastTheBackpressureWindow_ReportsReconnect()
    {
        // Arrange
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

        var providerName = Guid.NewGuid().ToString("N");
        var writer = CreateWriter(channel, providerName, TimeSpan.FromMilliseconds(100));

        using var saturation = new SaturationMeterProbe(providerName);

        await writer.WriteAsync("first", CancellationToken.None);

        // Act — the reader never drains, so the bounded backpressure wait must elapse and report a reconnect.
        var result = await writer.WriteAsync("second", CancellationToken.None);

        // Assert
        Assert.Equal(AsteriskRealtimeIngestionWriteResult.BackpressureTimedOut, result);
        Assert.Equal(1, saturation.Count);
    }

    [Fact]
    public async Task WriteAsync_WhenListenerIsCancelledDuringBackpressure_Throws()
    {
        // Arrange
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

        var providerName = Guid.NewGuid().ToString("N");
        var writer = CreateWriter(channel, providerName, TimeSpan.FromSeconds(30));

        using var cts = new CancellationTokenSource();
        await writer.WriteAsync("first", cts.Token);

        // Act
        var pending = writer.WriteAsync("second", cts.Token);
        await cts.CancelAsync();

        // Assert — listener shutdown surfaces as cancellation rather than a silent reconnect signal.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
    }

    private static AsteriskRealtimeIngestionWriter CreateWriter(
        Channel<string> channel,
        string providerName,
        TimeSpan backpressureTimeout)
        => new(channel.Writer, channel.Reader, providerName, backpressureTimeout, NullLogger.Instance);

    private sealed class SaturationMeterProbe : IDisposable
    {
        private readonly MeterListener _listener;
        private readonly string _providerName;
        private long _count;

        public SaturationMeterProbe(string providerName)
        {
            _providerName = providerName;

            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == AsteriskDiagnostics.MeterName &&
                        instrument.Name == "asterisk.realtime.ingestion.saturated")
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
