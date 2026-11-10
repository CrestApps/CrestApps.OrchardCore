using System.Net;
using CrestApps.OrchardCore.Asterisk.Telemetry;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Telephony.Telemetry;
using CrestApps.OrchardCore.WebSockets.Services;
using CrestApps.OrchardCore.WebSockets.Telemetry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Polly.Timeout;

namespace CrestApps.OrchardCore.Tests.Telemetry.VoiceTelephony;

/// <summary>
/// The cross-provider telephony instruments (<c>CrestApps.OrchardCore.Telephony</c>), the Asterisk provider's
/// (<c>CrestApps.OrchardCore.Asterisk</c>, with its deprecated <c>asterisk.*</c> names kept for one release) and the
/// WebSocket rendezvous (<c>CrestApps.OrchardCore.WebSockets</c>) record what the observability docs list, with bounded
/// tags only.
/// </summary>
public sealed class TelephonyTelemetryTests
{
    [Fact]
    public async Task CommandExecutor_RecordsEachCommandByOutcome()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var command = "Command" + Guid.NewGuid().ToString("n");
        var executor = CreateExecutor(stopping: false);

        // Act
        await executor.ExecuteAsync("Telnyx", command, _ => Task.FromResult(TelephonyResult.Success()));
        await executor.ExecuteAsync("Telnyx", command, _ => Task.FromResult(TelephonyResult.Failed("Refused.")));

