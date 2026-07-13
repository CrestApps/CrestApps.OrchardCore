using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports;

internal static class ContactCenterReportFilter
{
    public const string QueueId = "QueueId";
    public const string AgentId = "AgentId";
    public const string CampaignId = "CampaignId";
    public const string Channel = "Channel";
    public const string Direction = "Direction";
    public const string ActivitySource = "ActivitySource";
    public const string ActivityStatus = "ActivityStatus";

    public static ContactCenterReportCriteria GetCriteria(ReportFilter filter)
    {
        return new ContactCenterReportCriteria
        {
            QueueId = GetString(filter, QueueId),
            AgentId = GetString(filter, AgentId),
            CampaignId = GetString(filter, CampaignId),
            ActivitySource = GetString(filter, ActivitySource),
            Channel = GetEnum<InteractionChannel>(filter, Channel),
            Direction = GetEnum<InteractionDirection>(filter, Direction),
            ActivityStatus = GetEnum<ActivityStatus>(filter, ActivityStatus),
        };
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

    private static T? GetEnum<T>(ReportFilter filter, string key)
        where T : struct, Enum
    {
        var value = GetString(filter, key);

        return Enum.TryParse<T>(value, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }
}
