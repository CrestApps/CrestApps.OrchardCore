using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Consent.Core;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Tests.Modules.Omnichannel.Sms;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Omnichannel;

/// <summary>
/// One consent API over every place consent is kept: the CRM's flags and address opt-outs, the do-not-call lists and a
/// customer's consent to marketing. Each source answers only for its own channels and records, so the dialer, the SMS
/// channel and the campaigns keep the answers they had.
/// </summary>
public sealed class ConsentServiceTests
{
    private static readonly DateTime _now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task TheFirstSourceThatSaysStop_Answers_AndAnExcludedSourceIsntAsked()
    {
        var first = Source("first", optedOut: false);
        var second = Source("second", optedOut: true);
        var third = Source("third", optedOut: true);
        var consent = new DefaultConsentService([first.Object, second.Object, third.Object]);

        var status = await consent.GetAsync(ConsentChannels.Phone, "+17025550100", cancellationToken: TestContext.Current.CancellationToken);
        var excluded = await consent.GetAsync(new ConsentQuery { Channel = ConsentChannels.Phone, Address = "+17025550100", ExcludedSources = ["second", "third"] }, TestContext.Current.CancellationToken);

        Assert.True(status.HasOptedOut);
        Assert.Equal("second", status.Source);
        Assert.False(excluded.HasOptedOut);
        third.Verify(source => source.HasOptedOutAsync(It.IsAny<ConsentQuery>(), It.IsAny<CancellationToken>()), Times.Never());
        second.Verify(source => source.HasOptedOutAsync(It.IsAny<ConsentQuery>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task AChange_IsRecordedWithEverySource()
    {
        var first = Source("first", optedOut: false);
        var second = Source("second", optedOut: false);
        var change = new ConsentChange { OptedOut = true, Source = "Test" };

        await new DefaultConsentService([first.Object, second.Object]).RecordAsync(ConsentChannels.Sms, "+17025550100", change, TestContext.Current.CancellationToken);

        first.Verify(source => source.RecordAsync(ConsentChannels.Sms, "+17025550100", change, It.IsAny<CancellationToken>()), Times.Once());
        second.Verify(source => source.RecordAsync(ConsentChannels.Sms, "+17025550100", change, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task TheCrm_SaysStop_ForAContactWhoOptedOut_OrAnAddressThatDid()
    {
        var resolver = new SharedNumberOptOutResolver();
        var consent = new DefaultConsentService([CrmSource(resolver, out _)]);
        var contact = new ContentItem { ContentItemId = "contact-1", ContentType = "Customer" };

        Assert.False((await consent.GetAsync(new ConsentQuery { Channel = ConsentChannels.Sms, Address = "+17025550100", Contact = contact }, TestContext.Current.CancellationToken)).HasOptedOut);

        resolver.SomebodyAtTheNumberOptedOut = true;

        var status = await consent.GetAsync(new ConsentQuery { Channel = ConsentChannels.Sms, Address = "+17025550100", Contact = contact }, TestContext.Current.CancellationToken);

        Assert.Equal(ConsentSourceNames.ContactPreferences, status.Source);

        resolver.SomebodyAtTheNumberOptedOut = false;
        resolver.AddressOptOuts.Add(new ContactAddressOptOut { Channel = ConsentChannels.Sms, Address = "+17025550199" });

        Assert.True((await consent.GetAsync(ConsentChannels.Sms, "+17025550199", cancellationToken: TestContext.Current.CancellationToken)).HasOptedOut);

        // A customer's marketing question isn't the CRM's to answer.
        resolver.SomebodyAtTheNumberOptedOut = true;

        Assert.False((await consent.GetAsync(new ConsentQuery { Channel = ConsentChannels.Email, Address = "sam@example.com", ContactKind = ConsentContactKinds.Customer, ContactId = "c1" }, TestContext.Current.CancellationToken)).HasOptedOut);
    }

    [Fact]
    public async Task AStopFromANumberNobodyHolds_IsRecordedAgainstTheNumber()
    {
        var resolver = new SharedNumberOptOutResolver();
        var consent = new DefaultConsentService([CrmSource(resolver, out _)]);

        await consent.RecordAsync(ConsentChannels.Sms, "+17025550100", new ConsentChange { OptedOut = true, Source = "SmsKeyword" }, TestContext.Current.CancellationToken);

        var optOut = Assert.Single(resolver.AddressOptOuts);

        Assert.Equal(ConsentChannels.Sms, optOut.Channel);
        Assert.Equal("+17025550100", optOut.Address);
        Assert.Equal("SmsKeyword", optOut.Source);
        Assert.Equal(_now, optOut.OptedOutUtc);

        await consent.RecordAsync(ConsentChannels.Sms, "+17025550100", new ConsentChange { OptedOut = false, Source = "SmsKeyword" }, TestContext.Current.CancellationToken);

        Assert.Empty(resolver.AddressOptOuts);
    }

    [Theory]
    [InlineData("Phone")]
    [InlineData("SMS")]
    [InlineData("Email")]
    public async Task AContactsStopAndStart_SetAndClearTheirFlagForTheChannel(string channel)
    {
        var resolver = new SharedNumberOptOutResolver();
        var contact = new ContentItem { ContentItemId = "contact-1", ContentType = "Customer" };
        var consent = new DefaultConsentService([CrmSource(resolver, out var contentManager, contact)]);

        await consent.RecordAsync(channel, "+17025550100", new ConsentChange { OptedOut = true, ContactKind = ConsentContactKinds.Contact, ContactId = "contact-1" }, TestContext.Current.CancellationToken);

        Assert.True(OmnichannelContactPreferences.HasOptedOut(contact, channel));
        Assert.Empty(resolver.AddressOptOuts);

        await consent.RecordAsync(channel, "+17025550100", new ConsentChange { OptedOut = false, ContactId = "contact-1" }, TestContext.Current.CancellationToken);

        Assert.False(OmnichannelContactPreferences.HasOptedOut(contact, channel));
        contentManager.Verify(manager => manager.UpdateAsync(contact), Times.Exactly(2));
    }

    [Fact]
    public async Task AnUnsubscribe_WithdrawsTheCustomersMarketingConsent_AndOnlyTheirs()
    {
        var customer = new Customer { ItemId = "c1", Email = "sam@example.com", AcceptsMarketing = true };
        var customers = new Mock<ICustomerStore>();
        customers.Setup(store => store.FindByIdAsync("c1", It.IsAny<CancellationToken>())).ReturnsAsync(customer);

        var crm = new SharedNumberOptOutResolver();
        var consent = new DefaultConsentService([CrmSource(crm, out _), new CustomerMarketingConsentSource(customers.Object, Clock())]);
        var query = new ConsentQuery { Channel = ConsentChannels.Email, Address = "sam@example.com", ContactKind = ConsentContactKinds.Customer, ContactId = "c1" };

        Assert.False((await consent.GetAsync(query, TestContext.Current.CancellationToken)).HasOptedOut);

        await consent.RecordAsync(ConsentChannels.Email, "sam@example.com", new ConsentChange
        {
            OptedOut = true,
            Source = "MarketingUnsubscribe",
            ContactKind = ConsentContactKinds.Customer,
            ContactId = "c1",
        }, TestContext.Current.CancellationToken);

        var status = await consent.GetAsync(query, TestContext.Current.CancellationToken);

        Assert.False(customer.AcceptsMarketing);
        Assert.Equal(_now, customer.ModifiedUtc);
        Assert.Equal(ConsentSourceNames.CustomerMarketing, status.Source);
        customers.Verify(store => store.UpdateAsync(customer, It.IsAny<CancellationToken>()), Times.Once());

        // Marketing is not every e-mail: the CRM keeps no opt-out for the address.
        Assert.Empty(crm.AddressOptOuts);
        Assert.False((await consent.GetAsync(ConsentChannels.Email, "sam@example.com", cancellationToken: TestContext.Current.CancellationToken)).HasOptedOut);
    }

    private static Mock<IConsentSource> Source(string name, bool optedOut)
    {
        var source = new Mock<IConsentSource>();
        source.SetupGet(value => value.Name).Returns(name);
        source.Setup(value => value.HasOptedOutAsync(It.IsAny<ConsentQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(optedOut);

        return source;
    }

    private static ContactPreferencesConsentSource CrmSource(SharedNumberOptOutResolver resolver, out Mock<IContentManager> contentManager, ContentItem contact = null)
    {
        contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((string id, VersionOptions _) => contact is not null && contact.ContentItemId == id ? contact : null);

        return new ContactPreferencesConsentSource(resolver, contentManager.Object, Clock());
    }

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }
}
