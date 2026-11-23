using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.MailingLists;
using CrestApps.OrchardCore.MailingLists.Core.Services;
using CrestApps.OrchardCore.MailingLists.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.MailingLists;

/// <summary>
/// The mailing lists consent source only reacts: an e-mail opt-out for marketing or for every purpose unsubscribes the
/// address from every list, an opt-in or another purpose changes nothing, and the changes the module records itself are
/// ignored so they never loop back.
/// </summary>
public sealed class MailingListConsentSourceTests
{
    [Fact]
    public async Task RecordAsync_AMarketingOptOut_UnsubscribesEveryList()
    {
        // Arrange
        var (source, members) = Create();

        // Act
        await source.RecordAsync(ConsentChannels.Email, "ada@example.com", new ConsentChange { OptedOut = true, Purpose = ConsentPurposes.Marketing, Source = "MarketingUnsubscribe" }, TestContext.Current.CancellationToken);

        // Assert
        members.Verify(service => service.UnsubscribeAsync(null, "ada@example.com", "Consent", "MarketingUnsubscribe", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordAsync_ADoNotEmailForEveryPurpose_UnsubscribesEveryList()
    {
        // Arrange
        var (source, members) = Create();

        // Act
        await source.RecordAsync(ConsentChannels.Email, "ada@example.com", new ConsentChange { OptedOut = true, Source = "ContactPreferences" }, TestContext.Current.CancellationToken);

        // Assert
        members.Verify(service => service.UnsubscribeAsync(null, "ada@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordAsync_AChangeTheModuleRecordedItself_IsIgnored()
    {
        // Arrange
        var (source, members) = Create();

        // Act
        await source.RecordAsync(ConsentChannels.Email, "ada@example.com", new ConsentChange { OptedOut = true, Purpose = ConsentPurposes.Marketing, Source = MailingListsConstants.ConsentSourceName }, TestContext.Current.CancellationToken);

        // Assert
        members.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, "Email", "Marketing")]
    [InlineData(true, "SMS", "Marketing")]
    [InlineData(true, "Email", "Support")]
    public async Task RecordAsync_AnOptInAnotherChannelOrAnotherPurpose_ChangesNoList(bool optedOut, string channel, string purpose)
    {
        // Arrange
        var (source, members) = Create();

        // Act
        await source.RecordAsync(channel, "ada@example.com", new ConsentChange { OptedOut = optedOut, Purpose = purpose, Source = "Somewhere" }, TestContext.Current.CancellationToken);

        // Assert
        members.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HasOptedOutAsync_NeverSaysAnAddressOptedOut()
    {
        // Arrange
        var (source, _) = Create();

        // Act
        var optedOut = await source.HasOptedOutAsync(new ConsentQuery { Channel = ConsentChannels.Email, Address = "ada@example.com", Purpose = ConsentPurposes.Marketing }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(optedOut);
    }

    private static (MailingListConsentSource Source, Mock<IMailingListMemberService> Members) Create()
    {
        var members = new Mock<IMailingListMemberService>();

        members.Setup(service => service.UnsubscribeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var provider = new ServiceCollection()
            .AddSingleton(members.Object)
            .BuildServiceProvider();

        return (new MailingListConsentSource(provider), members);
    }
}
