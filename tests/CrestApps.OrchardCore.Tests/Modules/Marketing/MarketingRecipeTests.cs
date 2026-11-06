using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Marketing.Deployments;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Marketing.Recipes;
using CrestApps.OrchardCore.Marketing.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Marketing;

/// <summary>
/// Marketing segments and campaigns travel between sites: the deployment step writes the segments before the
/// campaigns, a campaign travels without its delivery and imports as a draft, a second run adds nothing, and a
/// campaign that has started sending is left as it is.
/// </summary>
public sealed class MarketingRecipeTests
{
    private const string SegmentId = "4sg00000000000000000000001";
    private const string CampaignId = "4cp00000000000000000000001";

    [Fact]
    public async Task SegmentsAndCampaigns_RoundTripIntoAnEmptySite_AsDrafts()
    {
        // Arrange
        var sourceSegments = new InMemorySegmentStore();
        var sourceCampaigns = new InMemoryCampaignStore();

        sourceSegments.Entries.Add(new Segment { ItemId = SegmentId, Name = "Lapsed buyers", Rules = new SegmentRules { MinOrders = 1, LastOrderAtLeastDaysAgo = 180, Country = "DE" } });
        sourceCampaigns.Entries.Add(new Campaign
        {
            ItemId = CampaignId,
            Name = "Win back",
            SegmentId = SegmentId,
            Subject = "We miss you",
            Body = "Come back for 10% off.",
            Status = CampaignStatus.Sent,
            Cursor = "after-500",
            Sent = 480,
            OutreachRunId = "run-on-the-source-site",
            Failed = 20,
        });

        var targetSegments = new InMemorySegmentStore();
        var targetCampaigns = new InMemoryCampaignStore();
        var segmentStep = new MarketingSegmentsStep(targetSegments, RecipeRoundTrip.Localizer<MarketingSegmentsStep>());
        var campaignStep = new MarketingCampaignsStep(targetCampaigns, targetSegments, RecipeRoundTrip.Localizer<MarketingCampaignsStep>());

        // Act
        var steps = await ExportAsync(new MarketingDeploymentSource(sourceSegments, sourceCampaigns));
        var errors = new List<string>();

        for (var run = 0; run < 2; run++)
        {
            errors.AddRange(await RecipeRoundTrip.RunAsync(segmentStep, steps[0]));
            errors.AddRange(await RecipeRoundTrip.RunAsync(campaignStep, steps[1]));
        }

        // Assert
        Assert.Empty(errors);
        Assert.Equal(["MarketingSegments", "MarketingCampaigns"], steps.Select(step => step["name"].GetValue<string>()));
        Assert.False(steps[1]["Campaigns"][0].AsObject().ContainsKey("Sent"));
        Assert.False(steps[1]["Campaigns"][0].AsObject().ContainsKey("OutreachRunId"));

        var segment = Assert.Single(targetSegments.Entries);
        var campaign = Assert.Single(targetCampaigns.Entries);

        Assert.Equal(SegmentId, segment.ItemId);
        Assert.Equal(180, segment.Rules.LastOrderAtLeastDaysAgo);
        Assert.Equal("DE", segment.Rules.Country);
        Assert.Equal(CampaignId, campaign.ItemId);
        Assert.Equal("We miss you", campaign.Subject);
        Assert.Equal(CampaignStatus.Draft, campaign.Status);
        Assert.Equal(0, campaign.Sent);
        Assert.Null(campaign.Cursor);
        Assert.Null(campaign.OutreachRunId);
    }

    [Fact]
    public async Task ACampaignThatStartedSending_IsLeftAsItIs()
    {
        // Arrange
        var segments = new InMemorySegmentStore();
        var campaigns = new InMemoryCampaignStore();

        segments.Entries.Add(new Segment { ItemId = SegmentId, Name = "Everyone" });
        campaigns.Entries.Add(new Campaign { ItemId = CampaignId, Name = "Launch", SegmentId = SegmentId, Subject = "Hello", Body = "Hi", Status = CampaignStatus.Sending, Sent = 10 });

        var step = new MarketingCampaignsStep(campaigns, segments, RecipeRoundTrip.Localizer<MarketingCampaignsStep>());
        var recipe = JsonNode.Parse($$"""
            { "name": "MarketingCampaigns", "Campaigns": [ { "ItemId": "{{CampaignId}}", "Name": "Launch", "SegmentId": "{{SegmentId}}", "Subject": "Changed", "Body": "Hi" } ] }
            """).AsObject();

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Empty(errors);

        var campaign = Assert.Single(campaigns.Entries);

        Assert.Equal("Hello", campaign.Subject);
        Assert.Equal(CampaignStatus.Sending, campaign.Status);
        Assert.Equal(10, campaign.Sent);
    }

    [Fact]
    public async Task ACampaignWhoseSegmentIsMissing_IsReported()
    {
        // Arrange
        var campaigns = new InMemoryCampaignStore();
        var step = new MarketingCampaignsStep(campaigns, new InMemorySegmentStore(), RecipeRoundTrip.Localizer<MarketingCampaignsStep>());
        var recipe = JsonNode.Parse("""
            { "name": "MarketingCampaigns", "Campaigns": [ { "Name": "Launch", "SegmentId": "missing", "Subject": "Hello", "Body": "Hi" } ] }
            """).AsObject();

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Equal(["The campaign 'Launch' goes to a segment that doesn't exist. Import the segments first."], errors);
        Assert.Empty(campaigns.Entries);
    }

    private static async Task<JsonObject[]> ExportAsync(MarketingDeploymentSource source)
    {
        var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());

        await source.ProcessDeploymentStepAsync(new MarketingDeploymentStep(), result);

        return [.. result.Steps.Select(step => JsonNode.Parse(step.ToJsonString()).AsObject())];
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
