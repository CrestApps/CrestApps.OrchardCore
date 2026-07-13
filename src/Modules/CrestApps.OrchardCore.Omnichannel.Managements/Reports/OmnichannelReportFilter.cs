using CrestApps.Core.Services;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
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
            CampaignId = GetString(filter, CampaignId),
            CampaignGroupId = GetString(filter, CampaignGroupId),
            Channel = GetString(filter, Channel),
            Source = GetString(filter, Source),
            Status = GetStatus(filter),
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

    public static string GetString(ReportFilter filter, string key)
    {
        return filter.Properties.TryGetPropertyValue(key, out var value)
            ? value?.GetValue<string>()
            : null;
    }

    public static void SetString(ReportFilter filter, string key, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            filter.Properties.Remove(key);

            return;
        }

        filter.Properties[key] = JsonValue.Create(value);
    }

    private static ActivityStatus? GetStatus(ReportFilter filter)
    {
        var value = GetString(filter, Status);

        return Enum.TryParse<ActivityStatus>(value, ignoreCase: true, out var status)
            ? status
            : null;
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
