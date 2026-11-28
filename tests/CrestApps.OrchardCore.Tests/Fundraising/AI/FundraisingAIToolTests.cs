using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Fundraising;
using CrestApps.OrchardCore.Fundraising.AI;
using CrestApps.OrchardCore.Fundraising.Models;
using CrestApps.OrchardCore.Fundraising.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Fundraising.AI;

/// <summary>
/// <c>getFundraisingCampaignProgress</c> needs Manage fundraising, reports the progress the donation service computes,
/// and lists the campaign's donations largest first, filtered by a least amount, only when asked.
/// </summary>
public sealed class FundraisingAIToolTests
{
    private readonly ContentItem _campaign;
    private readonly Mock<IContentManager> _contentManager = new();
    private readonly Mock<IDonationStore> _donations = new();

    public FundraisingAIToolTests()
    {
        _campaign = new ContentItem { ContentItemId = "library", ContentType = "Campaign", DisplayText = "Library campaign" };
        _campaign.Weld(new FundraisingCampaignPart { Goal = 10000m });
        _campaign.Weld(new ProductPart { Currency = "USD" });

        _contentManager.Setup(manager => manager.GetAsync("library", It.IsAny<VersionOptions>())).ReturnsAsync(_campaign);
        _donations.Setup(store => store.ListByCampaignAsync("library", It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new Donation { ItemId = "d1", CampaignId = "library", DonorName = "Ana", DonorEmail = "ana@example.com", Amount = 600m, CampaignAmount = 600m, Currency = "USD", ReceiptNumber = "R-1" },
            new Donation { ItemId = "d2", CampaignId = "library", DonorName = "Ben", DonorEmail = "ben@example.com", Amount = 100m, CampaignAmount = 100m, Currency = "USD", ReceiptNumber = "R-2" },
            new Donation { ItemId = "d3", CampaignId = "library", DonorName = "Cal", DonorEmail = "cal@example.com", Amount = 900m, CampaignAmount = 900m, Currency = "USD", ReceiptNumber = "R-3", Refunded = 900m },
        ]);
    }

    [Fact]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetFundraisingCampaignProgressTool(), Build(signedIn: false), new() { ["campaignId"] = "library" });

        // Assert
        Assert.Equal(FundraisingAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_donations.Invocations);
    }

    [Fact]
    public async Task Tool_RefusesACallerWithoutManageFundraising()
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != FundraisingPermissions.ManageFundraising.Name);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetFundraisingCampaignProgressTool(), services, new() { ["campaignId"] = "library" });

        // Assert
        Assert.Equal(FundraisingAIToolBase.NotAllowedMessage, result);
        Assert.Equal(FundraisingPermissions.ManageFundraising.Name, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_donations.Invocations);
    }

    [Fact]
    public async Task Tool_ReturnsTheProgress_AndTheDonationsOverAnAmount_LargestFirst()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new GetFundraisingCampaignProgressTool(), Build(), new()
        {
            ["campaignId"] = "library",
            ["includeDonations"] = true,
            ["minimumAmount"] = 500m,
        }));

        // Assert
        Assert.Equal(700m, json.GetProperty("raised").GetDecimal());
        Assert.Equal(10000m, json.GetProperty("goal").GetDecimal());
        Assert.Equal(2, json.GetProperty("donors").GetInt32());
        Assert.Equal("USD", json.GetProperty("currency").GetString());
        var donations = json.GetProperty("donations").GetProperty("items").EnumerateArray().Select(donation => donation.GetProperty("id").GetString()).ToArray();
        Assert.Equal(new[] { "d3", "d1" }, donations);
    }

    [Fact]
    public async Task Tool_SaysWhenTheItemIsNotACampaign()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetFundraisingCampaignProgressTool(), Build(), new() { ["campaignId"] = "missing" });

        // Assert
        Assert.Contains("No fundraising campaign has the id 'missing'", result, StringComparison.Ordinal);
    }

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        services.AddSingleton(_contentManager.Object);
        services.AddSingleton(_donations.Object);
        services.AddSingleton(provider => new DonationService(
            _donations.Object,
            _contentManager.Object,
            Mock.Of<ISiteService>(),
            Mock.Of<ICommerceEmailSender>(),
            HtmlEncoder.Default,
            clock.Object,
            provider.GetRequiredService<ILocalClock>(),
            new PassThroughStringLocalizer<DonationService>(),
            Mock.Of<IShopperCurrency>()));

        return services.BuildServiceProvider();
    }
}
