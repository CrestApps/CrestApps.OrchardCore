using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Marketing;
using CrestApps.OrchardCore.Marketing.AI;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Marketing.Services;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Fluid.Values;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Email;
using OrchardCore.Infrastructure;
using OrchardCore.Liquid;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.Marketing.AI;

/// <summary>
/// The marketing tools need Manage marketing, size a segment with the counts its page shows, send a test only to an address
/// that did not opt out, queue or schedule a campaign through the page's send (never reporting it as sent), refuse to send
/// without consent checks, and cancel only a scheduled or sending campaign. Segments and campaigns are read-only record kinds.
/// </summary>
public sealed class MarketingAIToolTests
{
    private static readonly DateTime _now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    private readonly Segment _segment = new()
    {
        ItemId = "segment-1",
        Name = "Lapsed buyers",
        Rules = new SegmentRules { MinSpent = 200m, LastOrderAtLeastDaysAgo = 90 },
    };

    private readonly Campaign _campaign = new()
    {
        ItemId = "campaign-1",
        Name = "Win back",
        SegmentId = "segment-1",
        Subject = "We miss you",
        Body = "<p>Hi {{ Customer.FirstName }}</p>",
        Status = CampaignStatus.Draft,
        OutreachRunId = "run-secret",
    };

    private readonly Mock<ICampaignStore> _campaigns = new();
    private readonly Mock<ISegmentStore> _segments = new();
    private readonly Mock<ICustomerPurchasesStore> _purchases = new();
    private readonly Mock<ICustomerStore> _customers = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IEmailProviderResolver> _emailProviders = new();
    private readonly Mock<IOutreachEngine> _outreach = new();
    private readonly Mock<IConsentService> _consent = new();
    private readonly Mock<ISite> _site = new();

