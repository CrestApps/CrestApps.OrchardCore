using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Marketing.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Fluid.Values;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Email;
using OrchardCore.Infrastructure;
using OrchardCore.Liquid;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// Customers' purchases count each paid order and refund once, and a campaign goes only to a segment's customers who
/// accept marketing, each with their own unsubscribe link.
/// </summary>
public sealed class MarketingTests
{
    private static readonly DateTime _now = new(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AnOrder_CountsOnce_AndARefundComesOffOnce()
    {
        var service = new CustomerPurchasesService(Mock.Of<ICustomerPurchasesStore>(), SiteService(), Clock(), Mock.Of<IShopperCurrency>(), Mock.Of<IOrderStore>(), Mock.Of<global::YesSql.ISession>());
        var purchases = new CustomerPurchases { ItemId = "customer-1" };
        var order = new Order
        {
            ItemId = "order-1",
            CustomerId = "customer-1",
            Status = OrderStatus.Paid,
            Currency = "USD",
            Subtotal = 100m,
            Total = 108m,
            PaidUtc = _now.AddDays(-100),
            BillingAddress = new CrestApps.OrchardCore.Addresses.Models.Address { Country = "de" },
        };

        Assert.True(await service.ApplyAsync(purchases, order, TestContext.Current.CancellationToken));
        Assert.False(await service.ApplyAsync(purchases, order, TestContext.Current.CancellationToken));

        order.Refunds.Add(new OrderRefund { RefundId = "refund-1", Amount = 8m, Currency = "USD" });

        Assert.True(await service.ApplyAsync(purchases, order, TestContext.Current.CancellationToken));
        Assert.False(await service.ApplyAsync(purchases, order, TestContext.Current.CancellationToken));

        Assert.Equal(1, purchases.OrderCount);
        Assert.Equal(100m, purchases.TotalSpent);
        Assert.Equal(_now.AddDays(-100), purchases.LastOrderUtc);
        Assert.Equal("DE", purchases.Country);
    }

    [Fact]
    public async Task ACampaign_GoesOnlyToCustomersWhoAcceptMarketing_WithTheirUnsubscribeLink()
    {
        var segment = new Segment { ItemId = "lapsed", Rules = new SegmentRules { LastOrderAtLeastDaysAgo = 90 } };
        var campaign = new Campaign { ItemId = "winback", SegmentId = "lapsed", Subject = "We miss you", Body = "<p>Come back</p>", Status = CampaignStatus.Sending };

        var segments = new Mock<ISegmentStore>();
        segments.Setup(store => store.FindByIdAsync("lapsed", It.IsAny<CancellationToken>())).ReturnsAsync(segment);

        var purchases = new Mock<ICustomerPurchasesStore>();
        purchases.Setup(store => store.ListAsync(segment.Rules, It.IsAny<DateTime>(), null, CampaignService.BatchSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CustomerPurchases { Id = 1, ItemId = "yes" }, new CustomerPurchases { Id = 2, ItemId = "no" }, new CustomerPurchases { Id = 3, ItemId = "gone" }]);

        var customers = new Mock<ICustomerStore>();
        customers.Setup(store => store.FindByIdAsync("yes", It.IsAny<CancellationToken>())).ReturnsAsync(new Customer { ItemId = "yes", Email = "sam@example.com", FirstName = "Sam", AcceptsMarketing = true });
        customers.Setup(store => store.FindByIdAsync("no", It.IsAny<CancellationToken>())).ReturnsAsync(new Customer { ItemId = "no", Email = "kim@example.com", AcceptsMarketing = false });
        customers.Setup(store => store.FindByIdAsync("gone", It.IsAny<CancellationToken>())).ReturnsAsync(new Customer { ItemId = "gone", Email = "lee@example.com", AcceptsMarketing = true, Status = CustomerStatus.Anonymized });

        var sent = new List<MailMessage>();
        var email = new Mock<IEmailService>();
        email.Setup(value => value.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<MailMessage, string, CancellationToken>((message, _, _) => sent.Add(message))
            .ReturnsAsync(Result.Success());

        var service = CreateCampaigns(segments.Object, purchases.Object, customers.Object, email.Object);

        Assert.False(await service.SendBatchAsync(campaign, token => "https://store.example.com/marketing/unsubscribe?token=" + token, TestContext.Current.CancellationToken));

        var message = Assert.Single(sent);
        Assert.Equal("sam@example.com", message.To);
        Assert.Contains("unsubscribe?token=", message.HtmlBody);
        Assert.Equal((1, 2, CampaignStatus.Sent, "3"), (campaign.Sent, campaign.Skipped, campaign.Status, campaign.Cursor));

        var token = message.HtmlBody[(message.HtmlBody.IndexOf("token=", StringComparison.Ordinal) + 6)..message.HtmlBody.IndexOf('"', message.HtmlBody.IndexOf("token=", StringComparison.Ordinal))];

        Assert.Equal("yes", service.ReadUnsubscribeToken(System.Net.WebUtility.HtmlDecode(token)));
        Assert.Null(service.ReadUnsubscribeToken("forged"));
    }

    [Fact]
    public void ACampaignsText_IsItsHtmlWithoutTags()
        => Assert.Equal("Hi Sam,\nCome back & save", CampaignService.ToText("<p>Hi Sam,</p><p>Come <b>back</b> &amp; save</p>"));

    private static CampaignService CreateCampaigns(ISegmentStore segments, ICustomerPurchasesStore purchases, ICustomerStore customers, IEmailService email)
    {
        // The Liquid passes the template through, so the test reads what the campaign adds to it.
        var liquid = new Mock<ILiquidTemplateManager>();
        liquid.Setup(value => value.RenderStringAsync(It.IsAny<string>(), It.IsAny<TextEncoder>(), It.IsAny<object>(), It.IsAny<IEnumerable<KeyValuePair<string, FluidValue>>>()))
            .Returns<string, TextEncoder, object, IEnumerable<KeyValuePair<string, FluidValue>>>((template, _, _, _) => Task.FromResult(template));

        return new CampaignService(
            Mock.Of<ICampaignStore>(),
            segments,
            purchases,
            customers,
            liquid.Object,
            SiteService(),
            SignedLinkDoubles.Tokens(),
            Clock(),
            NullLogger<CampaignService>.Instance,
            new PassThroughStringLocalizer<CampaignService>(),
            email);
    }

    private static IClock Clock()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }

    private static ISiteService SiteService()
    {
        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings { Currency = "USD" });
        site.SetupGet(value => value.SiteName).Returns("Contoso");
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return siteService.Object;
    }
}
