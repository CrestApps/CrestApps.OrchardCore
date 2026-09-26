using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// The workspace lists customers, not threads, and shows one tab per channel above a customer's conversation, so an
/// agent switching channel stays in the same view. These pin how the tabs, the customer rows and the channel registry
/// behave, since a mistake there either hides a waiting customer or badges the thread the agent is already reading.
/// </summary>
public sealed class MessagingChannelWorkspaceTests
{
    [Fact]
    public void CustomerKey_IsTheContact_WhenOneIsLinked_SoEveryChannelOfThatContactSharesIt()
    {
        var sms = new MessagingConversation { Channel = "SMS", ContactAddress = "+15551112222", ContactContentItemId = "contact-1" };
        var email = new MessagingConversation { Channel = "Email", ContactAddress = "ann@example.com", ContactContentItemId = "contact-1" };

        Assert.Equal(sms.GetCustomerKey(), email.GetCustomerKey());
    }

    [Fact]
    public void CustomerKey_IsTheChannelAndAddress_ForAnUnknownSender()
    {
        var first = new MessagingConversation { Channel = "SMS", ContactAddress = "+15551112222" };
        var sameSenderOtherEndpoint = new MessagingConversation { Channel = "sms", ContactAddress = "+15551112222", ServiceAddress = "+15550000000" };
        var otherChannel = new MessagingConversation { Channel = "Email", ContactAddress = "+15551112222" };

        Assert.Equal(first.GetCustomerKey(), sameSenderOtherEndpoint.GetCustomerKey());
        Assert.NotEqual(first.GetCustomerKey(), otherChannel.GetCustomerKey());
    }

    [Fact]
    public void Tabs_OfferEveryEnabledChannel_AndMarkTheOneOnScreenActive()
    {
        var current = Conversation("c-sms", "SMS", unread: 0);

        var tabs = BuildTabs([Channel("SMS", 0), Channel("Email", 1)], current, [current]);

        Assert.Equal(["SMS", "Email"], tabs.Select(tab => tab.Name));
        Assert.True(tabs[0].IsActive);
        Assert.False(tabs[1].IsActive);
    }

    [Fact]
    public void Tabs_BadgeTheUnreadMessagesWaitingOnAnotherChannel_ButNeverTheThreadOnScreen()
    {
        // The thread on screen has just been read, so a badge on its own tab would count messages the agent is looking at.
        var current = Conversation("c-sms", "SMS", unread: 4);
        var email = Conversation("c-email", "Email", unread: 2);

        var tabs = BuildTabs([Channel("SMS", 0), Channel("Email", 1)], current, [current, email]);

        Assert.Equal(0, tabs[0].UnreadCount);
        Assert.Equal(2, tabs[1].UnreadCount);
        Assert.Equal("/conversation/c-email", tabs[1].Url);
    }

    [Fact]
    public void Tabs_OfferToStartAConversation_OnAChannelTheCustomerHasAnAddressOn()
    {
        var current = Conversation("c-sms", "SMS", unread: 0);

        var tabs = BuildTabs(
            [Channel("SMS", 0), Channel("Email", 1)],
            current,
            [current],
            new Dictionary<string, IReadOnlyList<string>> { ["Email"] = ["ann@example.com"] });

        Assert.False(tabs[1].HasConversation);
        Assert.Equal("/start/Email/ann@example.com", tabs[1].Url);
    }

    [Fact]
    public void Tabs_AreDisabled_OnAChannelTheCustomerCannotBeReachedOn()
    {
        var current = Conversation("c-sms", "SMS", unread: 0);

        var tabs = BuildTabs([Channel("SMS", 0), Channel("Email", 1)], current, [current]);

        Assert.Null(tabs[1].Url);
        Assert.False(string.IsNullOrEmpty(tabs[1].Tooltip));
    }

