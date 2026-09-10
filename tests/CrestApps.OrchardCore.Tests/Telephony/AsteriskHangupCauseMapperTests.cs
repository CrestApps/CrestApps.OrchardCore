using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskHangupCauseMapperTests
{
    // Every Q.850 cause an Asterisk release can place on a hangup event, paired with the outcome it
    // actually represents. Before this mapping existed every one of them collapsed into a single
    // "disconnected" state, so a busy number, an unanswered dial, an abandoned call, and a completed
    // conversation were indistinguishable to compliance and abandon reporting.
    [Theory]
    [InlineData(16, true, HangupCause.NormalClearing)]
    [InlineData(16, false, HangupCause.Canceled)]
    [InlineData(31, true, HangupCause.NormalClearing)]
    [InlineData(31, false, HangupCause.Canceled)]
    [InlineData(17, false, HangupCause.Busy)]
    [InlineData(18, false, HangupCause.NoAnswer)]
    [InlineData(19, false, HangupCause.NoAnswer)]
    [InlineData(20, false, HangupCause.NoAnswer)]
    [InlineData(102, false, HangupCause.NoAnswer)]
    [InlineData(21, false, HangupCause.Rejected)]
    [InlineData(22, false, HangupCause.Rejected)]
    [InlineData(23, false, HangupCause.Rejected)]
    [InlineData(34, false, HangupCause.Congestion)]
    [InlineData(38, false, HangupCause.Congestion)]
    [InlineData(41, false, HangupCause.Congestion)]
    [InlineData(42, false, HangupCause.Congestion)]
    [InlineData(44, false, HangupCause.Congestion)]
    [InlineData(47, false, HangupCause.Congestion)]
    [InlineData(1, false, HangupCause.Failed)]
    [InlineData(27, false, HangupCause.Failed)]
    [InlineData(28, false, HangupCause.Failed)]
    [InlineData(88, false, HangupCause.Failed)]
    [InlineData(0, false, HangupCause.Unknown)]
    public void FromCauseCode_ForEveryQ850CauseAsteriskReports_MapsToTheMatchingHangupCause(
        int causeCode,
        bool wasAnswered,
        HangupCause expected)
    {
        // Act
        var actual = AsteriskHangupCauseMapper.FromCauseCode(causeCode, wasAnswered);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FromCauseCode_AcrossTheQ850Range_ProducesEveryDistinguishableHangupCause()
    {
        // Arrange
        // AnsweringMachine cannot come from a Q.850 cause because Q.850 describes the release, not who
        // or what answered, so it is proven separately through answer detection.
        var expected = Enum.GetValues<HangupCause>()
            .Where(cause => cause != HangupCause.AnsweringMachine)
            .ToHashSet();

        // Act
        var produced = new HashSet<HangupCause>();

        for (var causeCode = 0; causeCode <= 127; causeCode++)
        {
            produced.Add(AsteriskHangupCauseMapper.FromCauseCode(causeCode, wasAnswered: true));
            produced.Add(AsteriskHangupCauseMapper.FromCauseCode(causeCode, wasAnswered: false));
        }

        // Assert
        Assert.Equal(expected, produced);
    }

    [Theory]
    [InlineData("Normal Clearing", true, HangupCause.NormalClearing)]
    [InlineData("Normal Clearing", false, HangupCause.Canceled)]
    [InlineData("User busy", false, HangupCause.Busy)]
    [InlineData("No user responding", false, HangupCause.NoAnswer)]
    [InlineData("Subscriber absent", false, HangupCause.NoAnswer)]
    [InlineData("Call Rejected", false, HangupCause.Rejected)]
    [InlineData("No circuit/channel available", false, HangupCause.Congestion)]
    public void Resolve_WhenOnlyTheCauseTextIsReported_StillResolvesTheCause(
        string causeText,
        bool wasAnswered,
        HangupCause expected)
    {
        // Act
        var actual = AsteriskHangupCauseMapper.Resolve(
            causeCode: null,
            causeText: causeText,
            wasAnswered: wasAnswered,
            answeringMachineDetected: false);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Resolve_WhenAnswerDetectionReportsAMachine_TakesPrecedenceOverTheReleaseCause()
    {
        // Act
        var actual = AsteriskHangupCauseMapper.Resolve(
            causeCode: 16,
            causeText: "Normal Clearing",
            wasAnswered: true,
            answeringMachineDetected: true);

        // Assert
        Assert.Equal(HangupCause.AnsweringMachine, actual);
    }

    [Fact]
    public void Resolve_WhenTheProviderReportsNoCauseAtAll_RecordsItAsUnknownRatherThanNormal()
    {
        // Act
        var actual = AsteriskHangupCauseMapper.Resolve(
            causeCode: null,
            causeText: null,
            wasAnswered: true,
            answeringMachineDetected: false);

        // Assert
        Assert.Equal(HangupCause.Unknown, actual);
    }

    [Theory]
    [InlineData(17, HangupCause.Busy)]
    [InlineData(19, HangupCause.NoAnswer)]
    [InlineData(21, HangupCause.Rejected)]
    [InlineData(34, HangupCause.Congestion)]
    [InlineData(28, HangupCause.Failed)]
    public void TryMap_ForAChannelDestroyedEvent_CarriesTheReleaseCauseOutOfTheProviderModule(
        int causeCode,
        HangupCause expected)
    {
        // Arrange
        var payload = $$"""
        {
          "type": "ChannelDestroyed",
          "timestamp": "2026-04-02T09:00:00.000+0000",
          "cause": {{causeCode}},
          "cause_txt": "Released",
          "channel": {
            "id": "channel-1",
            "state": "Ring",
            "caller": { "number": "+15551230000" },
            "connected": { "number": "+15559990000" },
            "dialplan": { "exten": "1000" }
          }
        }
        """;

        // Act
        var mapped = AsteriskRealtimeVoiceEventMapper.TryMap("Asterisk", payload, out var voiceEvent);

        // Assert
        Assert.True(mapped);
        Assert.Equal(CallState.Disconnected, voiceEvent.State);
        Assert.Equal(expected, voiceEvent.HangupCause);
    }

    [Fact]
    public void TryMap_ForANonTerminalEvent_ReportsNoHangupCause()
    {
        // Arrange
        var payload = """
        {
          "type": "ChannelStateChange",
          "timestamp": "2026-04-02T09:00:00.000+0000",
          "channel": {
            "id": "channel-1",
            "state": "Up",
            "caller": { "number": "+15551230000" },
            "connected": { "number": "+15559990000" },
            "dialplan": { "exten": "1000" }
          }
        }
        """;

        // Act
        var mapped = AsteriskRealtimeVoiceEventMapper.TryMap("Asterisk", payload, out var voiceEvent);

        // Assert
        Assert.True(mapped);
        Assert.Equal(CallState.Connected, voiceEvent.State);
        Assert.Null(voiceEvent.HangupCause);
    }

    [Fact]
    public void TryMap_WhenAnswerDetectionMarkedTheChannel_ReportsAnAnsweringMachineHangup()
    {
        // Arrange
        var payload = """
        {
          "type": "ChannelDestroyed",
          "timestamp": "2026-04-02T09:00:00.000+0000",
          "cause": 16,
          "cause_txt": "Normal Clearing",
          "channel": {
            "id": "channel-1",
            "state": "Up",
            "caller": { "number": "+15551230000" },
            "connected": { "number": "+15559990000" },
            "dialplan": { "exten": "1000" },
            "channelvars": { "AMDSTATUS": "MACHINE" }
          }
        }
        """;

        // Act
        var mapped = AsteriskRealtimeVoiceEventMapper.TryMap("Asterisk", payload, out var voiceEvent);

        // Assert
        Assert.True(mapped);
        Assert.Equal(HangupCause.AnsweringMachine, voiceEvent.HangupCause);
    }
}
