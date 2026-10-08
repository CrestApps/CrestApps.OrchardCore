using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Drivers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Sms;

/// <summary>
/// The SMS endpoint editor's provider picker names the SMS provider that sends from the endpoint's number. The endpoint
/// editor was split into cards, confirmed live; these pin which endpoints carry the picker and what it stores.
/// </summary>
public sealed class SmsEndpointProviderDisplayDriverTests
{
    [Fact]
    public void Edit_ForAnEndpointOnAnotherChannel_AddsNothing()
    {
        // Arrange
        var endpoint = new OmnichannelChannelEndpoint { Channel = "Email", ProviderName = "Mailer" };

        // Act
        var result = CreateDriver().Edit(endpoint, PostedFormUpdateModel.CreateContext(new SmsEndpointProviderViewModel()));

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("  Twilio ", "Twilio")]
    [InlineData("Telnyx", "Telnyx")]
    [InlineData(null, null)]
    public async Task UpdateAsync_ForAnSmsEndpoint_StoresTheProviderNameTrimmed(string posted, string expected)
    {
        // Arrange
        var endpoint = new OmnichannelChannelEndpoint { Channel = OmnichannelConstants.Channels.Sms };

        // Act
        await CreateDriver().UpdateAsync(endpoint, PostedFormUpdateModel.CreateContext(new SmsEndpointProviderViewModel { ProviderName = posted }));

        // Assert
        Assert.Equal(expected, endpoint.ProviderName);
    }

    // The picker is not on another channel's editor, so nothing it could post may change that endpoint's provider.
    [Fact]
    public async Task UpdateAsync_ForAnEndpointOnAnotherChannel_LeavesItsProviderAlone()
    {
        // Arrange
        var endpoint = new OmnichannelChannelEndpoint { Channel = "Email", ProviderName = "Mailer" };

        // Act
        await CreateDriver().UpdateAsync(endpoint, PostedFormUpdateModel.CreateContext(new SmsEndpointProviderViewModel { ProviderName = "Twilio" }));

        // Assert
        Assert.Equal("Mailer", endpoint.ProviderName);
    }

    private static SmsEndpointProviderDisplayDriver CreateDriver()
    {
        var options = new SmsProviderOptions();
        var providerType = Mock.Of<ISmsProvider>().GetType();

        options.TryAddProvider("Twilio", new SmsProviderTypeOptions(providerType) { IsEnabled = true });

        return new SmsEndpointProviderDisplayDriver(new TestOptionsMonitor<SmsProviderOptions>(options));
    }
}