    [Fact]
    public void Tabs_LeadToTheMostRecentConversation_WhenTheCustomerHasSeveralOnAChannel()
    {
        var current = Conversation("c-sms", "SMS", unread: 0);
        var older = Conversation("c-email-old", "Email", unread: 1, lastMessageUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var newer = Conversation("c-email-new", "Email", unread: 1, lastMessageUtc: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        var tabs = BuildTabs([Channel("SMS", 0), Channel("Email", 1)], current, [current, older, newer]);

        Assert.Equal("/conversation/c-email-new", tabs[1].Url);
        Assert.Equal(2, tabs[1].UnreadCount);
    }

    [Fact]
    public void InboxRows_FoldEachCustomersConversationsIntoOneRow_LedByTheMostRecent()
    {
        var latestSms = Conversation("c1", "SMS", unread: 1, contactId: "contact-1");
        var otherCustomer = Conversation("c2", "SMS", unread: 3, contactId: "contact-2");
        var olderEmail = Conversation("c3", "Email", unread: 2, contactId: "contact-1");

        var groups = InboxRowGrouping.Group([latestSms, otherCustomer, olderEmail]);

        Assert.Equal(2, groups.Count);
        Assert.Same(latestSms, groups[0].Latest);
        Assert.Equal(3, groups[0].UnreadCount);
        Assert.Equal(["SMS", "Email"], groups[0].Channels);
        Assert.Same(otherCustomer, groups[1].Latest);
    }

    [Fact]
    public void ChannelResolver_ListsChannelsInDisplayOrder_AndFindsThemWithoutRegardToCase()
    {
        var email = Channel("Email", 5);
        var sms = Channel("SMS", 0);

        var resolver = new MessagingChannelResolver([email, sms]);

        Assert.Equal(["SMS", "Email"], resolver.GetAll().Select(channel => channel.Name));
        Assert.Same(sms, resolver.Get("sms"));
        Assert.Null(resolver.Get("WhatsApp"));
        Assert.Null(resolver.Get(null));
    }

    [Theory]
    [InlineData("+15551112222", true)]
    [InlineData("+1 555 111 2222", true)]
    [InlineData("5551112222", true)]
    [InlineData("12345", false)]
    [InlineData("ann@example.com", false)]
    [InlineData("", false)]
    public void SmsChannel_AcceptsOnlyPhoneNumbers(string address, bool valid)
    {
        var channel = MessagingTestChannels.Sms(Mock.Of<ISmsDispatcher>());

        Assert.Equal(valid, channel.IsValidAddress(address));
    }

    [Fact]
    public void SmsChannel_OffersTheContactsCellNumberFirst()
    {
        var channel = MessagingTestChannels.Sms(Mock.Of<ISmsDispatcher>());
        var contact = ContactWithPhones(("Home", "+15550000001"), ("Cell", "+15550000002"));

        Assert.Equal(["+15550000002", "+15550000001"], channel.GetContactAddresses(contact));
    }

    [Fact]
    public void SmsChannel_ReadsTheDoNotSmsFlag_AsTheOptOut()
    {
        var channel = MessagingTestChannels.Sms(Mock.Of<ISmsDispatcher>());
        var contact = new ContentItem { ContentType = "Customer" };

        Assert.False(channel.IsOptedOut(contact));

        contact.Alter<OmnichannelContactPart>(part => part.SetDoNotSms(true, DateTime.UtcNow));

        Assert.True(channel.IsOptedOut(contact));
    }

    [Fact]
    public async Task SmsChannel_SendsThroughTheDispatcher_FromTheConversationsNumber()
    {
        var dispatcher = MessagingTestChannels.AcceptingDispatcher();
        var channel = MessagingTestChannels.Sms(dispatcher.Object);

        var result = await channel.SendAsync(
            new MessagingOutboundMessage { ServiceAddress = "+15553334444", ContactAddress = "+15551112222", Body = "hi" },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        dispatcher.Verify(
            d => d.SendAsync(It.Is<SmsMessage>(m => m.From == "+15553334444" && m.To == "+15551112222" && m.Body == "hi"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static IReadOnlyList<ChannelTabViewModel> BuildTabs(
        IReadOnlyList<IMessagingChannel> channels,
        MessagingConversation current,
        IReadOnlyList<MessagingConversation> customerConversations,
        IReadOnlyDictionary<string, IReadOnlyList<string>> addresses = null)
        => ChannelTabsBuilder.Build(
            channels,
            current,
            customerConversations,
            addresses ?? new Dictionary<string, IReadOnlyList<string>>(),
            conversation => $"/conversation/{conversation.ItemId}",
            (channel, address) => $"/start/{channel}/{address}",
            new PassThroughLocalizer());

    private static MessagingConversation Conversation(string id, string channel, int unread, string contactId = "contact-1", DateTime? lastMessageUtc = null)
        => new()
        {
            ItemId = id,
            Channel = channel,
            ContactAddress = "+15551112222",
            ContactContentItemId = contactId,
            UnreadCount = unread,
            LastMessageUtc = lastMessageUtc ?? DateTime.UtcNow,
        };

    private static IMessagingChannel Channel(string name, int order)
    {
        var channel = new Mock<IMessagingChannel>();

        channel.SetupGet(c => c.Name).Returns(name);
        channel.SetupGet(c => c.Order).Returns(order);
        channel.SetupGet(c => c.DisplayName).Returns(new LocalizedString(name, name));
        channel.SetupGet(c => c.IconCssClass).Returns("fa-solid fa-circle");
        channel.SetupGet(c => c.Capabilities).Returns(new MessagingChannelCapabilities());

        return channel.Object;
    }

    private static ContentItem ContactWithPhones(params (string Type, string Number)[] phones)
    {
        var contact = new ContentItem { ContentType = "Customer" };
        var bag = new BagPart();

        foreach (var (type, number) in phones)
        {
            var method = new ContentItem { ContentType = OmnichannelConstants.ContentTypes.PhoneNumber };
            method.Alter<PhoneNumberInfoPart>(part =>
            {
                part.Type = new TextField { Text = type };
                part.Number = new PhoneField { PhoneNumber = number };
            });

            bag.ContentItems.Add(method);
        }

        contact.Weld(OmnichannelConstants.NamedParts.ContactMethods, bag);

        return contact;
    }

    private sealed class PassThroughLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
