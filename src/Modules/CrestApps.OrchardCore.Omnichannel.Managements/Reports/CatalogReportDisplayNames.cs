using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Reports;

internal static class CatalogReportDisplayNames
{
    public static IReadOnlyDictionary<string, string> ForCampaigns(IEnumerable<OmnichannelCampaign> campaigns)
    {
        return campaigns
            .Where(campaign => !string.IsNullOrWhiteSpace(campaign.ItemId) && !string.IsNullOrWhiteSpace(campaign.DisplayText))
            .ToDictionary(campaign => campaign.ItemId, campaign => campaign.DisplayText, StringComparer.Ordinal);
    }

    public static IReadOnlyDictionary<string, string> ForDispositions(IEnumerable<OmnichannelDisposition> dispositions)
    {
        return dispositions
            .Where(disposition => !string.IsNullOrWhiteSpace(disposition.ItemId) && !string.IsNullOrWhiteSpace(disposition.Name))
            .ToDictionary(disposition => disposition.ItemId, disposition => disposition.Name, StringComparer.Ordinal);
    }

    public static string Resolve(
        string id,
        IReadOnlyDictionary<string, string> names,
        string emptyText,
        string unknownText)
    {
        if (string.IsNullOrEmpty(id))
        {
            return emptyText;
        }

        return names.GetValueOrDefault(id, unknownText);
    }
}
