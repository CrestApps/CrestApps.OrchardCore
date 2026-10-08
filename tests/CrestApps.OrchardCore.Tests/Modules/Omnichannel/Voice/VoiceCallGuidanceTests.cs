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
}
