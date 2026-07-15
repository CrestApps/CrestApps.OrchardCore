using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.DialPad;
using CrestApps.OrchardCore.DialPad.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Localization;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class DialPadContactCenterVoiceProviderTests
{
    [Fact]
    public async Task DialAsync_WhenTelephonyOutcomeIsUnknown_PreservesUnknownOutcome()
    {
        // Arrange
        var telephonyProvider = new Mock<ITelephonyProvider>();
        telephonyProvider
            .Setup(provider => provider.DialAsync(It.IsAny<DialRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Unknown("The provider response was lost."));

        var resolver = new Mock<ITelephonyProviderResolver>();
        resolver
            .Setup(provider => provider.GetAsync(DialPadConstants.ProviderTechnicalName))
            .ReturnsAsync(telephonyProvider.Object);

        var localizer = new Mock<IStringLocalizer<DialPadContactCenterVoiceProvider>>();
        localizer
            .Setup(value => value["DialPad"])
            .Returns(new LocalizedString("DialPad", "DialPad"));
        var service = new DialPadContactCenterVoiceProvider(
            resolver.Object,
            new TestContactCenterFeatureWorkManager(),
            localizer.Object);

        // Act
        var result = await service.DialAsync(new ContactCenterDialRequest
        {
            Destination = "+15551234567",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.True(result.OutcomeUnknown);
        Assert.Equal("dial_outcome_unknown", result.ErrorCode);
    }
}
