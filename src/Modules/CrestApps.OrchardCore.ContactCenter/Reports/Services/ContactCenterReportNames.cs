using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Names the queue and the agent a report row describes.
/// </summary>
/// <remarks>
/// A call routed straight to one agent is carried under a synthetic queue id that is not a stored queue, so it is
/// named for what it is rather than shown as that id. Work no agent took is reported as having no agent, which is
/// not the same as an agent whose profile can no longer be found.
/// </remarks>
internal static class ContactCenterReportNames
{
    public static string Queue(string queueId, IReadOnlyDictionary<string, ActivityQueue> queues, IStringLocalizer S)
    {
        if (string.IsNullOrWhiteSpace(queueId))
        {
            return S["(Not set)"].Value;
        }

        if (string.Equals(queueId, ContactCenterConstants.DirectRouting.QueueId, StringComparison.Ordinal))
        {
            return S["Direct to agent"].Value;
        }

        return queues is not null && queues.TryGetValue(queueId, out var queue) ? queue.Name : queueId;
    }

    public static string Agent(string agentId, IReadOnlyDictionary<string, string> agentUserNames, IStringLocalizer S)
    {
        if (string.IsNullOrEmpty(agentId))
        {
            return S["(No agent)"].Value;
        }

        return ReportValue.UserDisplayName(
            agentUserNames.TryGetValue(agentId, out var userName) ? userName : null,
            S["(Unknown agent)"].Value);
    }
}
