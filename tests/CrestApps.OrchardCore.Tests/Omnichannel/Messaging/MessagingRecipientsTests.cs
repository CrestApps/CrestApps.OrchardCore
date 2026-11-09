using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// The composer and the broadcast screen each had a private copy of the recipient parser; they share this one.
/// </summary>
public sealed class MessagingRecipientsTests
{
    [Fact]
    public void Parse_SplitsOnLinesCommasAndSemicolons_TrimsAndDropsRepeats()
    {
        var recipients = MessagingRecipients.Parse(" +15550000001\r\n+15550000002, +15550000001;+15550000003 ;;");

        Assert.Equal(["+15550000001", "+15550000002", "+15550000003"], recipients);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n ")]
    public void Parse_WithNothingTyped_ReturnsNoRecipients(string text)
        => Assert.Empty(MessagingRecipients.Parse(text));
}
