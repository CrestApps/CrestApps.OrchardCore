using CrestApps.OrchardCore.Telnyx.Services;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// An automated voice agent's call is recorded from the answer, and the recording is listed with the call's direction
/// and the customer's number. Telnyx reports a leg's direction on <c>call.initiated</c> only, and the recording starts
/// on <c>call.answered</c>.
/// </summary>
public sealed class TelnyxAiVoiceRecordingHandlerTests
{
    [Fact]
    public void ResolveCustomer_ACallAnEntryPointAnsweredForTheAI_IsInboundFromTheCaller()
    {
        // Arrange
        // call.answered carries no direction. Read from the event alone, every call a customer placed to an entry point
        // routed to an AI voice agent was listed as outbound, with the business's own number as the customer.
        var answered = new TelnyxCallEvent
        {
            EventType = "call.answered",
            CallControlId = "ctrl-1",
            From = "+306900000000",
            To = "+19300000000",
        };
        var state = new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.AiVoiceLegIntent,
            ActivityId = "activity-1",
            Inbound = true,
        };

        // Act
        var (isInbound, customerNumber) = TelnyxAiVoiceRecordingHandler.ResolveCustomer(answered, state);

        // Assert
        Assert.True(isInbound);
        Assert.Equal("+306900000000", customerNumber);
    }

    [Fact]
    public void ResolveCustomer_ACallTheAIPlaced_IsOutboundToTheDialedNumber()
    {
        // Arrange
        var answered = new TelnyxCallEvent
        {
            EventType = "call.answered",
            CallControlId = "ctrl-1",
            From = "+19300000000",
            To = "+306900000000",
        };
        var state = new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.AiVoiceLegIntent,
            ActivityId = "activity-1",
        };

        // Act
        var (isInbound, customerNumber) = TelnyxAiVoiceRecordingHandler.ResolveCustomer(answered, state);

        // Assert
        Assert.False(isInbound);
        Assert.Equal("+306900000000", customerNumber);
    }
}
