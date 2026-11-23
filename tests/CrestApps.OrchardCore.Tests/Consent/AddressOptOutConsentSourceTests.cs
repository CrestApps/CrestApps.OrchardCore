using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Consent.Core.Models;
using CrestApps.OrchardCore.Consent.Core.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;

namespace CrestApps.OrchardCore.Tests.Consent;

/// <summary>
/// The address opt-outs answer a question for a purpose when the address stopped that purpose, whoever the question
/// names; they never answer a question with no purpose, and keep only changes with a purpose.
/// </summary>
public sealed class AddressOptOutConsentSourceTests
{
    private static readonly DateTime _now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RecordAsync_AMarketingOptOut_IsKeptForTheNormalizedAddress()
    {
        // Arrange
        var store = new InMemoryConsentAddressOptOutStore();
        var source = new AddressOptOutConsentSource(store, new TestClock(_now));

        // Act
        await source.RecordAsync(ConsentChannels.Email, " Sam@Example.com ", Change(optedOut: true, ConsentPurposes.Marketing), TestContext.Current.CancellationToken);

        // Assert
        var record = Assert.Single(store.Records);

        Assert.Equal(ConsentChannels.Email, record.Channel);
        Assert.Equal("sam@example.com", record.Address);
        Assert.Equal(ConsentPurposes.Marketing, record.Purpose);
        Assert.Equal("unsubscribe-link", record.Source);
        Assert.Equal(_now, record.OptedOutUtc);
    }

    [Fact]
    public async Task HasOptedOutAsync_AnswersOnlyForThePurposeAndChannelTheAddressStopped()
    {
        // Arrange
        var store = new InMemoryConsentAddressOptOutStore();
        var source = new AddressOptOutConsentSource(store, new TestClock(_now));
        await source.RecordAsync(ConsentChannels.Email, "sam@example.com", Change(optedOut: true, ConsentPurposes.Marketing), TestContext.Current.CancellationToken);

        // Act
        var marketing = await source.HasOptedOutAsync(Query(ConsentPurposes.Marketing), TestContext.Current.CancellationToken);
        var otherCase = await source.HasOptedOutAsync(Query(ConsentPurposes.Marketing, "SAM@example.COM"), TestContext.Current.CancellationToken);
        var customerCampaign = await source.HasOptedOutAsync(Query(ConsentPurposes.Marketing, kind: ConsentContactKinds.Customer, contactId: "c1"), TestContext.Current.CancellationToken);
        var everyPurpose = await source.HasOptedOutAsync(Query(null), TestContext.Current.CancellationToken);
        var otherPurpose = await source.HasOptedOutAsync(Query("Surveys"), TestContext.Current.CancellationToken);
        var otherAddress = await source.HasOptedOutAsync(Query(ConsentPurposes.Marketing, "kim@example.com"), TestContext.Current.CancellationToken);
        var otherChannel = await source.HasOptedOutAsync(new ConsentQuery { Channel = ConsentChannels.Sms, Address = "sam@example.com", Purpose = ConsentPurposes.Marketing }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(marketing);
        Assert.True(otherCase);
        Assert.True(customerCampaign);
        Assert.False(everyPurpose);
        Assert.False(otherPurpose);
        Assert.False(otherAddress);
        Assert.False(otherChannel);
    }

    [Fact]
    public async Task RecordAsync_AnOptIn_ClearsEveryOptOutOfThatPurpose_AndLeavesOtherPurposes()
    {
        // Arrange
        var store = new InMemoryConsentAddressOptOutStore();
        store.Records.Add(new ConsentAddressOptOut { Id = 1, ItemId = "a", Channel = ConsentChannels.Email, Address = "sam@example.com", Purpose = ConsentPurposes.Marketing });
        store.Records.Add(new ConsentAddressOptOut { Id = 2, ItemId = "b", Channel = ConsentChannels.Email, Address = "sam@example.com", Purpose = ConsentPurposes.Marketing });
        store.Records.Add(new ConsentAddressOptOut { Id = 3, ItemId = "c", Channel = ConsentChannels.Email, Address = "sam@example.com", Purpose = "Surveys" });
        var source = new AddressOptOutConsentSource(store, new TestClock(_now));

        // Act
        await source.RecordAsync(ConsentChannels.Email, "Sam@Example.com", Change(optedOut: false, ConsentPurposes.Marketing), TestContext.Current.CancellationToken);

        // Assert
        var left = Assert.Single(store.Records);

        Assert.Equal("Surveys", left.Purpose);
        Assert.False(await source.HasOptedOutAsync(Query(ConsentPurposes.Marketing), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RecordAsync_ARepeatedOptOut_UpdatesTheRecordInsteadOfAddingOne()
    {
        // Arrange
        var store = new InMemoryConsentAddressOptOutStore();
        var clock = new TestClock(_now);
        var source = new AddressOptOutConsentSource(store, clock);
        await source.RecordAsync(ConsentChannels.Email, "sam@example.com", Change(optedOut: true, ConsentPurposes.Marketing), TestContext.Current.CancellationToken);
        clock.UtcNow = _now.AddDays(3);

        // Act
        await source.RecordAsync(ConsentChannels.Email, "sam@example.com", new ConsentChange { OptedOut = true, Purpose = ConsentPurposes.Marketing, Source = "MarketingUnsubscribe" }, TestContext.Current.CancellationToken);

        // Assert
        var record = Assert.Single(store.Records);

        Assert.Equal("MarketingUnsubscribe", record.Source);
        Assert.Equal(_now.AddDays(3), record.OptedOutUtc);
    }

    [Theory]
    [InlineData(null, "sam@example.com")]
    [InlineData("", "sam@example.com")]
    [InlineData(ConsentPurposes.Marketing, null)]
    [InlineData(ConsentPurposes.Marketing, " ")]
    public async Task RecordAsync_WithoutAPurposeOrAnAddress_KeepsNothing(string purpose, string address)
    {
        // Arrange
        var store = new InMemoryConsentAddressOptOutStore();
        var source = new AddressOptOutConsentSource(store, new TestClock(_now));

        // Act
        await source.RecordAsync(ConsentChannels.Email, address, Change(optedOut: true, purpose), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(store.Records);
    }

    private static ConsentChange Change(bool optedOut, string purpose)
        => new()
        {
            OptedOut = optedOut,
            Purpose = purpose,
            Source = "unsubscribe-link",
        };

    private static ConsentQuery Query(string purpose, string address = "sam@example.com", string kind = null, string contactId = null)
        => new()
        {
            Channel = ConsentChannels.Email,
            Address = address,
            Purpose = purpose,
            ContactKind = kind,
            ContactId = contactId,
        };
}