        // Assert
        var outcomes = capture.Find("crestapps.telephony.command.duration", TelephonyTelemetry.CommandTag, command)
            .Select(measurement => measurement.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag])
            .ToList();

        Assert.Equal(new object[] { CrestAppsOrchardCoreTelemetry.OutcomeOk, CrestAppsOrchardCoreTelemetry.OutcomeError }, outcomes);
        Assert.All(
            capture.Find("crestapps.telephony.command.duration", TelephonyTelemetry.CommandTag, command),
            measurement => Assert.Equal("Telnyx", measurement.Tags[CrestAppsOrchardCoreTelemetry.ProviderTag]));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public async Task CommandExecutor_WhenTheHostIsStopping_RecordsTheCommandAsNotAdmitted()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var command = "Command" + Guid.NewGuid().ToString("n");
        var executor = CreateExecutor(stopping: true);

        // Act
        await Assert.ThrowsAsync<TelephonyCommandNotAdmittedException>(() =>
            executor.ExecuteAsync("Telnyx", command, _ => Task.FromResult(TelephonyResult.Success())));

        // Assert
        var measurement = Assert.Single(capture.Find("crestapps.telephony.command.duration", TelephonyTelemetry.CommandTag, command));
        Assert.Equal(TelephonyTelemetry.ResultNotAdmitted, measurement.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
    }

    [Theory]
    [InlineData("DoNotCall", "do_not_call")]
    [InlineData("ScreeningError", "screening_error")]
    [InlineData("calling-window", "calling_window")]
    [InlineData("+1 (555) 010-0000", "other")]
    [InlineData(null, "unknown")]
    public void ToBoundedName_KeepsCodesAndRejectsAnythingElse(string code, string expected)
    {
        // Act
        var value = TelephonyTelemetry.ToBoundedName(code);

        // Assert
        Assert.Equal(expected, value);
    }

    [Fact]
    public void GetDiscardReason_NamesWhyADeliveryIsDiscarded()
    {
        // Arrange
        var occurredUtc = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
        var watermark = new VoiceStreamWatermark
        {
            Phase = VoiceCallLifecyclePhase.Established,
            LastEventUtc = occurredUtc,
            HighWaterSequence = 10,
        };

        // Act
        var applied = VoiceStreamOrdering.GetDiscardReason(watermark, new VoiceStreamDelivery
        {
            Phase = VoiceCallLifecyclePhase.Established,
            OccurredUtc = occurredUtc.AddSeconds(1),
            SequenceNumber = 11,
        });
        var repeated = VoiceStreamOrdering.GetDiscardReason(watermark, new VoiceStreamDelivery
        {
            Phase = VoiceCallLifecyclePhase.Established,
            OccurredUtc = occurredUtc.AddSeconds(1),
            SequenceNumber = 10,
        });
        var unsequenced = VoiceStreamOrdering.GetDiscardReason(watermark, new VoiceStreamDelivery
        {
            Phase = VoiceCallLifecyclePhase.Established,
            OccurredUtc = occurredUtc.AddSeconds(1),
        });

        // Assert
        Assert.Null(applied);
        Assert.Equal(VoiceStreamOrdering.DiscardSequenceRegression, repeated);
        Assert.Equal(VoiceStreamOrdering.DiscardUnsequenced, unsequenced);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "ok")]
    [InlineData(HttpStatusCode.NotFound, "http_4xx")]
    [InlineData(HttpStatusCode.BadGateway, "http_5xx")]
    public void GetAriResult_ClassifiesTheResponse(HttpStatusCode statusCode, string expected)
    {
        // Act
        var result = AsteriskDiagnostics.GetAriResult(statusCode);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetAriResult_WithoutAResponse_TellsATimeoutFromAnUnreachableAsterisk()
    {
        // Act
        var timedOut = AsteriskDiagnostics.GetAriResult(null, new TimeoutRejectedException());
        var unreachable = AsteriskDiagnostics.GetAriResult(null, new HttpRequestException("refused"));

        // Assert
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeTimeout, timedOut);
        Assert.Equal(AsteriskDiagnostics.AriResultUnreachable, unreachable);
    }

    [Fact]
    public void Asterisk_RecordsTheNewInstrumentNamesNextToTheDeprecatedOnes_AndTheCurrentConnectionState()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var provider = "asterisk-" + Guid.NewGuid().ToString("n");

        // Act
        AsteriskDiagnostics.RecordRealtimeIngestionSaturated(provider);
        AsteriskDiagnostics.RecordRealtimeConnected(provider);

        using (var tracking = AsteriskDiagnostics.TrackRealtimeListener(provider))
        {
            tracking.EventReceived();
        }

        AsteriskDiagnostics.RecordRealtimeEvent(provider, "StasisStart", AsteriskDiagnostics.EventApplied);
        AsteriskDiagnostics.RecordRealtimeEvent(provider, "SomethingNew", AsteriskDiagnostics.EventIgnored);

        // Assert
        Assert.Single(capture.Find("asterisk.realtime.ingestion.saturated", "provider", provider));
        Assert.Single(capture.Find("crestapps.asterisk.realtime.ingestion.saturated", "provider", provider));
        Assert.Single(capture.Find("asterisk.realtime.connected", "provider", provider));
        Assert.Single(capture.Find("crestapps.asterisk.realtime.connections", "provider", provider));
        Assert.Equal(
            new[] { 1d, -1d },
            capture.Find("crestapps.asterisk.realtime.connected", "provider", provider).Select(measurement => measurement.Value));

        var eventTypes = capture.Find("crestapps.asterisk.realtime.events", CrestAppsOrchardCoreTelemetry.ProviderTag, provider)
            .Select(measurement => measurement.Tags[AsteriskDiagnostics.EventTypeTag])
            .ToList();
        Assert.Equal(new object[] { "StasisStart", CrestAppsOrchardCoreTelemetry.Other }, eventTypes);
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public async Task WebSocketRendezvous_RecordsRegistrationsClaimsAndMisses()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var registry = new InMemoryWebSocketConnectionRegistry();
        var key = "key-" + Guid.NewGuid().ToString("n");

        // Act
        await registry.RegisterAsync(key, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => registry.RegisterAsync(key, TestContext.Current.CancellationToken));
        var claimed = await registry.TryClaimAsync(key, TestContext.Current.CancellationToken);
        var missed = await registry.TryClaimAsync(key, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(claimed);
        Assert.Null(missed);

        var results = capture.Measurements
            .Where(measurement => measurement.Instrument == "crestapps.websockets.rendezvous")
            .Select(measurement => measurement.Tags[CrestAppsOrchardCoreTelemetry.ResultTag])
            .ToList();

        Assert.Contains(WebSocketsTelemetry.ResultRegistered, results);
        Assert.Contains(WebSocketsTelemetry.ResultConflict, results);
        Assert.Contains(WebSocketsTelemetry.ResultClaimed, results);
        Assert.Contains(WebSocketsTelemetry.ResultMiss, results);
        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.websockets.rendezvous.wait.duration");

        // The key is unguessable and identifies the call: it is never a tag.
        Assert.DoesNotContain(capture.Measurements, measurement => measurement.Tags.ContainsValue(key));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    private static DefaultTelephonyCommandExecutor CreateExecutor(bool stopping)
    {
        var lifetime = new Mock<IHostApplicationLifetime>();
        lifetime.SetupGet(host => host.ApplicationStopping).Returns(new CancellationToken(stopping));

        return new DefaultTelephonyCommandExecutor(
            Options.Create(new TelephonyCommandOptions { Timeout = TimeSpan.FromSeconds(10) }),
            lifetime.Object);
    }
}
