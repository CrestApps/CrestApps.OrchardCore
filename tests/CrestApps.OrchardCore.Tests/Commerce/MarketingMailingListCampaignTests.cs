using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Consent.Core.Services;
using CrestApps.OrchardCore.Consent.Unsubscribe;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.MailingLists;
using CrestApps.OrchardCore.MailingLists.Core.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Marketing.Outreach;
using CrestApps.OrchardCore.Marketing.Recipes;
using CrestApps.OrchardCore.Marketing.Services;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.MailingLists;
using CrestApps.OrchardCore.Tests.Outreach;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Fluid.Values;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Email;
using OrchardCore.Infrastructure;
using OrchardCore.Liquid;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// A campaign to a mailing list goes, through the mailing lists Outreach audience, to the list's subscribed members only,
/// checked by address for the marketing purpose, rendered without a customer, and each e-mail's unsubscribe link
/// unsubscribes that list.
/// </summary>
public sealed class MarketingMailingListCampaignTests
{
    private const string ListId = "list-news";

    [Fact]
    public void TheRunOfAListCampaign_ReadsTheListsMembers_ByAddress_ForMarketing()
    {
        // Arrange
        var campaign = new Campaign
        {
            ItemId = "news",
            Name = "November news",
            AudienceKind = CampaignAudienceKinds.MailingList,
            MailingListId = ListId,
            Subject = "News",
            Body = "<p>News</p>",
            Cursor = "7",
            Sent = 3,
        };

        // Act
        var run = CampaignService.BuildRun(campaign);

        // Assert
        Assert.Equal(MailingListsConstants.OutreachAudienceSource, run.AudienceSource);
        Assert.Equal(ListId, MailingListAudienceSource.ListIdOf(run));
        Assert.Equal(MarketingOutreachConstants.CampaignOwnerType, run.OwnerType);
        Assert.Equal("news", run.OwnerId);
        Assert.Equal(OutreachConstants.EmailSender, run.Sender);
        Assert.Equal(ConsentChannels.Email, run.ConsentChannel);
        Assert.Null(run.ConsentContactKind);
        Assert.Equal(ConsentPurposes.Marketing, run.ConsentPurpose);
        Assert.Equal(MarketingOutreachConstants.CampaignRenderer, run.Renderer);
        Assert.Equal(("7", 3), (run.Cursor, run.Sent));
    }

    [Fact]
    public void TheRunOfASegmentCampaign_StaysAsItWas_WhenItNamesNoAudience()
    {
        // Arrange
        var campaign = new Campaign { ItemId = "winback", SegmentId = "lapsed", MailingListId = ListId, Subject = "Hi", Body = "<p>Hi</p>" };

        // Act
        var run = CampaignService.BuildRun(campaign);

        // Assert
        Assert.Equal(MarketingOutreachConstants.SegmentAudienceSource, run.AudienceSource);
        Assert.Equal(ConsentContactKinds.Customer, run.ConsentContactKind);
        Assert.False(run.Properties.ContainsKey(MailingListsConstants.OutreachListIdProperty));
        Assert.Equal(UnsubscribeScopeKinds.AllMarketing, CampaignService.GetUnsubscribeScope(campaign).Kind);
    }

