using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The dialer profile editor was split into cards (General, Dialing, Caller ID, Compliance, Abandonment and safe harbor)
/// that post one form, confirmed live. These pin what the driver stores from that post.
/// </summary>
public sealed class DialerProfileDisplayDriverTests
{
    // The region is an ISO 3166-1 alpha-2 code, stored upper-cased as the region picker lists it.
    [Theory]
    [InlineData(" us ", "US")]
    [InlineData("gb", "GB")]
    [InlineData("CA", "CA")]
    [InlineData(null, null)]
    public async Task UpdateAsync_StoresTheDefaultRegionUpperCased(string posted, string expected)
    {
        // Arrange
        var profile = new DialerProfile { ItemId = "profile-1" };
        var model = new DialerProfileViewModel { Name = "Renewals", Mode = DialerMode.Preview, DefaultRegionCode = posted };

        // Act
        await CreateDriver().UpdateAsync(profile, PostedFormUpdateModel.CreateContext(model));

        // Assert
        Assert.Equal(expected, profile.DefaultRegionCode);
    }

    // No provider means the tenant's default voice provider places the calls; a blank choice must read as that, not as a
    // provider whose name is empty.
    [Theory]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    [InlineData(" Telnyx ", "Telnyx")]
    public async Task UpdateAsync_StoresABlankProviderAsNone(string posted, string expected)
    {
        // Arrange
        var profile = new DialerProfile { ItemId = "profile-1", ProviderName = "Twilio" };
        var model = new DialerProfileViewModel { Name = "Renewals", Mode = DialerMode.Preview, ProviderName = posted };

        // Act
        await CreateDriver().UpdateAsync(profile, PostedFormUpdateModel.CreateContext(model));

        // Assert
        Assert.Equal(expected, profile.ProviderName);
    }

    private static DialerProfileDisplayDriver CreateDriver()
        => new(
            AdminFormOptionsProviderFactory.Create(),
            Mock.Of<IShellFeaturesManager>(),
            new PassThroughStringLocalizer<DialerProfileDisplayDriver>());
}
