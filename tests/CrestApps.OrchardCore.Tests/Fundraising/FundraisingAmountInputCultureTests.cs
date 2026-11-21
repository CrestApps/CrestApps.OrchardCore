using CrestApps.OrchardCore.Fundraising.Drivers;
using CrestApps.OrchardCore.Fundraising.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Tests.Core.ModelBinding;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Moq;
using OrchardCore.Modules;
using FundraisingStartup = CrestApps.OrchardCore.Fundraising.Startup;

namespace CrestApps.OrchardCore.Tests.Fundraising;

/// <summary>
/// Found with the fifth fix verification (outside the PR, on main from #1332): a campaign's goal and least donation were
/// read with <c>NumberStyles.Number</c> and the invariant culture, where the comma is a group separator, so a German
/// admin's "2,50" least donation was 250. The campaign editor is posted here through MVC's own binder, under the request
/// culture, into the real part driver.
/// </summary>
public sealed class FundraisingAmountInputCultureTests
{
    public static TheoryData<string, string, string> Posts => new()
    {
        { "en-US", "10000.50", "2.50" },
        { "de-DE", "10000.50", "2.50" },
        { "de-DE", "10000,50", "2,50" },
        { "fr-FR", "10000,50", "2,50" },
        { "nl-NL", "10000.50", "2.50" },
        { "nl-NL", "10000,50", "2,50" },
    };

    public static TheoryData<string, string> Unreadable => new()
    {
        { "en-US", "1,250.00" },
        { "de-DE", "1.250,00" },
        { "fr-FR", "douze" },
    };

    [Theory]
    [MemberData(nameof(Posts))]
    public async Task Campaign_SavesTheAmountsTheFormSent(string culture, string goal, string minimum)
    {
        var part = new FundraisingCampaignPart();
        var updater = Post(culture, goal, minimum);

        await CreateDriver().UpdateAsync(part, updater.CreatePartContext(nameof(FundraisingCampaignPart)));

        Assert.True(updater.ModelState.IsValid);
        Assert.Equal(10000.50m, part.Goal);
        Assert.Equal(2.50m, part.MinimumAmount);
    }

    [Theory]
    [MemberData(nameof(Unreadable))]
    public async Task Campaign_WithAGoalThatIsNotANumber_IsRefused(string culture, string posted)
    {
        var part = new FundraisingCampaignPart { Goal = 5000m };
        var updater = Post(culture, posted, "5");

        await CreateDriver().UpdateAsync(part, updater.CreatePartContext(nameof(FundraisingCampaignPart)));

        Assert.False(updater.ModelState.IsValid);
        Assert.NotEqual(1250m, part.Goal);
    }

    [Theory]
    [MemberData(nameof(Unreadable))]
    public async Task Campaign_WithALeastDonationThatIsNotANumber_IsRefused(string culture, string posted)
    {
        var part = new FundraisingCampaignPart { MinimumAmount = 5m };
        var updater = Post(culture, "10000", posted);

        await CreateDriver().UpdateAsync(part, updater.CreatePartContext(nameof(FundraisingCampaignPart)));

        Assert.False(updater.ModelState.IsValid);
        Assert.NotEqual(1250m, part.MinimumAmount);
    }

    // An empty goal still means none, and an empty least donation the default of 1.
    [Fact]
    public async Task Campaign_WithEmptyAmounts_HasNoGoalAndTheDefaultLeastDonation()
    {
        var part = new FundraisingCampaignPart { Goal = 5000m, MinimumAmount = 5m };
        var updater = Post("de-DE", string.Empty, string.Empty);

        await CreateDriver().UpdateAsync(part, updater.CreatePartContext(nameof(FundraisingCampaignPart)));

        Assert.True(updater.ModelState.IsValid);
        Assert.Equal(0m, part.Goal);
        Assert.Equal(1m, part.MinimumAmount);
    }

    private static FormPostUpdateModel Post(string culture, string goal, string minimum)
        => new(new FundraisingStartup(), culture, new Dictionary<string, StringValues>
        {
            ["Goal"] = goal,
            ["MinimumAmount"] = minimum,
            ["SuggestedAmounts"] = "25, 50, 100",
        });

    private static FundraisingCampaignPartDisplayDriver CreateDriver()
        => new(
            donations: null,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new PassThroughStringLocalizer<FundraisingCampaignPartDisplayDriver>(),
            Mock.Of<IClock>(),
            Mock.Of<ILocalClock>(),
            Mock.Of<IShopperCurrency>());
}
