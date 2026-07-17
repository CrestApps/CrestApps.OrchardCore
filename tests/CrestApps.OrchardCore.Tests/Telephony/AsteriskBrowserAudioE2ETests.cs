namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskBrowserAudioE2ETests
{
    [Fact(Skip = "Requires real Asterisk, coturn, browser WebRTC, trusted WSS/DTLS certificates, direct-ICE and forced-TURN tone verification; unavailable in unit-test infrastructure.")]
    public void BrowserToAsteriskWebRtcAudio_WithDirectIceAndForcedTurn_VerifiesReceivedToneFrequencies()
    {
        // This release-blocking proof runs on the dedicated media runner described by PLAN-2 CC-3.
    }
}
