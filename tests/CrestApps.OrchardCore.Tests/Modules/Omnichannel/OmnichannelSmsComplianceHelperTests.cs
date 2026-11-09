using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// Locks the one SMS keyword policy that both the automated conversation and the messaging workspace use. A
/// regression here either ignores a genuine opt-out (a compliance risk) or falsely treats an ordinary reply as one.
/// </summary>
/// <remarks>
/// Bug: the automated path matched an opt-out keyword as a prefix ("STOP please" opted out) while the workspace
/// matched the whole message, so the same text opted a contact out on one path only. Making both exact then stopped
/// "STOP please" from opting out at all, a compliance regression. The rule is now one rule for both paths: a message
/// opts out when, after trimming whitespace and trailing punctuation, it is an opt-out keyword or starts with one
/// followed by a space or punctuation (its first word). HELP and START still need the whole message.
/// </remarks>
public sealed class OmnichannelSmsComplianceHelperTests
{
    [Theory]
    [InlineData("STOP")]
    [InlineData("stop")]
    [InlineData("  Stop  ")]
    [InlineData("STOP.")]
    [InlineData("STOP!")]
    [InlineData("STOPALL")]
    [InlineData("unsubscribe")]
    [InlineData("CANCEL")]
    [InlineData("End")]
    [InlineData("quit")]
    [InlineData("STOP please")]
    [InlineData("stop.")]
    [InlineData("Stop texting me")]
    [InlineData("STOP, wrong number")]
    [InlineData("stop by the shop later")]
    public void IsOptOutRequest_WhenTheFirstWordIsAStandardKeyword_ReturnsTrue(string message)
    {
        Assert.True(OmnichannelSmsComplianceHelper.IsOptOutRequest(message));
    }

    [Theory]
    [InlineData("stopping by later")]
    [InlineData("stopwatch")]
    [InlineData("please stop")]
    [InlineData("I want to cancel my appointment tomorrow")]
    [InlineData("endoscopy")]
    [InlineData("yes")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsOptOutRequest_WhenTheFirstWordIsNotAKeyword_ReturnsFalse(string message)
    {
        Assert.False(OmnichannelSmsComplianceHelper.IsOptOutRequest(message));
    }

    [Fact]
    public void IsOptOutRequest_HonorsConfiguredKeywords_AtTheStartOfTheMessage()
    {
        var keywords = new[] { "no more" };

        Assert.True(OmnichannelSmsComplianceHelper.IsOptOutRequest("No More", keywords));
        Assert.True(OmnichannelSmsComplianceHelper.IsOptOutRequest("no more!", keywords));
        Assert.True(OmnichannelSmsComplianceHelper.IsOptOutRequest("no more, thanks", keywords));
        Assert.False(OmnichannelSmsComplianceHelper.IsOptOutRequest("no moreover", keywords));
        Assert.False(OmnichannelSmsComplianceHelper.IsOptOutRequest("there is no more", keywords));
    }

    [Theory]
    [InlineData("STOP please")]
    [InlineData("stop.")]
    public void TheAutomatedPath_OptsOut_WithOrWithoutConfiguredKeywords(string message)
    {
        // The automated SMS conversation asks with the subject flow's keywords, which may be empty.
        Assert.True(OmnichannelSmsComplianceHelper.IsOptOutRequest(message, keywords: null));
        Assert.True(OmnichannelSmsComplianceHelper.IsOptOutRequest(message, ["no more"]));
    }

    [Theory]
    [InlineData("STOP please")]
    [InlineData("stop.")]
    public void TheWorkspacePath_ClassifiesAsStop(string message)
    {
        Assert.Equal(SmsKeyword.Stop, SmsKeywordPolicy.Classify(message));
    }

    [Fact]
    public void IsOptOutRequest_WithConfiguredKeywords_StillHonorsTheStandardKeywords()
    {
        // The carriers require the standard keywords to opt out whatever a tenant adds.
        Assert.True(OmnichannelSmsComplianceHelper.IsOptOutRequest("STOP", ["no more"]));
    }

    [Fact]
    public void IsOptOutRequest_WithNullOrEmptyKeywords_UsesTheStandardKeywords()
    {
        Assert.True(OmnichannelSmsComplianceHelper.IsOptOutRequest("STOP", keywords: null));
        Assert.True(OmnichannelSmsComplianceHelper.IsOptOutRequest("STOP", keywords: []));
    }

    [Theory]
    [InlineData("STOP please")]
    [InlineData("STOP")]
    [InlineData("stop.")]
    [InlineData("Unsubscribe")]
    [InlineData("stop by the shop later")]
    [InlineData("HELP")]
    [InlineData("START")]
    [InlineData("")]
    [InlineData(null)]
    public void TheAutomatedAndWorkspacePaths_AgreeOnWhatIsAnOptOut(string message)
    {
        // The automated conversation asks IsOptOutRequest; the messaging workspace asks SmsKeywordPolicy.
        Assert.Equal(
            OmnichannelSmsComplianceHelper.IsOptOutRequest(message),
            SmsKeywordPolicy.Classify(message) == SmsKeyword.Stop);
    }

    [Theory]
    [InlineData("HELP", true)]
    [InlineData("info.", true)]
    [InlineData("help me", false)]
    [InlineData("help stop", false)]
    public void IsHelpRequest_MatchesTheWholeMessage(string message, bool expected)
    {
        Assert.Equal(expected, OmnichannelSmsComplianceHelper.IsHelpRequest(message));
    }

    [Theory]
    [InlineData("START", true)]
    [InlineData("unstop", true)]
    [InlineData("Yes!", true)]
    [InlineData("start of the month", false)]
    public void IsOptInRequest_MatchesTheWholeMessage(string message, bool expected)
    {
        Assert.Equal(expected, OmnichannelSmsComplianceHelper.IsOptInRequest(message));
    }

    [Fact]
    public void ParseOptOutKeywords_SplitsOnCommasSemicolonsAndNewlines()
    {
        var keywords = OmnichannelSmsComplianceHelper.ParseOptOutKeywords("STOP, QUIT; END\nBYE");

        Assert.Equal(["STOP", "QUIT", "END", "BYE"], keywords);
    }

    [Fact]
    public void ParseOptOutKeywords_KeepsDistinctValues()
    {
        var keywords = OmnichannelSmsComplianceHelper.ParseOptOutKeywords("pause, STOP; pause");

        Assert.Equal(["pause", "STOP"], keywords);
    }

    [Fact]
    public void ParseOptOutKeywords_WhenBlank_ReturnsTheDefaults()
    {
        Assert.Same(OmnichannelSmsComplianceHelper.DefaultOptOutKeywords, OmnichannelSmsComplianceHelper.ParseOptOutKeywords("   "));
    }
}
