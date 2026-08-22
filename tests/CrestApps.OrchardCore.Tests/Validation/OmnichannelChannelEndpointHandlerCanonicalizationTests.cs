using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using Moq;
using OrchardCore.Email;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Validation;

/// <summary>
/// Pins the canonical form of a channel endpoint to the handler so every write path stores the same value.
/// </summary>
/// <remarks>
/// An endpoint is matched against inbound traffic by an exact comparison of its value, so an endpoint stored in the
/// form it happened to be typed in never matches anything. Canonicalizing in the editor's driver left the recipe path
/// storing the raw value; canonicalizing only when an entry is initialized or updated left the editor's create path
/// storing it, because an editor builds the entry before it binds the form to it and only the create runs afterwards.
/// </remarks>
public class OmnichannelChannelEndpointHandlerCanonicalizationTests
{
    [Theory]
    [InlineData(OmnichannelConstants.Channels.Phone)]
    [InlineData(OmnichannelConstants.Channels.Sms)]
    public async Task CreatingAsync_ForATelephonyEndpoint_StoresTheCanonicalNumber(string channel)
    {
        // Arrange
        var endpoint = new OmnichannelChannelEndpoint
        {
            Channel = channel,
            Value = "  (415) 555-2671  ",
        };

        var handler = CreateHandler();

        // Act
        await handler.CreatingAsync(new CreatingContext<OmnichannelChannelEndpoint>(endpoint), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("+14155552671", endpoint.Value);
    }

    [Fact]
    public async Task CreatingAsync_ForANonTelephonyEndpoint_OnlyTrimsTheValue()
    {
        // Arrange
        var endpoint = new OmnichannelChannelEndpoint
        {
            Channel = OmnichannelConstants.Channels.Email,
            Value = "  someone@example.com  ",
        };

        var handler = CreateHandler();

        // Act
        await handler.CreatingAsync(new CreatingContext<OmnichannelChannelEndpoint>(endpoint), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("someone@example.com", endpoint.Value);
    }

    [Fact]
    public async Task CreatingAsync_WhenTheNumberCannotBeParsed_LeavesTheValueForTheRulesToRefuse()
    {
        // Arrange
        var endpoint = new OmnichannelChannelEndpoint
        {
            Channel = OmnichannelConstants.Channels.Phone,
            Value = " not-a-number ",
        };

        var handler = CreateHandler();

        // Act
        await handler.CreatingAsync(new CreatingContext<OmnichannelChannelEndpoint>(endpoint), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("not-a-number", endpoint.Value);
    }

    [Fact]
    public async Task CreatingAsync_WhenTheValueIsMissing_DoesNotThrow()
    {
        // Arrange
        var endpoint = new OmnichannelChannelEndpoint
        {
            Channel = OmnichannelConstants.Channels.Phone,
            Value = null,
        };

        var handler = CreateHandler();

        // Act
        await handler.CreatingAsync(new CreatingContext<OmnichannelChannelEndpoint>(endpoint), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(endpoint.Value);
    }

    private static OmnichannelChannelEndpointHandler CreateHandler()
    {
        var phoneNumberService = new Mock<IPhoneNumberService>();

        phoneNumberService
            .Setup(service => service.TryFormatToE164(It.IsAny<string>(), It.IsAny<string>(), out It.Ref<string>.IsAny))
            .Returns(new TryFormatToE164Callback((string rawNumber, string regionCode, out string e164Number) =>
            {
                e164Number = "+14155552671";

                return rawNumber == "(415) 555-2671";
            }));

        return new OmnichannelChannelEndpointHandler(
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IClock>().Object,
            phoneNumberService.Object,
            new Mock<IEmailAddressValidator>().Object,
            new PassThroughStringLocalizer<OmnichannelCampaignHandler>());
    }

    private delegate bool TryFormatToE164Callback(string rawNumber, string regionCode, out string e164Number);
}
