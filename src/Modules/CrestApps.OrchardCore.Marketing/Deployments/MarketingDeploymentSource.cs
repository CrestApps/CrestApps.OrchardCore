using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Marketing.Recipes;
using CrestApps.OrchardCore.Marketing.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Marketing.Deployments;

/// <summary>
/// Writes the marketing segments, then the campaigns, into a <c>MarketingSegments</c> and a <c>MarketingCampaigns</c>
/// recipe step, so each campaign's segment is imported before it. A campaign is written without its delivery: its
/// status, schedule, progress and counts belong to the site that sends it.
/// </summary>
internal sealed class MarketingDeploymentSource : DeploymentSourceBase<MarketingDeploymentStep>
{
    private static readonly string[] _deliveryMembers =
    [
        nameof(Campaign.Status),
        nameof(Campaign.ScheduledUtc),
        nameof(Campaign.StartedUtc),
        nameof(Campaign.CompletedUtc),
        nameof(Campaign.Cursor),
        nameof(Campaign.Sent),
        nameof(Campaign.Skipped),
        nameof(Campaign.Failed),
        nameof(Campaign.OutreachRunId),
    ];

    private readonly ISegmentStore _segmentStore;
    private readonly ICampaignStore _campaignStore;

    public MarketingDeploymentSource(ISegmentStore segmentStore, ICampaignStore campaignStore)
    {
        _segmentStore = segmentStore;
        _campaignStore = campaignStore;
    }

    protected override async Task ProcessAsync(MarketingDeploymentStep step, DeploymentPlanResult result)
    {
        var segments = (await _segmentStore.GetAllAsync()).OrderBy(segment => segment.Name, StringComparer.OrdinalIgnoreCase);
        var campaigns = (await _campaignStore.GetAllAsync()).OrderBy(campaign => campaign.Name, StringComparer.OrdinalIgnoreCase);

        result.Steps.Add(new JsonObject
        {
            ["name"] = MarketingSegmentsStep.StepKey,
            [MarketingSegmentsStep.EntriesName] = new JsonArray([.. segments.Select(segment => (JsonNode)CatalogRecordSerializer.Export(segment))]),
        });

        result.Steps.Add(new JsonObject
        {
            ["name"] = MarketingCampaignsStep.StepKey,
            [MarketingCampaignsStep.EntriesName] = new JsonArray([.. campaigns.Select(campaign =>
            {
                var node = CatalogRecordSerializer.Export(campaign);

                foreach (var member in _deliveryMembers)
                {
                    node.Remove(member);
                }

                return (JsonNode)node;
            })]),
        });
    }
}
