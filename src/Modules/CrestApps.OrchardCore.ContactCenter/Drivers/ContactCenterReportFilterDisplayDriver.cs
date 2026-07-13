using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

/// <summary>
/// Adds queue, agent, campaign, channel, direction, source, and status filters to Contact Center reports.
/// </summary>
public sealed class ContactCenterReportFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    private static readonly HashSet<string> _activityReports =
    [
        "contact-center-campaign-summary",
        "contact-center-subject-inventory",
    ];
    private static readonly HashSet<string> _workforceReports =
    [
        "contact-center-agent-time-summary",
        "contact-center-agent-daily-timecard",
        "contact-center-presence-status-duration",
        "contact-center-agent-break-analysis",
        "contact-center-ready-not-ready",
        "contact-center-agent-utilization",
        "contact-center-agent-occupancy",
        "contact-center-presence-reasons",
        "contact-center-presence-audit",
        "contact-center-queue-signed-in-hours",
        "contact-center-campaign-signed-in-hours",
        "contact-center-payroll-timecard",
    ];

    private readonly IActivityQueueManager _queueManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterReportFilterDisplayDriver"/> class.
    /// </summary>
    /// <param name="queueManager">The queue manager.</param>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="campaignManager">The campaign manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterReportFilterDisplayDriver(
        IActivityQueueManager queueManager,
        IAgentProfileManager agentManager,
        ICatalogManager<OmnichannelCampaign> campaignManager,
        IStringLocalizer<ContactCenterReportFilterDisplayDriver> stringLocalizer)
    {
        _queueManager = queueManager;
        _agentManager = agentManager;
        _campaignManager = campaignManager;
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        if (!IsContactCenterReport(filter.ReportName))
        {
            return null;
        }

        return Initialize<ContactCenterReportFilterViewModel>("ContactCenterReportFilter_Edit", async model =>
        {
            await PopulateAsync(model, filter);
        }).Location("Content:2");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ReportFilter filter, UpdateEditorContext context)
    {
        if (!IsContactCenterReport(filter.ReportName))
        {
            return null;
        }

        var model = new ContactCenterReportFilterViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.QueueId, model.QueueId);
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.AgentId, model.AgentId);
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.CampaignId, model.CampaignId);
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.Channel, model.Channel);
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.Direction, model.Direction);
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.ActivitySource, model.ActivitySource);
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.ActivityStatus, model.ActivityStatus);

        return Edit(filter, context);
    }

    private async Task PopulateAsync(ContactCenterReportFilterViewModel model, ReportFilter filter)
    {
        model.QueueId = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.QueueId);
        model.AgentId = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.AgentId);
        model.CampaignId = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.CampaignId);
        model.Channel = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.Channel);
        model.Direction = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.Direction);
        model.ActivitySource = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.ActivitySource);
        model.ActivityStatus = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.ActivityStatus);
        model.ShowActivityFilters = _activityReports.Contains(filter.ReportName);
        model.ShowWorkforceFilters = _workforceReports.Contains(filter.ReportName);
        model.ShowInteractionFilters = !model.ShowActivityFilters && !model.ShowWorkforceFilters;

        var queues = await _queueManager.GetAllAsync();
        var agents = await _agentManager.GetAllAsync();
        var campaigns = await _campaignManager.GetAllAsync();

        model.Queues = queues
            .OrderBy(queue => queue.Name)
            .Select(queue => new SelectListItem(queue.Name ?? queue.ItemId, queue.ItemId))
            .ToList();

        model.Agents = agents
            .OrderBy(agent => ResolveAgentName(agent))
            .Select(agent => new SelectListItem(ResolveAgentName(agent), agent.ItemId))
            .ToList();

        model.Campaigns = campaigns
            .OrderBy(campaign => campaign.DisplayText)
            .Select(campaign => new SelectListItem(campaign.DisplayText ?? campaign.ItemId, campaign.ItemId))
            .ToList();

        model.Channels =
        [
            new SelectListItem(S["Voice"], InteractionChannel.Voice.ToString()),
            new SelectListItem(S["SMS"], InteractionChannel.Sms.ToString()),
            new SelectListItem(S["Email"], InteractionChannel.Email.ToString()),
            new SelectListItem(S["Chat"], InteractionChannel.Chat.ToString()),
        ];

        model.Directions =
        [
            new SelectListItem(S["Inbound"], InteractionDirection.Inbound.ToString()),
            new SelectListItem(S["Outbound"], InteractionDirection.Outbound.ToString()),
        ];

        model.ActivitySources =
        [
            new SelectListItem(S["Manual"], ActivitySources.Manual),
            new SelectListItem(S["Automatic"], ActivitySources.Automatic),
            new SelectListItem(S["Dialer"], ActivitySources.Dialer),
            new SelectListItem(S["Preview dial"], ActivitySources.PreviewDial),
            new SelectListItem(S["Power dial"], ActivitySources.PowerDial),
            new SelectListItem(S["Progressive dial"], ActivitySources.ProgressiveDial),
            new SelectListItem(S["Predictive dial"], ActivitySources.PredictiveDial),
            new SelectListItem(S["Callback"], ActivitySources.Callback),
            new SelectListItem(S["Inbound"], ActivitySources.Inbound),
            new SelectListItem(S["Workflow"], ActivitySources.Workflow),
            new SelectListItem(S["API"], ActivitySources.Api),
        ];

        model.ActivityStatuses =
        [
            new SelectListItem(S["Not started"], ActivityStatus.NotStated.ToString()),
            new SelectListItem(S["Awaiting agent response"], ActivityStatus.AwaitingAgentResponse.ToString()),
            new SelectListItem(S["Awaiting customer answer"], ActivityStatus.AwaitingCustomerAnswer.ToString()),
            new SelectListItem(S["Completed"], ActivityStatus.Completed.ToString()),
            new SelectListItem(S["Pending"], ActivityStatus.Pending.ToString()),
            new SelectListItem(S["Scheduled"], ActivityStatus.Scheduled.ToString()),
            new SelectListItem(S["Reserved"], ActivityStatus.Reserved.ToString()),
            new SelectListItem(S["Dialing"], ActivityStatus.Dialing.ToString()),
            new SelectListItem(S["In progress"], ActivityStatus.InProgress.ToString()),
            new SelectListItem(S["Failed"], ActivityStatus.Failed.ToString()),
            new SelectListItem(S["Cancelled"], ActivityStatus.Cancelled.ToString()),
            new SelectListItem(S["Purged"], ActivityStatus.Purged.ToString()),
        ];
    }

    private static bool IsContactCenterReport(string reportName)
    {
        return reportName?.StartsWith("contact-center-", StringComparison.Ordinal) == true;
    }

    private static string ResolveAgentName(AgentProfile agent)
    {
        if (!string.IsNullOrWhiteSpace(agent.DisplayName))
        {
            return agent.DisplayName;
        }

        if (!string.IsNullOrWhiteSpace(agent.UserName))
        {
            return agent.UserName;
        }

        return agent.ItemId;
    }
}