    public MarketingAIToolTests()
    {
        _campaigns.Setup(store => store.FindByIdAsync("campaign-1", It.IsAny<CancellationToken>())).ReturnsAsync(_campaign);
        _segments.Setup(store => store.FindByIdAsync("segment-1", It.IsAny<CancellationToken>())).ReturnsAsync(_segment);
        _purchases.Setup(store => store.CountAsync(It.IsAny<SegmentRules>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(2);
        _purchases.Setup(store => store.ListAsync(It.IsAny<SegmentRules>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            // The count reads pages of 500; a test email's sample (one customer) finds nobody, so no unsubscribe token is made.
            .ReturnsAsync((SegmentRules _, DateTime _, string afterId, int take, CancellationToken _) => afterId is null && take > 1
                ? [new CustomerPurchases { Id = 1, ItemId = "customer-1" }, new CustomerPurchases { Id = 2, ItemId = "customer-2" }]
                : []);
        _customers.Setup(store => store.FindByIdAsync("customer-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer { ItemId = "customer-1", Email = "ann@example.com", AcceptsMarketing = true, Status = CustomerStatus.Active });
        _customers.Setup(store => store.FindByIdAsync("customer-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer { ItemId = "customer-2", Email = "bob@example.com", AcceptsMarketing = false, Status = CustomerStatus.Active });
        _emailProviders.Setup(resolver => resolver.GetAsync(It.IsAny<string>())).ReturnsAsync(Mock.Of<IEmailProvider>());
        _email.Setup(email => email.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        _consent.Setup(consent => consent.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ConsentStatus.NotOptedOut);
        _site.Setup(site => site.BaseUrl).Returns("https://shop.example.com");
        _site.Setup(site => site.SiteName).Returns("Shop");
        _site.Setup(site => site.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings { Currency = "USD" });
    }

    public static TheoryData<string> ToolNames => new()
    {
        GetMarketingSegmentTool.TheName,
        SendMarketingCampaignTestTool.TheName,
        SendMarketingCampaignTool.TheName,
        CancelMarketingCampaignTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), Arguments(toolName));

        // Assert
        Assert.Equal(MarketingAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_campaigns.Invocations);
        Assert.Empty(_segments.Invocations);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutManageMarketing(string toolName)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != MarketingPermissions.ManageMarketing.Name);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        Assert.Equal(MarketingAIToolBase.NotAllowedMessage, result);
        Assert.Equal(MarketingPermissions.ManageMarketing.Name, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_campaigns.Invocations);
        Assert.Empty(_email.Invocations);
    }

    [Fact]
    public async Task GetMarketingSegment_ReturnsTheRulesAndTheTwoCounts()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(GetMarketingSegmentTool.TheName, Build(), new() { ["segmentId"] = "segment-1" }));

        // Assert
        Assert.Equal("Lapsed buyers", json.GetProperty("name").GetString());
        Assert.Equal(200m, json.GetProperty("rules").GetProperty("minSpent").GetDecimal());
        Assert.Equal("USD", json.GetProperty("rules").GetProperty("spentCurrency").GetString());
        Assert.Equal(2, json.GetProperty("customersInSegment").GetInt32());
        Assert.Equal(1, json.GetProperty("customersWhoCanBeEmailed").GetInt32());
    }

    [Fact]
    public async Task GetMarketingSegment_SaysWhenTheSegmentIsNotFound()
    {
        // Act
        var result = await InvokeAsync(GetMarketingSegmentTool.TheName, Build(), new() { ["segmentId"] = "missing" });

        // Assert
        Assert.StartsWith("Nothing was read: no segment has the id 'missing'", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendMarketingCampaign_QueuesADraft_AndNeverSaysItWasSent()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SendMarketingCampaignTool.TheName, Build(), new() { ["campaignId"] = "campaign-1" }));

        // Assert
        Assert.Equal(CampaignStatus.Sending, _campaign.Status);
        Assert.Equal(_now, _campaign.StartedUtc);
        Assert.Equal("sending", json.GetProperty("status").GetString());
        Assert.Contains("queued", json.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains("opted-out addresses are skipped", json.GetProperty("message").GetString(), StringComparison.Ordinal);
        _outreach.Verify(engine => engine.StartAsync(It.IsAny<CrestApps.OrchardCore.Outreach.Models.OutreachRun>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMarketingCampaign_SchedulesACampaignWhoseTimeIsToCome()
    {
        // Arrange
        _campaign.ScheduledUtc = _now.AddDays(1);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SendMarketingCampaignTool.TheName, Build(), new() { ["campaignId"] = "campaign-1" }));

        // Assert
        Assert.Equal(CampaignStatus.Scheduled, _campaign.Status);
        Assert.Equal("scheduled", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task SendMarketingCampaign_RefusesWithoutTheBaseUrl_AndLeavesItADraft()
    {
        // Arrange
        _site.Setup(site => site.BaseUrl).Returns((string)null);

        // Act
        var result = await InvokeAsync(SendMarketingCampaignTool.TheName, Build(), new() { ["campaignId"] = "campaign-1" });

        // Assert
        Assert.StartsWith("Nothing was sent", result, StringComparison.Ordinal);
        Assert.Equal(CampaignStatus.Draft, _campaign.Status);
    }

    [Fact]
    public async Task SendMarketingCampaign_RefusesWhenConsentCannotBeChecked()
    {
        // Act
        var result = await InvokeAsync(SendMarketingCampaignTool.TheName, Build(withConsent: false), new() { ["campaignId"] = "campaign-1" });

        // Assert
        Assert.Contains("opted-out addresses would not be skipped", result, StringComparison.Ordinal);
        Assert.Equal(CampaignStatus.Draft, _campaign.Status);
    }

    [Fact]
    public async Task SendMarketingCampaign_RefusesACampaignThatWasSent()
    {
        // Arrange
        _campaign.Status = CampaignStatus.Sent;

        // Act
        var result = await InvokeAsync(SendMarketingCampaignTool.TheName, Build(), new() { ["campaignId"] = "campaign-1" });

        // Assert
        Assert.Contains("Only a draft or scheduled campaign can be sent", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendMarketingCampaignTest_EmailsOneTestWithTheSubjectMarked()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SendMarketingCampaignTestTool.TheName, Build(), new() { ["campaignId"] = "campaign-1", ["to"] = "me@example.com" }));

        // Assert
        Assert.Equal("me@example.com", json.GetProperty("to").GetString());
        _email.Verify(email => email.SendAsync(It.Is<MailMessage>(message => message.To == "me@example.com" && message.Subject.StartsWith("[Test]", StringComparison.Ordinal)), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMarketingCampaignTest_RefusesAnAddressThatOptedOut()
    {
        // Arrange
        _consent.Setup(consent => consent.GetAsync(ConsentChannels.Email, "me@example.com", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConsentStatus.OptedOutBy("MarketingUnsubscribe"));

        // Act
        var result = await InvokeAsync(SendMarketingCampaignTestTool.TheName, Build(), new() { ["campaignId"] = "campaign-1", ["to"] = "me@example.com" });

        // Assert
        Assert.Contains("has opted out of email", result, StringComparison.Ordinal);
        _email.Verify(email => email.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelMarketingCampaign_CancelsASendingCampaign()
    {
        // Arrange
        _campaign.Status = CampaignStatus.Sending;
        _campaign.Sent = 12;

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(CancelMarketingCampaignTool.TheName, Build(), new() { ["campaignId"] = "campaign-1" }));

        // Assert
        Assert.Equal(CampaignStatus.Canceled, _campaign.Status);
        Assert.Equal(12, json.GetProperty("sent").GetInt32());
        _outreach.Verify(engine => engine.CancelAsync("run-secret", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelMarketingCampaign_RefusesADraft()
    {
        // Act
        var result = await InvokeAsync(CancelMarketingCampaignTool.TheName, Build(), new() { ["campaignId"] = "campaign-1" });

        // Assert
        Assert.Contains("Only a scheduled or sending campaign can be canceled", result, StringComparison.Ordinal);
        Assert.Equal(CampaignStatus.Draft, _campaign.Status);
    }

    [Fact]
    public async Task RecordKinds_AreReadOnly_AndTheCampaignProjectionLeavesOutTheRun()
    {
        // Arrange
        var services = new ServiceCollection();
        new AIToolsStartup(new PassThroughStringLocalizer<AIToolsStartup>()).ConfigureServices(services);
        services.AddSingleton(_campaigns.Object);
        services.AddSingleton(_segments.Object);
        _campaigns.Setup(store => store.PageAsync("win", 20, 20, It.IsAny<CancellationToken>())).ReturnsAsync((1, (IReadOnlyList<Campaign>)[_campaign]));

        using var provider = services.BuildServiceProvider();
        var kinds = provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds;
        var campaignKind = Assert.IsType<AIRecordKind<Campaign>>(kinds["marketingCampaign"]);

        // Act
        var projected = JsonSerializer.Serialize(campaignKind.Projection(_campaign));
        var page = await campaignKind.ListAsync(provider, new AIRecordQuery { Text = "win", Page = 2, PageSize = 20 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(kinds["marketingSegment"].CanWrite);
        Assert.False(campaignKind.CanWrite);
        Assert.DoesNotContain("run-secret", projected, StringComparison.Ordinal);
        Assert.Equal(1, page.Count);
        Assert.Same(_campaign, Assert.Single(page.Entries));
    }

    private static Dictionary<string, object> Arguments(string toolName)
        => toolName switch
        {
            GetMarketingSegmentTool.TheName => new() { ["segmentId"] = "segment-1" },
            SendMarketingCampaignTestTool.TheName => new() { ["campaignId"] = "campaign-1", ["to"] = "me@example.com" },
            _ => new() { ["campaignId"] = "campaign-1" },
        };

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null, bool withConsent = true)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);
        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(_site.Object);

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(_now);

        var liquid = new Mock<ILiquidTemplateManager>();
        liquid.Setup(manager => manager.RenderStringAsync(It.IsAny<string>(), It.IsAny<TextEncoder>(), It.IsAny<object>(), It.IsAny<IEnumerable<KeyValuePair<string, FluidValue>>>()))
            .ReturnsAsync((string template, TextEncoder _, object _, IEnumerable<KeyValuePair<string, FluidValue>> _) => template);

        services.AddSingleton(_campaigns.Object);
        services.AddSingleton(_segments.Object);
        services.AddSingleton(siteService.Object);
        services.AddSingleton(Mock.Of<YesSqlSession>());
        services.AddSingleton(Create<CampaignService>(
            _campaigns.Object,
            _segments.Object,
            _purchases.Object,
            _customers.Object,
            liquid.Object,
            siteService.Object,
            clock.Object,
            new PassThroughStringLocalizer<CampaignService>(),
            _email.Object,
            _emailProviders.Object,
            _outreach.Object));

        if (withConsent)
        {
            services.AddSingleton(_consent.Object);
        }

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            GetMarketingSegmentTool.TheName => new GetMarketingSegmentTool(),
            SendMarketingCampaignTestTool.TheName => new SendMarketingCampaignTestTool(),
            SendMarketingCampaignTool.TheName => new SendMarketingCampaignTool(),
            _ => new CancelMarketingCampaignTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }

    // Built from the services it's given, so a dependency these paths don't use is passed as null.
    private static T Create<T>(params object[] services)
    {
        var constructor = typeof(T).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
        var arguments = constructor.GetParameters()
            .Select(parameter => services.FirstOrDefault(service => service is not null && parameter.ParameterType.IsInstanceOfType(service)))
            .ToArray();

        return (T)constructor.Invoke(arguments);
    }
}
