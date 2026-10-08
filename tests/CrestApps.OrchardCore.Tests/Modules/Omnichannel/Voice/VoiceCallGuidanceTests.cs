using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// What every automated call tells the model about ending it.
/// </summary>
public sealed class VoiceCallGuidanceTests
{
    [Fact]
    public void OnlyAClearYes_ConfirmsDetailsReadBack()
    {
        // Live, a model read an email address back wrongly, the reply came through garbled, and it was taken for a
        // yes: the customer was thanked and the call hung up with the wrong address.
        Assert.Contains("only a clear yes", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
        Assert.Contains("was that a yes?", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
        Assert.Contains("read the corrected details back", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
    }

    [Fact]
    public void ACallbackWithoutATime_IsAskedWhenBeforeTheGoodbye()
    {
        // Live, a customer said "can you call me later?" and the model said goodbye and hung up without asking when.
        Assert.Contains("without saying when, ask once", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
    }

    [Fact]
    public void NotRightNow_IsAnsweredWithAnOfferToCallBack()
    {
        // Live, "no, not right now" was wished a good day and closed as finished, so the lead was never called again.
        Assert.Contains("now is not a good time, offer to call them back", VoiceCallGuidance.EndingTheCall, StringComparison.Ordinal);
    }
}
