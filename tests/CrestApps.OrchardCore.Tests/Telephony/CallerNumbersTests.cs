using CrestApps.OrchardCore.Telephony;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// SEC-FE2: a caller id is chosen by whoever places the call. It is kept to what a dialable number can contain before
/// it is stored, so markup in a crafted caller id never reaches the agent's recent calls or voicemail list.
/// </summary>
public sealed class CallerNumbersTests
{
    [Theory]
    [InlineData("+1 (702) 499-3350", "+17024993350")]
    [InlineData("1001", "1001")]
    [InlineData("*72#", "*72#")]
    [InlineData("5551234\" onmouseover=\"alert(1)", "55512341")]
    [InlineData("<script>alert(1)</script>", "1")]
    public void Sanitize_KeepsOnlyWhatANumberCanContain(string raw, string expected)
    {
        Assert.Equal(expected, CallerNumbers.Sanitize(raw));
    }

    // A SIP address (an extension or a WebRTC credential calling) stays an address, so the code that recognizes one
    // still does, but keeps none of the characters markup needs.
    [Theory]
    [InlineData("sip:gencred2@sip.telnyx.com", "sip:gencred2@sip.telnyx.com")]
    [InlineData("sip:1001@pbx.example.com;transport=ws", "sip:1001@pbx.example.com;transport=ws")]
    [InlineData("sip:\"><img src=x onerror=alert(1)>@x", "sip:imgsrc=xonerror=alert1@x")]
    public void Sanitize_KeepsASipAddress_WithoutMarkup(string raw, string expected)
    {
        Assert.Equal(expected, CallerNumbers.Sanitize(raw));
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("Anonymous")]
    [InlineData("restricted")]
    [InlineData("unknown")]
    [InlineData("private")]
    public void Sanitize_KeepsAKnownAnonymousMarker(string marker)
    {
        Assert.Equal(marker, CallerNumbers.Sanitize(marker));
    }

    // Voice Mild (fix verification): on a call between two extensions the provider puts the other party's name in the
    // address on purpose, and the caller id cleaning turned "Front Desk 2" into "2".
    [Theory]
    [InlineData("Front Desk 2", true, "Front Desk 2")]
    [InlineData("Front Desk 2", false, "2")]
    [InlineData("R&D \"Lab\" <b>", true, "R&D Lab b")]
    [InlineData("<>", true, null)]
    [InlineData("+1 (702) 499-3350", true, "+1 (702) 499-3350")]
    public void SanitizeAddress_KeepsTheNameOnAnExtensionCall(string raw, bool isExtensionCall, string expected)
    {
        Assert.Equal(expected, CallerNumbers.SanitizeAddress(raw, isExtensionCall));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\"><img src=x>")]
    public void Sanitize_ANumberWithNothingDialable_IsNull(string raw)
    {
        Assert.Null(CallerNumbers.Sanitize(raw));
    }
}
