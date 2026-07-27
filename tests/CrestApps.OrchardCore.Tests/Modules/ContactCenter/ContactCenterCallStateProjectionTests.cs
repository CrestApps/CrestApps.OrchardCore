using CrestApps.OrchardCore.ContactCenter.Models;
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
        var produced = Enum.GetValues<ContactCenterCallState>()
            .Select(ContactCenterCallStateProjection.ToTelephonyCallState)
            .ToHashSet();

        // Assert
        Assert.All(produced, state => Assert.Contains(state, declared));
    }

    [Theory]
    [InlineData(ContactCenterCallState.Planned, CallState.Idle)]
    [InlineData(ContactCenterCallState.Dialing, CallState.Connecting)]
    [InlineData(ContactCenterCallState.Ringing, CallState.Ringing)]
    [InlineData(ContactCenterCallState.Connected, CallState.Connected)]
    [InlineData(ContactCenterCallState.OnHold, CallState.OnHold)]
    [InlineData(ContactCenterCallState.Ending, CallState.Disconnected)]
    [InlineData(ContactCenterCallState.Ended, CallState.Disconnected)]
    [InlineData(ContactCenterCallState.Transferred, CallState.Disconnected)]
    [InlineData(ContactCenterCallState.Canceled, CallState.Disconnected)]
    [InlineData(ContactCenterCallState.NoAnswer, CallState.Failed)]
    [InlineData(ContactCenterCallState.Rejected, CallState.Failed)]
    [InlineData(ContactCenterCallState.Failed, CallState.Failed)]
    public void ToTelephonyCallState_NarrowsEachContactCenterStateOntoItsSoftPhoneState(
        ContactCenterCallState state,
        CallState expected)
    {
        // Act
        var actual = ContactCenterCallStateProjection.ToTelephonyCallState(state);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(CallState.Connecting, ContactCenterCallState.Dialing)]
    [InlineData(CallState.Ringing, ContactCenterCallState.Ringing)]
    [InlineData(CallState.Connected, ContactCenterCallState.Connected)]
    [InlineData(CallState.OnHold, ContactCenterCallState.OnHold)]
    public void ToContactCenterCallState_RoundTripsEveryLiveState(CallState state, ContactCenterCallState expected)
    {
        // Act
        var widened = ContactCenterCallStateProjection.ToContactCenterCallState(state);
        var narrowed = ContactCenterCallStateProjection.ToTelephonyCallState(widened);

        // Assert
        Assert.Equal(expected, widened);
        Assert.Equal(state, narrowed);
    }

    [Fact]
    public void ToContactCenterCallState_WhenTheProviderAlsoReportsHold_PrefersHeldOverConnected()
    {
        // Act
        var actual = ContactCenterCallStateProjection.ToContactCenterCallState(CallState.Connected, isOnHold: true);

        // Assert
        Assert.Equal(ContactCenterCallState.OnHold, actual);
    }

    [Fact]
    public void ToContactCenterCallState_ForTheIdleSentinel_TreatsTheCallAsOver()
    {
        // Act
        var actual = ContactCenterCallStateProjection.ToContactCenterCallState(CallState.Idle);

        // Assert
        Assert.Equal(ContactCenterCallState.Ended, actual);
    }

    // This is the whole point of carrying a hangup cause: the soft-phone vocabulary has one
    // "disconnected" state, so without the cause every one of these outcomes widened to Ended.
    [Theory]
    [InlineData(HangupCause.NormalClearing, ContactCenterCallState.Ended)]
    [InlineData(HangupCause.AnsweringMachine, ContactCenterCallState.Ended)]
    [InlineData(HangupCause.Busy, ContactCenterCallState.Rejected)]
    [InlineData(HangupCause.Rejected, ContactCenterCallState.Rejected)]
    [InlineData(HangupCause.NoAnswer, ContactCenterCallState.NoAnswer)]
    [InlineData(HangupCause.Canceled, ContactCenterCallState.Canceled)]
    [InlineData(HangupCause.Congestion, ContactCenterCallState.Failed)]
    [InlineData(HangupCause.Failed, ContactCenterCallState.Failed)]
    public void ToContactCenterCallState_ForADisconnectedCall_RefinesTheOutcomeFromTheHangupCause(
        HangupCause hangupCause,
        ContactCenterCallState expected)
    {
        // Act
        var actual = ContactCenterCallStateProjection.ToContactCenterCallState(
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
        var disconnected = ContactCenterCallStateProjection.ToContactCenterCallState(CallState.Disconnected);
        var failed = ContactCenterCallStateProjection.ToContactCenterCallState(CallState.Failed);
        var unknown = ContactCenterCallStateProjection.ToContactCenterCallState(
            CallState.Disconnected,
            isOnHold: false,
            HangupCause.Unknown);

        // Assert
        Assert.Equal(ContactCenterCallState.Ended, disconnected);
        Assert.Equal(ContactCenterCallState.Failed, failed);
        Assert.Equal(ContactCenterCallState.Ended, unknown);
    }

    [Fact]
    public void IsTerminal_AgreesWithTheStatesThatCarryAnEndTime()
    {
        // Arrange
        var expected = new HashSet<ContactCenterCallState>
        {
            ContactCenterCallState.Ended,
            ContactCenterCallState.Failed,
            ContactCenterCallState.NoAnswer,
            ContactCenterCallState.Rejected,
            ContactCenterCallState.Canceled,
            ContactCenterCallState.Transferred,
        };

        // Act
        var actual = Enum.GetValues<ContactCenterCallState>()
            .Where(ContactCenterCallStateProjection.IsTerminal)
            .ToHashSet();

        // Assert
        Assert.Equal(expected, actual);
    }
}
