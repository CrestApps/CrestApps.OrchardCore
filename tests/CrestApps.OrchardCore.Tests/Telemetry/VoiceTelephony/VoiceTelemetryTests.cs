using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Telemetry;

namespace CrestApps.OrchardCore.Tests.Telemetry.VoiceTelephony;

/// <summary>
/// What the AI usage report measures on a call reaches <c>CrestApps.OrchardCore.Voice</c> as it was measured, once per
/// call: the call by its first summary, its audio by the one summary that carries it. The deployment is a tag; the
/// activity, the call and anything said never are.
/// </summary>
public sealed class VoiceTelemetryTests
{
    [Fact]
    public void RecordSession_WithMeasurements_EmitsTheSessionAndEachMeasuredLatencyInSeconds()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var deployment = "deployment-" + Guid.NewGuid().ToString("n");
        var measured = new AIVoiceSessionMeasurements
        {
            SessionDurationMs = 95_000,
            TimeToFirstAssistantAudioMs = 1_250,
            ResponseLatenciesMs = [800, 1_200, 950],
            ModelLatenciesMs = [400, 600],
            DetectorWaitsMs = [300],
            BargeIns = 2,
        };

        // Act
        VoiceTelemetry.RecordSession(
            AIVoiceSessionEngine.Realtime,
            AIVoiceSessionOutcome.HandedToAgent,
            deployment,
            "Telnyx",
            measured,
            countSession: true);

        // Assert
        var session = Assert.Single(capture.Find("crestapps.voice.ai.sessions", VoiceTelemetry.DeploymentTag, deployment));
        Assert.Equal(VoiceTelemetry.SourceName, session.Meter);
        Assert.Equal(VoiceTelemetry.EngineRealtime, session.Tags[VoiceTelemetry.EngineTag]);
        Assert.Equal("handed_to_agent", session.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
        Assert.Equal("Telnyx", session.Tags[CrestAppsOrchardCoreTelemetry.ProviderTag]);

        Assert.Equal(95d, Assert.Single(capture.Find("crestapps.voice.ai.session.duration", VoiceTelemetry.DeploymentTag, deployment)).Value);
        Assert.Equal(1.25d, Assert.Single(capture.Find("crestapps.voice.ai.first_audio.latency", VoiceTelemetry.DeploymentTag, deployment)).Value);
        Assert.Equal(new[] { 0.8d, 1.2d, 0.95d }, capture.Find("crestapps.voice.ai.response.latency", VoiceTelemetry.DeploymentTag, deployment).Select(m => m.Value));
        Assert.Equal(new[] { 0.4d, 0.6d }, capture.Find("crestapps.voice.ai.model.latency", VoiceTelemetry.DeploymentTag, deployment).Select(m => m.Value));
        Assert.Equal(0.3d, Assert.Single(capture.Find("crestapps.voice.ai.detector.wait", VoiceTelemetry.DeploymentTag, deployment)).Value);
        Assert.Equal(2d, Assert.Single(capture.Find("crestapps.voice.ai.barge_ins", VoiceTelemetry.DeploymentTag, deployment)).Value);
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public void RecordSession_ForTheSecondSummaryOfACall_DoesNotCountTheCallAgain()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var deployment = "deployment-" + Guid.NewGuid().ToString("n");

        // Act
        // The provider's hangup summarized the call first, without its audio; the session's summary that replaces it
        // carries the audio but is the same call.
        VoiceTelemetry.RecordSession(AIVoiceSessionEngine.TurnBased, AIVoiceSessionOutcome.CallerHungUp, deployment, "Telnyx", measured: null, countSession: true);
        VoiceTelemetry.RecordSession(
            AIVoiceSessionEngine.TurnBased,
            AIVoiceSessionOutcome.CallerHungUp,
            deployment,
            "Telnyx",
            new AIVoiceSessionMeasurements { SessionDurationMs = 30_000, BargeIns = 3 },
            countSession: false);

        // Assert
        Assert.Single(capture.Find("crestapps.voice.ai.sessions", VoiceTelemetry.DeploymentTag, deployment));
        Assert.Single(capture.Find("crestapps.voice.ai.session.duration", VoiceTelemetry.DeploymentTag, deployment));

        // Nobody can talk over a turn-based call: it does not listen while it speaks.
        Assert.Empty(capture.Find("crestapps.voice.ai.barge_ins", VoiceTelemetry.DeploymentTag, deployment));
    }

    [Theory]
    [InlineData(AIVoiceSessionOutcome.CompletedByAI, "completed_by_ai")]
    [InlineData(AIVoiceSessionOutcome.HandedToAgent, "handed_to_agent")]
    [InlineData(AIVoiceSessionOutcome.Voicemail, "voicemail")]
    [InlineData(AIVoiceSessionOutcome.NoAnswer, "no_answer")]
    [InlineData(AIVoiceSessionOutcome.CallerHungUp, "caller_hung_up")]
    [InlineData(AIVoiceSessionOutcome.Failed, "failed")]
    [InlineData(AIVoiceSessionOutcome.Transferred, "transferred")]
    [InlineData(AIVoiceSessionOutcome.Unknown, "unknown")]
    public void GetOutcome_MapsEveryOutcomeToABoundedValue(AIVoiceSessionOutcome outcome, string expected)
    {
        // Act
        var value = VoiceTelemetry.GetOutcome(outcome);

        // Assert
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TrackMediaStream_CountsTheStreamOpenUntilTheLeaseIsDisposed()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var provider = "provider-" + Guid.NewGuid().ToString("n");

        // Act
        var lease = VoiceTelemetry.TrackMediaStream(provider);
        lease.Dispose();
        lease.Dispose();

        // Assert
        var changes = capture.Find("crestapps.voice.media.streams", CrestAppsOrchardCoreTelemetry.ProviderTag, provider).Select(m => m.Value);
        Assert.Equal(new[] { 1d, -1d }, changes);
    }
}
