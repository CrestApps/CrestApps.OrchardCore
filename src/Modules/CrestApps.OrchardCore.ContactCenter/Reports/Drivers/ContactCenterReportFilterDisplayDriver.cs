using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Drivers;

/// <summary>
/// Adds queue, agent, campaign, channel, direction, source, status, interaction type, dialer profile, queue state,
/// activity and decision reason filters to Contact Center reports.
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
    private readonly ActivitySourceOptions _activitySourceOptions;
    private readonly ActivityChannelOptions _activityChannelOptions;
    private readonly IDialerProfileManager _dialerProfileManager;

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
    /// <param name="activitySourceOptions">The activity sources registered by the enabled features.</param>
    /// <param name="activityChannelOptions">The activity channels registered by the enabled features.</param>
    /// <param name="dialerProfileManagers">The dialer profiles, offered when the Dialer feature is on; empty otherwise.</param>
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
        IOptions<ActivitySourceOptions> activitySourceOptions,
        IOptions<ActivityChannelOptions> activityChannelOptions,
        IEnumerable<IDialerProfileManager> dialerProfileManagers,
        IStringLocalizer<ContactCenterReportFilterDisplayDriver> stringLocalizer)
    {
        _dialerProfileManager = dialerProfileManagers.FirstOrDefault();
        _activitySourceOptions = activitySourceOptions.Value;
        _activityChannelOptions = activityChannelOptions.Value;
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
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.InteractionType, model.InteractionType);
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.DialerProfileId, model.DialerProfileId);
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.QueueState, model.QueueState);
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.ActivityItemId, model.ActivityItemId?.Trim());
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.DecisionReason, model.DecisionReason);

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
        model.InteractionType = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.InteractionType);
        model.DialerProfileId = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.DialerProfileId);
        model.QueueState = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.QueueState);
        model.ActivityItemId = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.ActivityItemId);
        model.DecisionReason = ContactCenterReportFilter.GetString(filter, ContactCenterReportFilter.DecisionReason);
        model.ShowQueueGroupFilter = filterNames.Contains(ContactCenterReportFilter.QueueGroupId);
        model.ShowQueueFilter = filterNames.Contains(ContactCenterReportFilter.QueueId);
        model.ShowAgentFilter = filterNames.Contains(ContactCenterReportFilter.AgentId);
        model.ShowCampaignFilter = filterNames.Contains(ContactCenterReportFilter.CampaignId);
        model.ShowCampaignGroupFilter = filterNames.Contains(ContactCenterReportFilter.CampaignGroupId);
        model.ShowChannelFilter = filterNames.Contains(ContactCenterReportFilter.Channel);
        model.ShowDirectionFilter = filterNames.Contains(ContactCenterReportFilter.Direction);
        model.ShowActivitySourceFilter = filterNames.Contains(ContactCenterReportFilter.ActivitySource);
        model.ShowActivityStatusFilter = filterNames.Contains(ContactCenterReportFilter.ActivityStatus);
        model.ShowInteractionTypeFilter = filterNames.Contains(ContactCenterReportFilter.InteractionType);
        model.ShowDialerProfileFilter = filterNames.Contains(ContactCenterReportFilter.DialerProfileId);
        model.ShowQueueStateFilter = filterNames.Contains(ContactCenterReportFilter.QueueState);
        model.ShowActivityItemFilter = filterNames.Contains(ContactCenterReportFilter.ActivityItemId);
        model.ShowDecisionReasonFilter = filterNames.Contains(ContactCenterReportFilter.DecisionReason);

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
            // Contact Center interactions are only ever voice calls. A report that also counts CRM activities (it
            // offers an activity source or status filter) can narrow them to SMS when activities can use SMS. No
            // feature creates email or chat work, so neither is offered; a value saved before stays listed.
            var channels = new List<SelectListItem>
            {
                new(S["Voice"], InteractionChannel.Voice.ToString()),
            };

            if ((model.ShowActivitySourceFilter || model.ShowActivityStatusFilter) &&
                _activityChannelOptions.Channels.TryGetValue(OmnichannelConstants.Channels.Sms, out var smsChannel))
            {
                channels.Add(new SelectListItem(smsChannel.DisplayName?.Value ?? S["SMS"].Value, InteractionChannel.Sms.ToString()));
            }

            ActivityFilterSelectListBuilder.KeepSelectedValue(channels, model.Channel);
            model.Channels = channels;
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
            // Only the sources the enabled features put on activities are offered.
            model.ActivitySources = ActivityFilterSelectListBuilder.BuildSourceItems(_activitySourceOptions, model.ActivitySource);
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

        if (model.ShowInteractionTypeFilter)
        {
            model.InteractionTypes =
            [
                new SelectListItem(S["Manual"], ActivityInteractionType.Manual.ToString()),
                new SelectListItem(S["Automated"], ActivityInteractionType.Automated.ToString()),
            ];
        }

        if (model.ShowDialerProfileFilter)
        {
            // Every profile is offered, turned off or not: a turned-off profile is exactly the one whose records stop
            // dialing. A profile that was deleted cannot be offered, so a value saved before stays listed.
            var profiles = _dialerProfileManager is null
                ? []
                : (await _dialerProfileManager.GetAllAsync())
                    .OrderBy(profile => profile.Name)
                    .Select(profile => new SelectListItem(
                        profile.Enabled ? profile.Name ?? profile.ItemId : S["{0} (turned off)", profile.Name ?? profile.ItemId].Value,
                        profile.ItemId))
                    .ToList();

            ActivityFilterSelectListBuilder.KeepSelectedValue(profiles, model.DialerProfileId);
            model.DialerProfiles = profiles;
        }

        if (model.ShowQueueStateFilter)
        {
            model.QueueStates =
            [
                new SelectListItem(S["Dialable now"], DialerQueueState.DialableNow.ToString()),
                new SelectListItem(S["Held back"], DialerQueueState.HeldBack.ToString()),
                new SelectListItem(S["Scheduled for later"], DialerQueueState.ScheduledForLater.ToString()),
                new SelectListItem(S["Not in any queue"], DialerQueueState.NotInAnyQueue.ToString()),
                new SelectListItem(S["Assigned to a user"], DialerQueueState.AssignedToUser.ToString()),
            ];
        }

        if (model.ShowDecisionReasonFilter)
        {
            model.DecisionReasons = DialerDecisionLabels.Options(S);
        }
    }

    private HashSet<string> GetFilterNames(string reportName)
    {
        if (_reportManager.GetReport(reportName) is not IReportFilterMetadata metadata)
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
