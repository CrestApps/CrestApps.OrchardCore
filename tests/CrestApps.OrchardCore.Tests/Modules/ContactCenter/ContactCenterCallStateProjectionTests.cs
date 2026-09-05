using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterCallStateProjectionTests
{
    [Fact]
    public void ToTelephonyCallState_IsTotalOverTheContactCenterVocabulary()
    {
        // Arrange
        var declared = Enum.GetValues<CallState>().ToHashSet();

        // Act
        var produced = Enum.GetValues<VoiceCallState>()
            .Select(VoiceCallStateProjection.ToTelephonyCallState)
            .ToHashSet();

        // Assert
        Assert.All(produced, state => Assert.Contains(state, declared));
    }

    [Theory]
    [InlineData(VoiceCallState.Planned, CallState.Idle)]
    [InlineData(VoiceCallState.Dialing, CallState.Connecting)]
    [InlineData(VoiceCallState.Ringing, CallState.Ringing)]
    [InlineData(VoiceCallState.Connected, CallState.Connected)]
    [InlineData(VoiceCallState.OnHold, CallState.OnHold)]
    [InlineData(VoiceCallState.Ending, CallState.Disconnected)]
    [InlineData(VoiceCallState.Ended, CallState.Disconnected)]
    [InlineData(VoiceCallState.Transferred, CallState.Disconnected)]
    [InlineData(VoiceCallState.Canceled, CallState.Disconnected)]
    [InlineData(VoiceCallState.NoAnswer, CallState.Failed)]
    [InlineData(VoiceCallState.Rejected, CallState.Failed)]
    [InlineData(VoiceCallState.Failed, CallState.Failed)]
    public void ToTelephonyCallState_NarrowsEachContactCenterStateOntoItsSoftPhoneState(
        VoiceCallState state,
        CallState expected)
    {
        // Act
        var actual = VoiceCallStateProjection.ToTelephonyCallState(state);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(CallState.Connecting, VoiceCallState.Dialing)]
    [InlineData(CallState.Ringing, VoiceCallState.Ringing)]
    [InlineData(CallState.Connected, VoiceCallState.Connected)]
    [InlineData(CallState.OnHold, VoiceCallState.OnHold)]
    public void ToContactCenterCallState_RoundTripsEveryLiveState(CallState state, VoiceCallState expected)
    {
        // Act
        var widened = VoiceCallStateProjection.ToVoiceCallState(state);
        var narrowed = VoiceCallStateProjection.ToTelephonyCallState(widened);

        // Assert
        Assert.Equal(expected, widened);
        Assert.Equal(state, narrowed);
    }

    [Fact]
    public void ToContactCenterCallState_WhenTheProviderAlsoReportsHold_PrefersHeldOverConnected()
    {
        // Act
        var actual = VoiceCallStateProjection.ToVoiceCallState(CallState.Connected, isOnHold: true);

        // Assert
        Assert.Equal(VoiceCallState.OnHold, actual);
    }

    [Fact]
    public void ToContactCenterCallState_ForTheIdleSentinel_TreatsTheCallAsOver()
    {
        // Act
        var actual = VoiceCallStateProjection.ToVoiceCallState(CallState.Idle);

        // Assert
        Assert.Equal(VoiceCallState.Ended, actual);
    }

    // This is the whole point of carrying a hangup cause: the soft-phone vocabulary has one
    // "disconnected" state, so without the cause every one of these outcomes widened to Ended.
    [Theory]
    [InlineData(HangupCause.NormalClearing, VoiceCallState.Ended)]
    [InlineData(HangupCause.AnsweringMachine, VoiceCallState.Ended)]
    [InlineData(HangupCause.Busy, VoiceCallState.Rejected)]
    [InlineData(HangupCause.Rejected, VoiceCallState.Rejected)]
    [InlineData(HangupCause.NoAnswer, VoiceCallState.NoAnswer)]
    [InlineData(HangupCause.Canceled, VoiceCallState.Canceled)]
    [InlineData(HangupCause.Congestion, VoiceCallState.Failed)]
    [InlineData(HangupCause.Failed, VoiceCallState.Failed)]
    public void ToContactCenterCallState_ForADisconnectedCall_RefinesTheOutcomeFromTheHangupCause(
        HangupCause hangupCause,
        VoiceCallState expected)
    {
        // Act
        var actual = VoiceCallStateProjection.ToVoiceCallState(
            CallState.Disconnected,
            isOnHold: false,
            hangupCause);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToContactCenterCallState_WhenNoCauseWasReported_KeepsTheUnrefinedTerminalState()
    {
        // Act
        var disconnected = VoiceCallStateProjection.ToVoiceCallState(CallState.Disconnected);
        var failed = VoiceCallStateProjection.ToVoiceCallState(CallState.Failed);
        var unknown = VoiceCallStateProjection.ToVoiceCallState(
            CallState.Disconnected,
            isOnHold: false,
            HangupCause.Unknown);

        // Assert
        Assert.Equal(VoiceCallState.Ended, disconnected);
        Assert.Equal(VoiceCallState.Failed, failed);
        Assert.Equal(VoiceCallState.Ended, unknown);
    }

    [Fact]
    public void IsTerminal_AgreesWithTheStatesThatCarryAnEndTime()
    {
        // Arrange
        var expected = new HashSet<VoiceCallState>
        {
            VoiceCallState.Ended,
            VoiceCallState.Failed,
            VoiceCallState.NoAnswer,
            VoiceCallState.Rejected,
            VoiceCallState.Canceled,
            VoiceCallState.Transferred,
        };

        // Act
        var actual = Enum.GetValues<VoiceCallState>()
            .Where(VoiceCallStateProjection.IsTerminal)
            .ToHashSet();

        // Assert
        Assert.Equal(expected, actual);
    }
}
