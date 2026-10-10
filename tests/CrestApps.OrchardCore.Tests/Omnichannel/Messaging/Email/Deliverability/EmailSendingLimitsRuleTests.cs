using System.ComponentModel.DataAnnotations;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email.Deliverability;

public sealed class EmailSendingLimitsRuleTests
{
    [Theory]
    [InlineData(-1, 100, 1, false, 1, "MaxPerHour")]
    [InlineData(500, 100, 2, false, 1, "MaxPerHour")]
    [InlineData(100, 1000, 2, true, 0, "WarmUpFirstDayLimit")]
    public async Task ValidateAsync_LimitsThatCannotWork_AreRefused(int perHour, int perDay, int gap, bool warmUp, int firstDay, string member)
    {
        // Arrange
        var context = Validate(new EmailSendingLimits
        {
            MaxPerHour = perHour,
            MaxPerDay = perDay,
            MinimumSecondsBetweenSends = gap,
            WarmUp = warmUp,
            WarmUpFirstDayLimit = firstDay,
        });

        // Act
        await new EmailAddressSettingsRule(new PassThroughStringLocalizer<EmailAddressSettingsRule>()).ValidateAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(context.Result.Succeeded);
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(member));
    }

    [Fact]
    public async Task ValidateAsync_NoLimitAtAll_IsAllowed()
    {
        // Arrange
        var context = Validate(new EmailSendingLimits { MaxPerHour = 0, MaxPerDay = 0, MinimumSecondsBetweenSends = 0 });

        // Act
        await new EmailAddressSettingsRule(new PassThroughStringLocalizer<EmailAddressSettingsRule>()).ValidateAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    private static ValidatingContext<OmnichannelChannelEndpoint> Validate(EmailSendingLimits limits)
    {
        var address = new OmnichannelChannelEndpoint { Capabilities = [OmnichannelConstants.Channels.Email] };
        address.Put(new EmailAddressSettings { Limits = limits });

        return new ValidatingContext<OmnichannelChannelEndpoint>(address);
    }
}
