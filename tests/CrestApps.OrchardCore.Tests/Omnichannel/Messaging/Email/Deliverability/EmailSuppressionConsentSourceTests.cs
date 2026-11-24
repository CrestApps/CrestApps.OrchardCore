using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email.Deliverability;

public sealed class EmailSuppressionConsentSourceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(ConsentContactKinds.Contact)]
    [InlineData(ConsentContactKinds.Customer)]
    public async Task HasOptedOutAsync_ASuppressedAddress_IsOptedOutOfEmailForEveryKindOfRecord(string contactKind)
    {
        // Arrange
        var suppressions = new InMemoryEmailSuppressionList();
        await suppressions.SuppressAsync("gone@example.com", EmailSuppressionReason.HardBounce, "5.1.1", cancellationToken: TestContext.Current.CancellationToken);
        var source = new EmailSuppressionConsentSource(suppressions);

        // Act
        var optedOut = await source.HasOptedOutAsync(
            new ConsentQuery { Channel = ConsentChannels.Email, Address = "Gone@Example.com", ContactKind = contactKind, ContactId = "contact-1" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(optedOut);
        Assert.Equal(EmailSuppressionConsentSource.SourceName, source.Name);
    }

    [Fact]
    public async Task HasOptedOutAsync_AnotherChannelOrAnAddressNotOnTheList_IsNotOptedOut()
    {
        // Arrange
        var suppressions = new InMemoryEmailSuppressionList();
        await suppressions.SuppressAsync("gone@example.com", EmailSuppressionReason.Complaint, "spam", cancellationToken: TestContext.Current.CancellationToken);
        var source = new EmailSuppressionConsentSource(suppressions);

        // Act
        var otherChannel = await source.HasOptedOutAsync(new ConsentQuery { Channel = ConsentChannels.Sms, Address = "gone@example.com" }, TestContext.Current.CancellationToken);
        var otherAddress = await source.HasOptedOutAsync(new ConsentQuery { Channel = ConsentChannels.Email, Address = "ann@example.com" }, TestContext.Current.CancellationToken);
        var noAddress = await source.HasOptedOutAsync(new ConsentQuery { Channel = ConsentChannels.Email }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(otherChannel);
        Assert.False(otherAddress);
        Assert.False(noAddress);
    }

    [Fact]
    public async Task RecordAsync_AnOptIn_DoesNotTakeTheAddressOffTheList()
    {
        // Arrange
        var suppressions = new InMemoryEmailSuppressionList();
        await suppressions.SuppressAsync("gone@example.com", EmailSuppressionReason.HardBounce, "5.1.1", cancellationToken: TestContext.Current.CancellationToken);
        var source = new EmailSuppressionConsentSource(suppressions);

        // Act
        await source.RecordAsync(ConsentChannels.Email, "gone@example.com", new ConsentChange { OptedOut = false, Source = "Admin" }, TestContext.Current.CancellationToken);

        // Assert: the list is kept by bounces, complaints and the people who manage it, never by a consent change.
        Assert.True(suppressions.Items.ContainsKey("gone@example.com"));
    }
}
