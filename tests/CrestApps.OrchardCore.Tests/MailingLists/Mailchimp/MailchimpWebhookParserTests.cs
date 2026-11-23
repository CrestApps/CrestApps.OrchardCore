using CrestApps.OrchardCore.MailingLists.Mailchimp.Services;
using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.Tests.MailingLists.Mailchimp;

public sealed class MailchimpWebhookParserTests
{
    private static readonly DateTime _receivedUtc = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Subscribe_IsReadWithItsMergeFields_AndMapsToSubscribedAndVerified()
    {
        // Arrange
        const string body = "type=subscribe&fired_at=2009-03-26+21%3A35%3A57&data%5Bid%5D=8a25ff1d98&data%5Blist_id%5D=aud123"
            + "&data%5Bemail%5D=api%2Btest%40mailchimp.com&data%5Bemail_type%5D=html&data%5Bmerges%5D%5BEMAIL%5D=api%2Btest%40mailchimp.com"
            + "&data%5Bmerges%5D%5BFNAME%5D=Mary+Ann&data%5Bmerges%5D%5BLNAME%5D=Smith&data%5Bmerges%5D%5BINTERESTS%5D=Group1%2CGroup2"
            + "&data%5Bmerges%5D%5BGROUPINGS%5D%5B0%5D%5Bname%5D=Colors&data%5Bip_opt%5D=10.20.10.30&data%5Bip_signup%5D=10.20.10.30";

        // Act
        var webhook = MailchimpWebhookParser.Parse(body);
        var change = MailchimpChangeMapper.FromWebhook(MailchimpTestContext.List(), webhook, _receivedUtc);

        // Assert
        Assert.Equal("subscribe", webhook.Type);
        Assert.Equal(new DateTime(2009, 3, 26, 21, 35, 57, DateTimeKind.Utc), webhook.FiredUtc);
        Assert.Equal(DateTimeKind.Utc, webhook.FiredUtc!.Value.Kind);
        Assert.Equal("aud123", webhook.AudienceId);
        Assert.Equal("api+test@mailchimp.com", webhook.Email);
        Assert.Equal("Mary Ann", webhook.Merges["FNAME"]);
        Assert.Equal("Group1,Group2", webhook.Merges["INTERESTS"]);
        Assert.False(webhook.Merges.ContainsKey("GROUPINGS"));
        Assert.DoesNotContain(webhook.Merges.Keys, key => key.Contains('[', StringComparison.Ordinal));

        Assert.Equal("list-1", change.ListId);
        Assert.Equal("api+test@mailchimp.com", change.Email);
        Assert.Equal(MailingListMemberStatus.Subscribed, change.Status);
        Assert.True(change.Verified);
        Assert.Equal("Mary Ann Smith", change.Name);
        Assert.Equal(MailchimpSubscriberHash.Compute("api+test@mailchimp.com"), change.ProviderMemberId);
        Assert.Equal(webhook.FiredUtc, change.OccurredUtc);
    }

    [Fact]
    public void Unsubscribe_MapsToUnsubscribed_WithMailchimpsReason()
    {
        // Arrange
        const string body = "type=unsubscribe&fired_at=2026-11-22+10%3A00%3A00&data%5Baction%5D=unsub&data%5Breason%5D=manual"
            + "&data%5Bid%5D=8a25ff1d98&data%5Blist_id%5D=aud123&data%5Bemail%5D=a%40example.com&data%5Bcampaign_id%5D=cb398d21d2";

        // Act
        var webhook = MailchimpWebhookParser.Parse(body);
        var change = MailchimpChangeMapper.FromWebhook(MailchimpTestContext.List(), webhook, _receivedUtc);

        // Assert
        Assert.Equal("unsub", webhook.Action);
        Assert.Equal("manual", webhook.Reason);
        Assert.Equal(MailingListMemberStatus.Unsubscribed, change.Status);
        Assert.False(change.Verified);
        Assert.Equal("manual", change.Reason);
        Assert.Equal("a@example.com", change.Email);
    }

    [Fact]
    public void Unsubscribe_ByAnAdminDelete_SaysDeleted()
    {
        // Arrange
        const string body = "type=unsubscribe&fired_at=2026-11-22+10%3A00%3A00&data%5Baction%5D=delete&data%5Breason%5D=manual&data%5Bemail%5D=a%40example.com";

        // Act
        var change = MailchimpChangeMapper.FromWebhook(MailchimpTestContext.List(), MailchimpWebhookParser.Parse(body), _receivedUtc);

        // Assert
        Assert.Equal(MailingListMemberStatus.Unsubscribed, change.Status);
        Assert.Equal("deleted", change.Reason);
    }

    [Fact]
    public void Cleaned_MapsToCleaned_WithTheBounceReason()
    {
        // Arrange
        const string body = "type=cleaned&fired_at=2026-11-22+10%3A00%3A00&data%5Blist_id%5D=aud123&data%5Bcampaign_id%5D=4fjk2ma9xd&data%5Breason%5D=hard&data%5Bemail%5D=bounce%40example.com";

        // Act
        var change = MailchimpChangeMapper.FromWebhook(MailchimpTestContext.List(), MailchimpWebhookParser.Parse(body), _receivedUtc);

        // Assert
        Assert.Equal(MailingListMemberStatus.Cleaned, change.Status);
        Assert.Equal("hard", change.Reason);
        Assert.Equal("bounce@example.com", change.Email);
        Assert.False(change.Verified);
    }

