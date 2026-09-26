using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Drivers;

/// <summary>
/// Adds queue, agent, campaign, channel, direction, source, and status filters to Contact Center reports.
/// </summary>
public sealed class ContactCenterReportFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    private readonly IReportManager _reportManager;
    private readonly IActivityQueueGroupManager _queueGroupManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly ICatalogManager<OmnichannelCampaignGroup> _campaignGroupManager;
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterReportFilterDisplayDriver"/> class.
    /// </summary>
    /// <param name="reportManager">The report manager.</param>
    /// <param name="queueGroupManager">The queue-group manager.</param>
    /// <param name="queueManager">The queue manager.</param>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="campaignManager">The campaign manager.</param>
    /// <param name="campaignGroupManager">The campaign group manager.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="displayNameProvider">The user display name provider.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterReportFilterDisplayDriver(
        IReportManager reportManager,
        IActivityQueueGroupManager queueGroupManager,
        IActivityQueueManager queueManager,
        IAgentProfileManager agentManager,
        ICatalogManager<OmnichannelCampaign> campaignManager,
        ICatalogManager<OmnichannelCampaignGroup> campaignGroupManager,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        IStringLocalizer<ContactCenterReportFilterDisplayDriver> stringLocalizer)
    {
        _reportManager = reportManager;
        _queueGroupManager = queueGroupManager;
        _queueManager = queueManager;
        _agentManager = agentManager;
        _campaignManager = campaignManager;
        _campaignGroupManager = campaignGroupManager;
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        if (GetFilterNames(filter.ReportName) is null)
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
        if (GetFilterNames(filter.ReportName) is null)
        {
            return null;
        }

        var model = new ContactCenterReportFilterViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.QueueGroupId, model.QueueGroupId);
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.QueueId, model.QueueId);
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.AgentId, model.AgentId);
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.CampaignId, model.CampaignId);
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.CampaignGroupId, model.CampaignGroupId);
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.Channel, model.Channel);
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.Direction, model.Direction);
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.ActivitySource, model.ActivitySource);
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.ActivityStatus, model.ActivityStatus);

        return Edit(filter, context);
    }

    private async Task PopulateAsync(ContactCenterReportFilterViewModel model, ReportFilter filter)
    {
        var filterNames = GetFilterNames(filter.ReportName);

        if (filterNames is null)
        {
            return;
        }

        model.QueueGroupId = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.QueueGroupId);
        model.QueueId = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.QueueId);
        model.AgentId = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.AgentId);
        model.CampaignId = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.CampaignId);
        model.CampaignGroupId = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.CampaignGroupId);
        model.Channel = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.Channel);
        model.Direction = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.Direction);
        model.ActivitySource = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.ActivitySource);
        model.ActivityStatus = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.ActivityStatus);
        model.ShowQueueGroupFilter = filterNames.Contains(ContactCenterReportFilter.QueueGroupId);
        model.ShowQueueFilter = filterNames.Contains(ContactCenterReportFilter.QueueId);
        model.ShowAgentFilter = filterNames.Contains(ContactCenterReportFilter.AgentId);
        model.ShowCampaignFilter = filterNames.Contains(ContactCenterReportFilter.CampaignId);
        model.ShowCampaignGroupFilter = filterNames.Contains(ContactCenterReportFilter.CampaignGroupId);
        model.ShowChannelFilter = filterNames.Contains(ContactCenterReportFilter.Channel);
        model.ShowDirectionFilter = filterNames.Contains(ContactCenterReportFilter.Direction);
        model.ShowActivitySourceFilter = filterNames.Contains(ContactCenterReportFilter.ActivitySource);
        model.ShowActivityStatusFilter = filterNames.Contains(ContactCenterReportFilter.ActivityStatus);

        if (model.ShowQueueGroupFilter)
        {
            var queueGroups = await _queueGroupManager.GetAllAsync();

            model.QueueGroups = queueGroups
                .OrderBy(group => group.Name)
                .Select(group => new SelectListItem(group.Name ?? group.ItemId, group.ItemId))
                .ToList();
        }

        if (model.ShowQueueFilter)
        {
            var queues = await _queueManager.GetAllAsync();

            model.Queues = queues
                .OrderBy(queue => queue.Name)
                .Select(queue => new SelectListItem(queue.Name ?? queue.ItemId, queue.ItemId))
                .ToList();
        }

        if (model.ShowAgentFilter)
        {
            var agents = await _agentManager.GetAllAsync();
            var agentOptions = new List<SelectListItem>();

            foreach (var agent in agents)
            {
                agentOptions.Add(new SelectListItem(await ResolveAgentNameAsync(agent), agent.ItemId));
            }

            model.Agents = agentOptions
                .OrderBy(option => option.Text)
                .ToList();
        }

        if (model.ShowCampaignFilter)
        {
            var campaigns = await _campaignManager.GetAllAsync();

            model.Campaigns = campaigns
                .OrderBy(campaign => campaign.DisplayText)
                .Select(campaign => new SelectListItem(campaign.DisplayText ?? campaign.ItemId, campaign.ItemId))
                .ToList();
        }

        if (model.ShowCampaignGroupFilter)
        {
            var campaignGroups = await _campaignGroupManager.GetAllAsync();

            model.CampaignGroups = campaignGroups
                .OrderBy(group => group.DisplayText)
                .Select(group => new SelectListItem(group.DisplayText ?? group.ItemId, group.ItemId))
                .ToList();
        }

        if (model.ShowChannelFilter)
        {
            model.Channels =
            [
                new SelectListItem(S["Voice"], InteractionChannel.Voice.ToString()),
                new SelectListItem(S["SMS"], InteractionChannel.Sms.ToString()),
                new SelectListItem(S["Email"], InteractionChannel.Email.ToString()),
                new SelectListItem(S["Chat"], InteractionChannel.Chat.ToString()),
            ];
        }

        if (model.ShowDirectionFilter)
        {
            model.Directions =
            [
                new SelectListItem(S["Inbound"], InteractionDirection.Inbound.ToString()),
                new SelectListItem(S["Outbound"], InteractionDirection.Outbound.ToString()),
            ];
        }

        if (model.ShowActivitySourceFilter)
        {
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
        }

        if (model.ShowActivityStatusFilter)
        {
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
    }

    private HashSet<string> GetFilterNames(string reportName)
    {
        if (_reportManager.FindByName(reportName) is not IReportFilterMetadata metadata)
        {
            return null;
        }

        return metadata.FilterNames.ToHashSet(StringComparer.Ordinal);
    }

    private async Task<string> ResolveAgentNameAsync(AgentProfile agent)
    {
        if (!string.IsNullOrEmpty(agent.UserId))
        {
            var user = await _userManager.FindByIdAsync(agent.UserId);

            if (user is not null)
            {
                var displayName = await _displayNameProvider.GetAsync(user);

                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName;
                }
            }
        }

        return S["(Unknown agent)"].Value;
    }
}
