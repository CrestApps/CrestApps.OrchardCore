using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Reports;

internal static class OmnichannelReportFilter
{
    public const string CampaignId = "CampaignId";
    public const string CampaignGroupId = "CampaignGroupId";
    public const string Channel = "Channel";
    public const string Source = "Source";
    public const string Status = "Status";

    public static async Task<OmnichannelReportCriteria> GetCriteriaAsync(
        ReportFilter filter,
        ICatalogManager<OmnichannelCampaign> campaignManager,
        CancellationToken cancellationToken = default)
    {
        var criteria = new OmnichannelReportCriteria
        {
            CampaignId = filter.Get<string>(CampaignId),
            CampaignGroupId = filter.Get<string>(CampaignGroupId),
            Channel = filter.Get<string>(Channel),
            Source = filter.Get<string>(Source),
            Status = filter.TryGet<ActivityStatus>(Status, out var status) ? status : null,
        };

        if (!string.IsNullOrEmpty(criteria.CampaignGroupId))
        {
            criteria.CampaignIds = (await campaignManager.GetAllAsync(cancellationToken))
                .Where(campaign => campaign.CampaignGroupId == criteria.CampaignGroupId)
                .Select(campaign => campaign.ItemId)
                .ToHashSet(StringComparer.Ordinal);
        }

        return criteria;
    }
}

internal sealed class OmnichannelReportCriteria
{
    public string CampaignId { get; set; }

    public string CampaignGroupId { get; set; }

    public IReadOnlySet<string> CampaignIds { get; set; }

    public string Channel { get; set; }

    public string Source { get; set; }

    public ActivityStatus? Status { get; set; }
}
