using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Consent.Core;
using CrestApps.OrchardCore.Consent.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Tests.Modules.Omnichannel.Sms;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Consent;

/// <summary>
/// The purpose rule over the consent service: an opt-out with a purpose stops questions for that purpose, an opt-out
/// with no purpose stops every question, and a question with no purpose asks only whether everything is stopped. A
/// "stop all marketing" never becomes a Do not email.
/// </summary>
public sealed class DefaultConsentServicePurposeTests
{
    private const string Address = "sam@example.com";

    private static readonly DateTime _now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AMarketingOptOut_StopsMarketing_ButNotAQuestionWithNoPurpose()
    {
        // Arrange
        var (consent, crm, addresses) = Create();

        // Act
        await consent.RecordAsync(ConsentChannels.Email, Address, new ConsentChange { OptedOut = true, Purpose = ConsentPurposes.Marketing, Source = "unsubscribe-link" }, TestContext.Current.CancellationToken);

        var marketing = await consent.GetAsync(Query(ConsentPurposes.Marketing), TestContext.Current.CancellationToken);
        var everything = await consent.GetAsync(Query(null), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ConsentSourceNames.AddressOptOuts, marketing.Source);
        Assert.False(everything.HasOptedOut);
        Assert.Empty(crm.AddressOptOuts);
        Assert.Single(addresses.Records);
    }

    [Fact]
    public async Task AnOptOutOfEveryPurpose_StopsMarketingToo()
    {
        // Arrange
        var (consent, crm, addresses) = Create();

        // Act
        await consent.RecordAsync(ConsentChannels.Email, Address, new ConsentChange { OptedOut = true, Source = "Complaint" }, TestContext.Current.CancellationToken);

        var marketing = await consent.GetAsync(Query(ConsentPurposes.Marketing), TestContext.Current.CancellationToken);
        var everything = await consent.GetAsync(Query(null), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ConsentSourceNames.ContactPreferences, marketing.Source);
        Assert.Equal(ConsentSourceNames.ContactPreferences, everything.Source);
        Assert.Single(crm.AddressOptOuts);
        Assert.Empty(addresses.Records);
    }

    [Fact]
    public async Task AMarketingOptIn_LiftsTheMarketingOptOut_AndLeavesAnOptOutOfEveryPurpose()
    {
        // Arrange
        var (consent, crm, _) = Create();
        await consent.RecordAsync(ConsentChannels.Email, Address, new ConsentChange { OptedOut = true, Purpose = ConsentPurposes.Marketing }, TestContext.Current.CancellationToken);
        await consent.RecordAsync(ConsentChannels.Email, Address, new ConsentChange { OptedOut = true }, TestContext.Current.CancellationToken);

        // Act
        await consent.RecordAsync(ConsentChannels.Email, Address, new ConsentChange { OptedOut = false, Purpose = ConsentPurposes.Marketing }, TestContext.Current.CancellationToken);

        var marketing = await consent.GetAsync(Query(ConsentPurposes.Marketing), TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(crm.AddressOptOuts);
        Assert.Equal(ConsentSourceNames.ContactPreferences, marketing.Source);
    }

    [Fact]
    public async Task AMarketingOptOutForAContact_DoesNotSetDoNotEmail()
    {
        // Arrange
        var contact = new ContentItem { ContentItemId = "contact-1", ContentType = "Customer" };
        var (consent, _, _) = Create(contact);

        // Act
        await consent.RecordAsync(ConsentChannels.Email, Address, new ConsentChange
        {
            OptedOut = true,
            Purpose = ConsentPurposes.Marketing,
            ContactKind = ConsentContactKinds.Contact,
            ContactId = "contact-1",
        }, TestContext.Current.CancellationToken);

        var marketing = await consent.GetAsync(new ConsentQuery { Channel = ConsentChannels.Email, Address = Address, Contact = contact, Purpose = ConsentPurposes.Marketing }, TestContext.Current.CancellationToken);
        var conversation = await consent.GetAsync(new ConsentQuery { Channel = ConsentChannels.Email, Address = Address, Contact = contact }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(OmnichannelContactPreferences.HasOptedOut(contact, ConsentChannels.Email));
        Assert.True(marketing.HasOptedOut);
        Assert.False(conversation.HasOptedOut);
    }

    [Fact]
    public async Task AContactsDoNotEmail_StopsMarketingQuestionsAboutTheContact()
    {
        // Arrange
        var contact = new ContentItem { ContentItemId = "contact-1", ContentType = "Customer" };
        var (consent, _, _) = Create(contact);
        await consent.RecordAsync(ConsentChannels.Email, Address, new ConsentChange { OptedOut = true, ContactKind = ConsentContactKinds.Contact, ContactId = "contact-1" }, TestContext.Current.CancellationToken);

        // Act
        var marketing = await consent.GetAsync(new ConsentQuery { Channel = ConsentChannels.Email, Address = Address, Contact = contact, Purpose = ConsentPurposes.Marketing }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(OmnichannelContactPreferences.HasOptedOut(contact, ConsentChannels.Email));
        Assert.Equal(ConsentSourceNames.ContactPreferences, marketing.Source);
    }

    private static (IConsentService Consent, SharedNumberOptOutResolver Crm, InMemoryConsentAddressOptOutStore Addresses) Create(ContentItem contact = null)
    {
        var crm = new SharedNumberOptOutResolver();
        var addresses = new InMemoryConsentAddressOptOutStore();
        var clock = new TestClock(_now);
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((string id, VersionOptions _) => contact is not null && contact.ContentItemId == id ? contact : null);

        var consent = new DefaultConsentService(
        [
            new ContactPreferencesConsentSource(crm, contentManager.Object, clock),
            new AddressOptOutConsentSource(addresses, clock),
        ]);

        return (consent, crm, addresses);
    }

    private static ConsentQuery Query(string purpose)
        => new()
        {
            Channel = ConsentChannels.Email,
            Address = Address,
            Purpose = purpose,
        };
}