    [Fact]
    public async Task AListCampaign_GoesToTheListsSubscribedMembers_WithLinksThatUnsubscribeTheList()
    {
        // Arrange
        var campaign = new Campaign
        {
            ItemId = "news",
            Name = "November news",
            AudienceKind = CampaignAudienceKinds.MailingList,
            MailingListId = ListId,
            Subject = "News",
            Body = "<p>News for you</p>",
            Status = CampaignStatus.Sending,
        };

        var members = new InMemoryMailingListMemberStore();
        await members.CreateAsync(Member("m1", ListId, "Ada@Example.com", "Ada", MailingListMemberStatus.Subscribed), TestContext.Current.CancellationToken);
        await members.CreateAsync(Member("m2", ListId, "grace@example.com", null, MailingListMemberStatus.Pending), TestContext.Current.CancellationToken);
        await members.CreateAsync(Member("m3", ListId, "alan@example.com", null, MailingListMemberStatus.Unsubscribed), TestContext.Current.CancellationToken);
        await members.CreateAsync(Member("m4", "list-other", "kim@example.com", null, MailingListMemberStatus.Subscribed), TestContext.Current.CancellationToken);

        var customers = new Mock<ICustomerStore>();
        var sent = new List<MailMessage>();
        await using var harness = await CreateHarnessAsync(campaign, members, customers, sent);

        // Act
        bool started;

        await using (var scope = harness.Services.CreateAsyncScope())
        {
            started = await scope.ServiceProvider.GetRequiredService<CampaignService>().StartSendingAsync(campaign, TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await harness.ProcessDueAsync();

        // Assert
        Assert.True(started);

        // The run kept the list through storage, so the audience reads it back.
        var run = await harness.GetRunAsync(campaign.OutreachRunId);
        Assert.Equal(MailingListsConstants.OutreachAudienceSource, run.AudienceSource);
        Assert.Equal(ListId, MailingListAudienceSource.ListIdOf(run));
        Assert.Null(run.ConsentContactKind);

        var message = Assert.Single(sent);
        Assert.Equal("Ada@Example.com", message.To);
        Assert.Equal((1, CampaignStatus.Sent), (campaign.Sent, campaign.Status));

        // A member of a list is not a customer: the customer store is never asked.
        customers.Verify(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // The link opens the e-mail preferences page for the member's address and this list.
        const string LinkStart = "email-preferences?token=";
        var start = message.HtmlBody.IndexOf(LinkStart, StringComparison.Ordinal) + LinkStart.Length;
        var token = message.HtmlBody.Substring(start, message.HtmlBody.IndexOf('"', start) - start);

        Assert.True(harness.Services.GetRequiredService<IUnsubscribeLinks>().TryRead(Uri.UnescapeDataString(System.Net.WebUtility.HtmlDecode(token)), out var ticket));
        Assert.Equal("ada@example.com", ticket.Address);
        Assert.Equal(MailingListsConstants.UnsubscribeScopeKind, ticket.Scope.Kind);
        Assert.Equal(ListId, ticket.Scope.Id);
    }

    [Fact]
    public async Task AListMemberWhoStoppedMarketing_IsSkipped_ThroughTheConsentServiceByAddress()
    {
        // Arrange
        var campaign = new Campaign
        {
            ItemId = "news",
            AudienceKind = CampaignAudienceKinds.MailingList,
            MailingListId = ListId,
            Subject = "News",
            Body = "<p>News</p>",
            Status = CampaignStatus.Sending,
        };

        var members = new InMemoryMailingListMemberStore();
        await members.CreateAsync(Member("m1", ListId, "ada@example.com", "Ada", MailingListMemberStatus.Subscribed), TestContext.Current.CancellationToken);

        var consent = new Mock<IConsentService>();
        consent
            .Setup(service => service.GetAsync(It.IsAny<ConsentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConsentQuery query, CancellationToken _) =>
                query is { Channel: ConsentChannels.Email, Address: "ada@example.com", ContactKind: null, Purpose: ConsentPurposes.Marketing }
                    ? ConsentStatus.OptedOutBy("MarketingUnsubscribe")
                    : ConsentStatus.NotOptedOut);

        var sent = new List<MailMessage>();
        await using var harness = await CreateHarnessAsync(campaign, members, new Mock<ICustomerStore>(), sent, consent.Object);

        // Act
        await using (var scope = harness.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<CampaignService>().StartSendingAsync(campaign, TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await harness.ProcessDueAsync();

        // Assert
        Assert.Empty(sent);
        Assert.Equal((0, 1, CampaignStatus.Sent), (campaign.Sent, campaign.Skipped, campaign.Status));
    }

    [Fact]
    public async Task AListCampaign_ImportsWithItsList_WhileTheMailingListsFeatureIsOn()
    {
        // Arrange
        var campaigns = new InMemoryCampaignStore();
        var lists = new Mock<ICampaignMailingLists>();
        lists.SetupGet(value => value.IsAvailable).Returns(true);
        lists.Setup(value => value.FindAsync(ListId, It.IsAny<CancellationToken>())).ReturnsAsync(new MailingList { ItemId = ListId, Name = "news" });

        var step = new MarketingCampaignsStep(campaigns, new InMemorySegmentStore(), RecipeRoundTrip.Localizer<MarketingCampaignsStep>(), lists.Object);
        var recipe = JsonNode.Parse($$"""
            { "name": "MarketingCampaigns", "Campaigns": [ { "ItemId": "news", "Name": "News", "AudienceKind": "MailingList", "MailingListId": "{{ListId}}", "SegmentId": "ignored", "Subject": "News", "Body": "Hi" } ] }
            """).AsObject();

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Empty(errors);

        var campaign = Assert.Single(campaigns.Entries);
        Assert.Equal(CampaignAudienceKinds.MailingList, campaign.AudienceKind);
        Assert.Equal(ListId, campaign.MailingListId);
        Assert.Null(campaign.SegmentId);
        Assert.Equal(CampaignStatus.Draft, campaign.Status);
    }

    [Fact]
    public async Task AListCampaign_IsReported_WhenItsListIsMissingOrTheFeatureIsOff()
    {
        // Arrange
        var recipe = JsonNode.Parse($$"""
            { "name": "MarketingCampaigns", "Campaigns": [ { "Name": "News", "AudienceKind": "MailingList", "MailingListId": "{{ListId}}", "Subject": "News", "Body": "Hi" } ] }
            """).AsObject();
        var withoutFeature = new InMemoryCampaignStore();
        var withoutList = new InMemoryCampaignStore();

        // Act
        var featureErrors = await RecipeRoundTrip.RunAsync(new MarketingCampaignsStep(withoutFeature, new InMemorySegmentStore(), RecipeRoundTrip.Localizer<MarketingCampaignsStep>()), recipe);
        var listErrors = await RecipeRoundTrip.RunAsync(new MarketingCampaignsStep(withoutList, new InMemorySegmentStore(), RecipeRoundTrip.Localizer<MarketingCampaignsStep>(), Mock.Of<ICampaignMailingLists>(value => value.IsAvailable)), recipe);

        // Assert
        Assert.Equal(["The campaign 'News' goes to a mailing list. Turn on the Mailing Lists feature first."], featureErrors);
        Assert.Equal(["The campaign 'News' goes to a mailing list that doesn't exist. Import the mailing lists first."], listErrors);
        Assert.Empty(withoutFeature.Entries);
        Assert.Empty(withoutList.Entries);
    }

    private static MailingListMember Member(string id, string listId, string email, string name, MailingListMemberStatus status)
        => new()
        {
            ItemId = id,
            ListId = listId,
            Contact = new ContactInfo { Email = email, Name = name },
            NormalizedEmail = email.ToLowerInvariant(),
            Status = status,
        };

    private static Task<OutreachTestHarness> CreateHarnessAsync(Campaign campaign, InMemoryMailingListMemberStore members, Mock<ICustomerStore> customers, List<MailMessage> sent, IConsentService consent = null)
    {
        var campaigns = new Mock<ICampaignStore>();
        campaigns.Setup(store => store.FindByIdAsync(campaign.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(campaign);

        var email = new Mock<IEmailService>();
        email.Setup(value => value.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<MailMessage, string, CancellationToken>((message, _, _) => sent.Add(message))
            .ReturnsAsync(Result.Success());

        var providers = new Mock<IEmailProviderResolver>();
        providers.Setup(value => value.GetAsync(It.IsAny<string>())).ReturnsAsync(Mock.Of<IEmailProvider>());

        // The Liquid passes the template through, so the test reads what the campaign adds to it.
        var liquid = new Mock<ILiquidTemplateManager>();
        liquid.Setup(value => value.RenderStringAsync(It.IsAny<string>(), It.IsAny<TextEncoder>(), It.IsAny<object>(), It.IsAny<IEnumerable<KeyValuePair<string, FluidValue>>>()))
            .Returns<string, TextEncoder, object, IEnumerable<KeyValuePair<string, FluidValue>>>((template, _, _, _) => Task.FromResult(template));

        var site = new Mock<ISite>();
        site.SetupGet(value => value.SiteName).Returns("Contoso");
        site.SetupGet(value => value.BaseUrl).Returns("https://store.example.com");
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return OutreachTestHarness.CreateAsync(services =>
        {
            services.AddSingleton(campaigns.Object);
            services.AddSingleton(Mock.Of<ISegmentStore>());
            services.AddSingleton(Mock.Of<ICustomerPurchasesStore>());
            services.AddSingleton(customers.Object);
            services.AddSingleton(email.Object);
            services.AddSingleton(providers.Object);
            services.AddSingleton(liquid.Object);
            services.AddSingleton(siteService.Object);
            services.AddSingleton(SignedLinkDoubles.Tokens());
            services.AddSingleton<IMailingListMemberStore>(members);
            services.AddScoped<IUnsubscribeLinks, UnsubscribeLinks>();
            services.AddSingleton<IStringLocalizer<CampaignService>>(new PassThroughStringLocalizer<CampaignService>());
            services.AddScoped<CampaignService>();
            services.AddOutreachChannelSender<EmailOutreachChannelSender>();
            services.AddOutreachAudienceSource<MailingListAudienceSource>();
            services.AddOutreachTemplateRenderer<MarketingCampaignTemplateRenderer>();
            services.AddOutreachRunObserver<MarketingCampaignRunObserver>();

            if (consent is not null)
            {
                services.AddSingleton(consent);
            }
        });
    }

    private sealed class InMemorySegmentStore : InMemoryCatalog<Segment>, ISegmentStore
    {
        public Task<(int Count, IReadOnlyList<Segment> Segments)> PageAsync(string search, int skip, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<(int, IReadOnlyList<Segment>)>((Entries.Count, Entries.Skip(skip).Take(take).ToList()));

        public Task<IReadOnlyList<Segment>> ListAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Segment>>(Entries.ToList());
    }

    private sealed class InMemoryCampaignStore : InMemoryCatalog<Campaign>, ICampaignStore
    {
        public Task<(int Count, IReadOnlyList<Campaign> Campaigns)> PageAsync(string search, int skip, int take, CancellationToken cancellationToken = default)
            => Task.FromResult<(int, IReadOnlyList<Campaign>)>((Entries.Count, Entries.Skip(skip).Take(take).ToList()));

        public Task<IReadOnlyList<Campaign>> ListDueAsync(DateTime utcNow, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Campaign>>([]);
    }
}
