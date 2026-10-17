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
    public const string DispositionIds = "DispositionIds";
    public const string Page = "Page";

    public static async Task<OmnichannelReportCriteria> GetCriteriaAsync(
        ReportFilter filter,
        ICatalogManager<OmnichannelCampaign> campaignManager,
        ActivitySourceOptions sourceOptions,
        CancellationToken cancellationToken = default)
    {
        var criteria = new OmnichannelReportCriteria
        {
            CampaignId = filter.GetOrDefault<string>(CampaignId),
            CampaignGroupId = filter.GetOrDefault<string>(CampaignGroupId),
            Channel = filter.GetOrDefault<string>(Channel),
            Source = filter.GetOrDefault<string>(Source),
            Status = filter.TryGet<ActivityStatus>(Status, out var status) ? status : null,
            DispositionIds = GetDispositionIds(filter),
            Page = filter.TryGet<int>(Page, out var page) ? Math.Max(1, page) : 1,
        };

        if (!string.IsNullOrEmpty(criteria.Source))
        {
            // One source can stand for several stored values, such as every dialer mode for the Dialer source.
            criteria.Sources = sourceOptions.GetStoredValues(criteria.Source).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrEmpty(criteria.CampaignGroupId))
        {
            criteria.CampaignIds = (await campaignManager.GetAllAsync(cancellationToken))
                .Where(campaign => campaign.CampaignGroupId == criteria.CampaignGroupId)
                .Select(campaign => campaign.ItemId)
                .ToHashSet(StringComparer.Ordinal);
        }

        return criteria;
    }

    private static HashSet<string> GetDispositionIds(ReportFilter filter)
    {
        // Several dispositions can be picked together, so the filter stores them as an array.
        var dispositionIds = filter.GetOrDefault<string[]>(DispositionIds);

        if (dispositionIds is null)
        {
            return null;
        }

        var selected = dispositionIds
            .Where(dispositionId => !string.IsNullOrWhiteSpace(dispositionId))
            .Select(dispositionId => dispositionId.Trim())
            .ToHashSet(StringComparer.Ordinal);

        return selected.Count > 0
            ? selected
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

    public IReadOnlySet<string> Sources { get; set; }

    public ActivityStatus? Status { get; set; }

    /// <summary>
    /// The dispositions to keep, or <see langword="null"/> when every disposition is kept. An activity with no
    /// disposition never matches a selection.
    /// </summary>
    public IReadOnlySet<string> DispositionIds { get; set; }

    /// <summary>
    /// The one-based page of rows a report that pages its rows shows. Reports that do not page ignore it.
    /// </summary>
    public int Page { get; set; } = 1;
}
