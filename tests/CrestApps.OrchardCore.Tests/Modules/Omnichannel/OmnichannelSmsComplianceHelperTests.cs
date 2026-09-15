using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// Locks the SMS opt-out detection that the automated conversation relies on to honor "STOP" and set the contact's
/// do-not-SMS flag. A regression here either ignores a genuine opt-out (a compliance risk) or falsely treats an
/// ordinary reply as one, so the matching rules are pinned here.
/// </summary>
public sealed class OmnichannelSmsComplianceHelperTests
{
    [Theory]
    [InlineData("STOP")]
    [InlineData("stop")]
    [InlineData("  Stop  ")]
    [InlineData("STOP.")]
    [InlineData("STOP please")]
    [InlineData("unsubscribe")]
    [InlineData("CANCEL")]
    public void IsOptOutRequest_WithADefaultKeyword_ReturnsTrue(string message)
    {
        Assert.True(OmnichannelSmsComplianceHelper.IsOptOutRequest(message));
    }

    [Theory]
    [InlineData("stopping by later")]   // "stop" followed by a letter is not a word boundary.
    [InlineData("please stop")]          // opt-out keywords must lead the message, not appear mid-sentence.
    [InlineData("I want to cancel my appointment tomorrow")]
    [InlineData("yes")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsOptOutRequest_ForOrdinaryMessages_ReturnsFalse(string message)
    {
        Assert.False(OmnichannelSmsComplianceHelper.IsOptOutRequest(message));
    }

    [Fact]
    public void IsOptOutRequest_HonorsConfiguredKeywords()
    {
        var keywords = new[] { "no more" };

        Assert.True(OmnichannelSmsComplianceHelper.IsOptOutRequest("No More", keywords));
        Assert.True(OmnichannelSmsComplianceHelper.IsOptOutRequest("no more, thanks", keywords));

        // A default keyword is not honored once a custom set is supplied.
        Assert.False(OmnichannelSmsComplianceHelper.IsOptOutRequest("STOP", keywords));
    }

    [Fact]
    public void IsOptOutRequest_WithNullOrEmptyKeywords_FallsBackToTheDefaults()
    {
        Assert.True(OmnichannelSmsComplianceHelper.IsOptOutRequest("STOP", keywords: null));
        Assert.True(OmnichannelSmsComplianceHelper.IsOptOutRequest("STOP", keywords: []));
    }

    [Fact]
    public void ParseOptOutKeywords_SplitsOnCommasSemicolonsAndNewlines()
    {
        var keywords = OmnichannelSmsComplianceHelper.ParseOptOutKeywords("STOP, QUIT; END\nBYE");

        Assert.Equal(["STOP", "QUIT", "END", "BYE"], keywords);
    }

    [Fact]
    public void ParseOptOutKeywords_WhenBlank_ReturnsTheDefaults()
    {
        Assert.Same(OmnichannelSmsComplianceHelper.DefaultOptOutKeywords, OmnichannelSmsComplianceHelper.ParseOptOutKeywords("   "));
    }
}