    [Fact]
    public void Profile_MapsToANameChangeOnly()
    {
        // Arrange
        const string body = "type=profile&fired_at=2026-11-22+10%3A00%3A00&data%5Bemail%5D=a%40example.com&data%5Bmerges%5D%5BFNAME%5D=Ada&data%5Bmerges%5D%5BLNAME%5D=";

        // Act
        var change = MailchimpChangeMapper.FromWebhook(MailchimpTestContext.List(), MailchimpWebhookParser.Parse(body), _receivedUtc);

        // Assert
        Assert.Null(change.Status);
        Assert.Equal("Ada", change.Name);
        Assert.Equal("a@example.com", change.Email);
    }

    [Fact]
    public void Profile_WithoutAName_ChangesNothing()
    {
        // Arrange
        const string body = "type=profile&fired_at=2026-11-22+10%3A00%3A00&data%5Bemail%5D=a%40example.com&data%5Bmerges%5D%5BCOMPANY%5D=Acme";

        // Act
        var change = MailchimpChangeMapper.FromWebhook(MailchimpTestContext.List(), MailchimpWebhookParser.Parse(body), _receivedUtc);

        // Assert
        Assert.Null(change);
    }

    [Fact]
    public void Profile_OnAListThatDoesNotSendNames_ChangesNothing()
    {
        // Arrange
        const string body = "type=profile&fired_at=2026-11-22+10%3A00%3A00&data%5Bemail%5D=a%40example.com&data%5Bmerges%5D%5BFNAME%5D=Ada";
        var list = MailchimpTestContext.List(configure: settings => settings.SendNameMergeFields = false);

        // Act
        var change = MailchimpChangeMapper.FromWebhook(list, MailchimpWebhookParser.Parse(body), _receivedUtc);

        // Assert
        Assert.Null(change);
    }

    [Fact]
    public void AddressChange_MapsTheOldAddressToTheNewOne()
    {
        // Arrange
        const string body = "type=upemail&fired_at=2026-11-22+10%3A00%3A00&data%5Blist_id%5D=aud123&data%5Bnew_id%5D=51da8c3259"
            + "&data%5Bnew_email%5D=new%40example.com&data%5Bold_email%5D=old%40example.com";

        // Act
        var webhook = MailchimpWebhookParser.Parse(body);
        var change = MailchimpChangeMapper.FromWebhook(MailchimpTestContext.List(), webhook, _receivedUtc);

        // Assert
        Assert.Equal("upemail", webhook.Type);
        Assert.Equal("old@example.com", change.Email);
        Assert.Equal("new@example.com", change.NewEmail);
        Assert.Null(change.Status);
        Assert.Equal(MailchimpSubscriberHash.Compute("new@example.com"), change.ProviderMemberId);
    }

    [Fact]
    public void Campaign_IsReadButNotHandled()
    {
        // Arrange
        const string body = "type=campaign&fired_at=2026-11-22+10%3A00%3A00&data%5Bid%5D=5aa2102003&data%5Bsubject%5D=News&data%5Bstatus%5D=sent&data%5Blist_id%5D=aud123";

        // Act
        var webhook = MailchimpWebhookParser.Parse(body);
        var change = MailchimpChangeMapper.FromWebhook(MailchimpTestContext.List(), webhook, _receivedUtc);

        // Assert
        Assert.Equal("campaign", webhook.Type);
        Assert.False(MailchimpWebhookParser.IsHandled(webhook.Type));
        Assert.Null(change);
    }

    [Fact]
    public void AMissingFiredAt_UsesTheTimeTheSiteReceivedTheWebhook()
    {
        // Arrange
        const string body = "type=unsubscribe&data%5Bemail%5D=a%40example.com";

        // Act
        var webhook = MailchimpWebhookParser.Parse(body);
        var change = MailchimpChangeMapper.FromWebhook(MailchimpTestContext.List(), webhook, _receivedUtc);

        // Assert
        Assert.Null(webhook.FiredUtc);
        Assert.Equal(_receivedUtc, change.OccurredUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("data%5Bemail%5D=a%40example.com")]
    public void ABodyWithoutAType_IsNotAWebhook(string body)
    {
        // Act
        var webhook = MailchimpWebhookParser.Parse(body);

        // Assert
        Assert.Null(webhook);
    }

    [Fact]
    public void AnEventWithoutAnAddress_ChangesNothing()
    {
        // Arrange
        const string body = "type=subscribe&fired_at=2026-11-22+10%3A00%3A00&data%5Blist_id%5D=aud123";

        // Act
        var change = MailchimpChangeMapper.FromWebhook(MailchimpTestContext.List(), MailchimpWebhookParser.Parse(body), _receivedUtc);

        // Assert
        Assert.Null(change);
    }
}
