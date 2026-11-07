using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Marketing.Outreach;
using CrestApps.OrchardCore.Marketing.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Outreach.Core;
using CrestApps.OrchardCore.Outreach.Core.Services;
using CrestApps.OrchardCore.Outreach.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Outreach;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Fluid.Values;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
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
        // Arrange
        var segment = new Segment { ItemId = "lapsed", Rules = new SegmentRules { LastOrderAtLeastDaysAgo = 90 } };
        var campaign = new Campaign { ItemId = "winback", Name = "Win back", SegmentId = "lapsed", Subject = "We miss you", Body = "<p>Come back</p>", Status = CampaignStatus.Sending };
        var sent = new List<MailMessage>();
        await using var harness = await CreateCampaignHarnessAsync(campaign, segment, sent);

        // Act
        var started = await StartSendingAsync(harness, campaign);
        await harness.ProcessDueAsync();

        // Assert
        Assert.True(started);
        Assert.False(string.IsNullOrEmpty(campaign.OutreachRunId));

        var message = Assert.Single(sent);
        Assert.Equal("sam@example.com", message.To);
        Assert.Contains("https://store.example.com/marketing/unsubscribe?token=", message.HtmlBody);
        Assert.Equal("Come back", message.TextBody.Split('\n')[0]);
        Assert.Equal((1, 2, 0, CampaignStatus.Sent), (campaign.Sent, campaign.Skipped, campaign.Failed, campaign.Status));
        Assert.NotNull(campaign.CompletedUtc);

        var token = message.HtmlBody[(message.HtmlBody.IndexOf("token=", StringComparison.Ordinal) + 6)..message.HtmlBody.IndexOf('"', message.HtmlBody.IndexOf("token=", StringComparison.Ordinal))];
        var service = harness.Services.GetRequiredService<CampaignService>();

        Assert.Equal("yes", service.ReadUnsubscribeToken(System.Net.WebUtility.HtmlDecode(token)));
        Assert.Null(service.ReadUnsubscribeToken("forged"));
    }

    [Fact]
    public async Task ACustomerWhoUnsubscribedAfterThePageWasRead_IsSkipped_ThroughTheConsentService()
    {
        // Arrange
        var segment = new Segment { ItemId = "lapsed", Rules = new SegmentRules() };
        var campaign = new Campaign { ItemId = "winback", SegmentId = "lapsed", Subject = "Hi", Body = "<p>Hi</p>", Status = CampaignStatus.Sending };
        var sent = new List<MailMessage>();
        var consent = new Mock<IConsentService>();
        consent
            .Setup(service => service.GetAsync(It.IsAny<ConsentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConsentQuery query, CancellationToken _) =>
                query is { Channel: ConsentChannels.Email, ContactKind: ConsentContactKinds.Customer, ContactId: "yes" }
                    ? ConsentStatus.OptedOutBy("customer-marketing")
                    : ConsentStatus.NotOptedOut);
        await using var harness = await CreateCampaignHarnessAsync(campaign, segment, sent, consent: consent.Object);

        // Act
        await StartSendingAsync(harness, campaign);
        await harness.ProcessDueAsync();

        // Assert
        Assert.Empty(sent);
        Assert.Equal((0, 3, 0, CampaignStatus.Sent), (campaign.Sent, campaign.Skipped, campaign.Failed, campaign.Status));
    }

    [Fact]
    public async Task ACampaignHalfSentBeforeTheEngine_ResumesAtItsCursor_WithItsCounts()
    {
        // Arrange
        var segment = new Segment { ItemId = "lapsed", Rules = new SegmentRules() };
        var campaign = new Campaign { ItemId = "winback", SegmentId = "lapsed", Subject = "Hi", Body = "<p>Hi</p>", Status = CampaignStatus.Sending, Cursor = "2", Sent = 5, Skipped = 1 };
        var sent = new List<MailMessage>();
        await using var harness = await CreateCampaignHarnessAsync(campaign, segment, sent, cursor: "2");

        // Act
        await StartSendingAsync(harness, campaign);
        await harness.ProcessDueAsync();

        // Assert
        Assert.Single(sent);
        Assert.Equal((6, 3, CampaignStatus.Sent), (campaign.Sent, campaign.Skipped, campaign.Status));
    }

    [Fact]
    public async Task ACampaignWhoseEmailIsRefused_CountsTheFailure()
    {
        // Arrange
        var segment = new Segment { ItemId = "lapsed", Rules = new SegmentRules() };
        var campaign = new Campaign { ItemId = "winback", SegmentId = "lapsed", Subject = "Hi", Body = "<p>Hi</p>", Status = CampaignStatus.Sending };
        await using var harness = await CreateCampaignHarnessAsync(campaign, segment, [], emailSucceeds: false);

        // Act
        await StartSendingAsync(harness, campaign);
        await harness.ProcessDueAsync();

        // Assert
        Assert.Equal((0, 2, 1, CampaignStatus.Sent), (campaign.Sent, campaign.Skipped, campaign.Failed, campaign.Status));
    }

    [Fact]
    public async Task ACanceledCampaign_StopsItsRun()
    {
        // Arrange
        var segment = new Segment { ItemId = "lapsed", Rules = new SegmentRules() };
        var campaign = new Campaign { ItemId = "winback", SegmentId = "lapsed", Subject = "Hi", Body = "<p>Hi</p>", Status = CampaignStatus.Sending };
        var sent = new List<MailMessage>();
        await using var harness = await CreateCampaignHarnessAsync(campaign, segment, sent);
        await StartSendingAsync(harness, campaign);

        // Act
        await using (var scope = harness.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<CampaignService>().CancelAsync(campaign, TestContext.Current.CancellationToken));
            await scope.ServiceProvider.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await harness.ProcessDueAsync();

        // Assert
        Assert.Empty(sent);
        Assert.Equal(CampaignStatus.Canceled, campaign.Status);
        Assert.Equal(OutreachRunStatus.Canceled, (await harness.GetRunAsync(campaign.OutreachRunId)).Status);
    }

    [Fact]
    public async Task CanSend_IsFalse_UntilTheSitesEmailIsSetUp()
    {
        // Arrange
        await using var harness = await CreateCampaignHarnessAsync(new Campaign { ItemId = "c" }, new Segment(), [], emailProvider: false);

        // Act
        var canSend = await harness.Services.GetRequiredService<CampaignService>().CanSendAsync();

        // Assert
        Assert.False(canSend);
    }

    [Fact]
    public void Marketing_RequiresTheEmailFeature_AndTheEmailServiceIsNotOptional()
    {
        // Arrange
        var feature = typeof(CampaignService).Assembly.GetCustomAttributes(typeof(global::OrchardCore.Modules.Manifest.FeatureAttribute), false)
            .Cast<global::OrchardCore.Modules.Manifest.FeatureAttribute>()
            .Single(attribute => attribute.GetType() == typeof(global::OrchardCore.Modules.Manifest.FeatureAttribute) && attribute.Id == CrestApps.OrchardCore.Marketing.MarketingConstants.Feature);

        // Act
        var optional = typeof(CampaignService).GetConstructors().Single().GetParameters().Where(parameter => parameter.IsOptional).Select(parameter => parameter.Name);

        // Assert
        Assert.Contains(OrchardCoreFeatureIds.Email, feature.Dependencies);
        Assert.Contains(CrestApps.OrchardCore.Outreach.OutreachConstants.Feature, feature.Dependencies);
        Assert.Empty(optional);
    }

    [Fact]
    public void ACampaignsText_IsItsHtmlWithoutTags()
        => Assert.Equal("Hi Sam,\nCome back & save", OutreachText.FromHtml("<p>Hi Sam,</p><p>Come <b>back</b> &amp; save</p>"));

    private static async Task<bool> StartSendingAsync(OutreachTestHarness harness, Campaign campaign)
    {
        await using var scope = harness.Services.CreateAsyncScope();

        var started = await scope.ServiceProvider.GetRequiredService<CampaignService>().StartSendingAsync(campaign, TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

        return started;
    }

    private static Task<OutreachTestHarness> CreateCampaignHarnessAsync(Campaign campaign, Segment segment, List<MailMessage> sent, string cursor = null, bool emailSucceeds = true, bool emailProvider = true, IConsentService consent = null)
    {
        var campaigns = new Mock<ICampaignStore>();
        campaigns.Setup(store => store.FindByIdAsync(campaign.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(campaign);

        var segments = new Mock<ISegmentStore>();
        segments.Setup(store => store.FindByIdAsync(segment.ItemId ?? string.Empty, It.IsAny<CancellationToken>())).ReturnsAsync(segment);

        var purchases = new Mock<ICustomerPurchasesStore>();
        purchases.Setup(store => store.ListAsync(segment.Rules, It.IsAny<DateTime>(), cursor, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CustomerPurchases { Id = 3, ItemId = "yes" }, new CustomerPurchases { Id = 4, ItemId = "no" }, new CustomerPurchases { Id = 5, ItemId = "gone" }]);

        var customers = new Mock<ICustomerStore>();
        customers.Setup(store => store.FindByIdAsync("yes", It.IsAny<CancellationToken>())).ReturnsAsync(new Customer { ItemId = "yes", Email = "sam@example.com", FirstName = "Sam", AcceptsMarketing = true });
        customers.Setup(store => store.FindByIdAsync("no", It.IsAny<CancellationToken>())).ReturnsAsync(new Customer { ItemId = "no", Email = "kim@example.com", AcceptsMarketing = false });
        customers.Setup(store => store.FindByIdAsync("gone", It.IsAny<CancellationToken>())).ReturnsAsync(new Customer { ItemId = "gone", Email = "lee@example.com", AcceptsMarketing = true, Status = CustomerStatus.Anonymized });

        var email = new Mock<IEmailService>();
        email.Setup(value => value.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<MailMessage, string, CancellationToken>((message, _, _) => sent.Add(message))
            .ReturnsAsync(emailSucceeds ? Result.Success() : Result.Failed());

        var providers = new Mock<IEmailProviderResolver>();
        providers.Setup(value => value.GetAsync(It.IsAny<string>())).ReturnsAsync(emailProvider ? Mock.Of<IEmailProvider>() : null);

        // The Liquid passes the template through, so the test reads what the campaign adds to it.
        var liquid = new Mock<ILiquidTemplateManager>();
        liquid.Setup(value => value.RenderStringAsync(It.IsAny<string>(), It.IsAny<TextEncoder>(), It.IsAny<object>(), It.IsAny<IEnumerable<KeyValuePair<string, FluidValue>>>()))
            .Returns<string, TextEncoder, object, IEnumerable<KeyValuePair<string, FluidValue>>>((template, _, _, _) => Task.FromResult(template));

        return OutreachTestHarness.CreateAsync(services =>
        {
            services.AddSingleton(campaigns.Object);
            services.AddSingleton(segments.Object);
            services.AddSingleton(purchases.Object);
            services.AddSingleton(customers.Object);
            services.AddSingleton(email.Object);
            services.AddSingleton(providers.Object);
            services.AddSingleton(liquid.Object);
            services.AddSingleton(SiteService());
            services.AddSingleton(SignedLinkDoubles.Tokens());
            services.AddSingleton<LinkGenerator>(new UnsubscribeLinkGenerator());
            services.AddSingleton<IStringLocalizer<CampaignService>>(new PassThroughStringLocalizer<CampaignService>());
            services.AddScoped<CampaignService>();
            services.AddOutreachChannelSender<EmailOutreachChannelSender>();
            services.AddOutreachAudienceSource<MarketingSegmentAudienceSource>();
            services.AddOutreachTemplateRenderer<MarketingCampaignTemplateRenderer>();
            services.AddOutreachRunObserver<MarketingCampaignRunObserver>();

            if (consent is not null)
            {
                services.AddSingleton(consent);
            }
        });
    }

    /// <summary>
    /// Returns the unsubscribe route's path with the token in the query.
    /// </summary>
    private sealed class UnsubscribeLinkGenerator : LinkGenerator
    {
        public override string GetPathByAddress<TAddress>(HttpContext httpContext, TAddress address, RouteValueDictionary values, RouteValueDictionary ambientValues = null, PathString? pathBase = null, FragmentString fragment = default, LinkOptions options = null)
            => Path(values);

        public override string GetPathByAddress<TAddress>(TAddress address, RouteValueDictionary values, PathString pathBase = default, FragmentString fragment = default, LinkOptions options = null)
            => Path(values);

        public override string GetUriByAddress<TAddress>(HttpContext httpContext, TAddress address, RouteValueDictionary values, RouteValueDictionary ambientValues = null, string scheme = null, HostString? host = null, PathString? pathBase = null, FragmentString fragment = default, LinkOptions options = null)
            => null;

        public override string GetUriByAddress<TAddress>(TAddress address, RouteValueDictionary values, string scheme, HostString host, PathString pathBase = default, FragmentString fragment = default, LinkOptions options = null)
            => null;

        private static string Path(RouteValueDictionary values)
            => "/marketing/unsubscribe?token=" + Uri.EscapeDataString(values["token"]?.ToString() ?? string.Empty);
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
        site.SetupGet(value => value.BaseUrl).Returns("https://store.example.com");
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return siteService.Object;
    }
}
