using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

/// <summary>
/// The carrier keywords are not optional politeness: a contact who texts STOP is entitled to a confirmation and
/// to never hear from the number again, and one who texts HELP is entitled to be told who is texting them.
/// Before this, STOP closed the thread silently and HELP and START were not recognised at all, so a contact who
/// had opted out had no supported way back in.
/// </summary>
public sealed class SmsKeywordPolicyTests
{
    [Theory]
    [InlineData("STOP")]
    [InlineData("stop")]
    [InlineData("  Stop  ")]
    [InlineData("STOPALL")]
    [InlineData("UNSUBSCRIBE")]
    [InlineData("CANCEL")]
    [InlineData("END")]
    [InlineData("QUIT")]
    public void Classify_RecognisesEveryOptOutKeyword(string body)
    {
        // Assert
        Assert.Equal(SmsKeyword.Stop, SmsKeywordPolicy.Classify(body));
    }

    [Theory]
    [InlineData("HELP")]
    [InlineData("help")]
    [InlineData("INFO")]
    public void Classify_RecognisesHelp(string body)
    {
        // Assert
        Assert.Equal(SmsKeyword.Help, SmsKeywordPolicy.Classify(body));
    }

    [Theory]
    [InlineData("START")]
    [InlineData("start")]
    [InlineData("UNSTOP")]
    [InlineData("YES")]
    public void Classify_RecognisesOptIn(string body)
    {
        // Assert
        Assert.Equal(SmsKeyword.Start, SmsKeywordPolicy.Classify(body));
    }

    [Theory]
    [InlineData("stop by the shop later")]
    [InlineData("can you help me with my order")]
    [InlineData("start of the month works")]
    [InlineData("")]
    [InlineData(null)]
    public void Classify_DoesNotFireOnOrdinaryProse(string body)
    {
        // Assert
        // A keyword is the whole message. Matching it as a prefix of a sentence opted people out of a service
        // they were in the middle of asking about.
        Assert.Equal(SmsKeyword.None, SmsKeywordPolicy.Classify(body));
    }

    [Theory]
    [InlineData("STOP.")]
    [InlineData("STOP!")]
    public void Classify_ToleratesTrailingPunctuation(string body)
    {
        // Assert
        Assert.Equal(SmsKeyword.Stop, SmsKeywordPolicy.Classify(body));
    }

    [Fact]
    public void ReplyFor_Stop_ConfirmsTheOptOut()
    {
        // Arrange
        var settings = new SmsKeywordReplySettings();

        // Act
        var reply = SmsKeywordPolicy.ReplyFor(SmsKeyword.Stop, settings);

        // Assert
        // The confirmation is itself required, so it is the one message that may be sent to a contact who has
        // just told us to stop.
        Assert.False(string.IsNullOrWhiteSpace(reply));
    }

    [Fact]
    public void ReplyFor_Help_UsesTheConfiguredText_WhenOneIsSet()
    {
        // Arrange
        var settings = new SmsKeywordReplySettings { HelpMessage = "Acme support: call 555-0100." };

        // Act
        var reply = SmsKeywordPolicy.ReplyFor(SmsKeyword.Help, settings);

        // Assert
        Assert.Equal("Acme support: call 555-0100.", reply);
    }

    [Fact]
    public void ReplyFor_None_IsNothing()
    {
        // Assert
        Assert.Null(SmsKeywordPolicy.ReplyFor(SmsKeyword.None, new SmsKeywordReplySettings()));
    }
}
