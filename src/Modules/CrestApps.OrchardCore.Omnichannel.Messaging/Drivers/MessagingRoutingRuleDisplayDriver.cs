using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Drivers;

/// <summary>
/// Renders a messaging routing rule's admin list row and its editor: when the rule applies and which conversations it
/// takes, who can get them, and how one agent is picked.
/// </summary>
internal sealed class MessagingRoutingRuleDisplayDriver : DisplayDriver<MessagingRoutingRule>
{
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IOmnichannelChannelEndpointManager _endpointManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly ICatalog<OmnichannelCampaign> _campaigns;
    private readonly ITeamManager _teamManager;
    private readonly IRoleService _roleService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingRoutingRuleDisplayDriver"/> class.
    /// </summary>
    /// <param name="channelResolver">The messaging channels.</param>
    /// <param name="endpointManager">The business addresses.</param>
    /// <param name="queueManager">The queues.</param>
    /// <param name="campaigns">The campaigns, when the Omnichannel campaigns are enabled.</param>
    /// <param name="teamManager">The teams.</param>
    /// <param name="roleService">The roles.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MessagingRoutingRuleDisplayDriver(
        IMessagingChannelResolver channelResolver,
        IOmnichannelChannelEndpointManager endpointManager,
        IActivityQueueManager queueManager,
        IEnumerable<ICatalog<OmnichannelCampaign>> campaigns,
        ITeamManager teamManager,
        IRoleService roleService,
        IStringLocalizer<MessagingRoutingRuleDisplayDriver> stringLocalizer)
    {
        _channelResolver = channelResolver;
        _endpointManager = endpointManager;
        _queueManager = queueManager;
        _campaigns = campaigns.FirstOrDefault();
        _teamManager = teamManager;
        _roleService = roleService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(MessagingRoutingRule rule, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = rule.DisplayText?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("MessagingRoutingRule_Fields_SummaryAdmin", rule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("MessagingRoutingRule_Description_SummaryAdmin", rule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("MessagingRoutingRule_DefaultTags_SummaryAdmin", rule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Routing:5"),
            View("MessagingRoutingRule_DefaultMeta_SummaryAdmin", rule)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Order:5"),
            View("MessagingRoutingRule_Buttons_SummaryAdmin", rule).Location("Actions:5"));
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(MessagingRoutingRule rule, BuildEditorContext context)
    {
        // One shape for the whole editor, so the option lists are read once; the view groups the fields in cards.
        return Initialize<MessagingRoutingRuleViewModel>("MessagingRoutingRuleFields_Edit", model => PopulateAsync(model, rule, context.IsNew))
            .Location("Content:1");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(MessagingRoutingRule rule, UpdateEditorContext context)
    {
        var model = new MessagingRoutingRuleViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        rule.DisplayText = model.DisplayText?.Trim();
        rule.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        rule.Enabled = model.Enabled;
        rule.Order = model.Order;
        rule.Triggers =
            (model.OnNewConversation ? MessagingRoutingRuleTriggers.NewConversation : MessagingRoutingRuleTriggers.None) |
            (model.OnContactReplied ? MessagingRoutingRuleTriggers.ContactReplied : MessagingRoutingRuleTriggers.None) |
            (model.OnStartedWithoutAgent ? MessagingRoutingRuleTriggers.StartedWithoutAgent : MessagingRoutingRuleTriggers.None) |
            (model.OnWaiting ? MessagingRoutingRuleTriggers.Waiting : MessagingRoutingRuleTriggers.None);
        rule.WaitingMinutes = model.WaitingMinutes;
        rule.Channels = Clean(model.Channels);
        rule.ServiceAddresses = Clean(model.ServiceAddresses);
        rule.QueueIds = Clean(model.QueueIds);
        rule.CampaignIds = Clean(model.CampaignIds);
        rule.CandidateSource = model.CandidateSource;
        rule.CandidateQueueId = string.IsNullOrWhiteSpace(model.CandidateQueueId) ? null : model.CandidateQueueId.Trim();

        // Only the list the chosen source reads is kept, so a rule never carries a stale list it no longer uses.
        rule.TeamIds = rule.CandidateSource == MessagingRoutingCandidateSources.Teams ? Clean(model.TeamIds) : [];
        rule.RoleNames = rule.CandidateSource == MessagingRoutingCandidateSources.Roles ? Clean(model.RoleNames) : [];
        rule.UserIds = rule.CandidateSource == MessagingRoutingCandidateSources.Users ? Clean(model.UserIds) : [];
        rule.Strategy = model.Strategy;
        rule.OnlyAvailableAgents = model.OnlyAvailableAgents;
        rule.RespectMaxConcurrent = model.RespectMaxConcurrent;

        // The storage rules (a name, a moment, a source with something to read) live on the handler, so the editor, a
        // recipe and a deployment plan all enforce them.
        return Edit(rule, context);
    }

    private async ValueTask PopulateAsync(MessagingRoutingRuleViewModel model, MessagingRoutingRule rule, bool isNew)
    {
        model.DisplayText = rule.DisplayText;
        model.Description = rule.Description;
        model.Enabled = isNew || rule.Enabled;
        model.Order = rule.Order;
        model.OnNewConversation = rule.Triggers.HasFlag(MessagingRoutingRuleTriggers.NewConversation);
        model.OnContactReplied = rule.Triggers.HasFlag(MessagingRoutingRuleTriggers.ContactReplied);
        model.OnStartedWithoutAgent = rule.Triggers.HasFlag(MessagingRoutingRuleTriggers.StartedWithoutAgent);
        model.OnWaiting = rule.Triggers.HasFlag(MessagingRoutingRuleTriggers.Waiting);
        model.WaitingMinutes = rule.WaitingMinutes > 0 ? rule.WaitingMinutes : MessagingRoutingRule.DefaultWaitingMinutes;
        model.Channels = rule.Channels?.ToArray() ?? [];
        model.ServiceAddresses = rule.ServiceAddresses?.ToArray() ?? [];
        model.QueueIds = rule.QueueIds?.ToArray() ?? [];
        model.CampaignIds = rule.CampaignIds?.ToArray() ?? [];
        model.CandidateSource = rule.CandidateSource ?? MessagingRoutingCandidateSources.QueueSignedIn;
        model.CandidateQueueId = rule.CandidateQueueId;
        model.TeamIds = rule.TeamIds?.ToArray() ?? [];
        model.RoleNames = rule.RoleNames?.ToArray() ?? [];
        model.UserIds = rule.UserIds?.ToArray() ?? [];
        model.Strategy = rule.Strategy ?? MessagingRoutingRuleStrategies.RoundRobin;
        model.OnlyAvailableAgents = rule.OnlyAvailableAgents;
        model.RespectMaxConcurrent = rule.RespectMaxConcurrent;

        var channels = _channelResolver.GetAll();

        model.AvailableChannels = channels
            .Select(channel => new SelectListItem(channel.DisplayName.Value, channel.Name, model.Channels.Contains(channel.Name, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        var endpoints = await _endpointManager.GetAllAsync();
        var addresses = new List<SelectListItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var channel in channels)
        {
            var group = new SelectListGroup { Name = channel.DisplayName.Value };

            foreach (var endpoint in endpoints.Where(endpoint => endpoint.HasCapability(channel.Name)))
            {
                // Stored as the channel writes a conversation's address, so a rule compares like with like.
                var value = channel.NormalizeAddress(endpoint.Value);

                if (string.IsNullOrEmpty(value) || !seen.Add(value))
                {
                    continue;
                }

                var address = channel.FormatAddress(endpoint.Value);

                addresses.Add(new SelectListItem
                {
                    Text = string.IsNullOrEmpty(endpoint.DisplayText) ? address : $"{endpoint.DisplayText} ({address})",
                    Value = value,
                    Group = group,
                    Selected = model.ServiceAddresses.Contains(value, StringComparer.OrdinalIgnoreCase),
                });
            }
        }

        // An address the rule names that is no longer a messaging address is still shown, so saving keeps it.
        foreach (var value in model.ServiceAddresses.Where(value => !seen.Contains(value)))
        {
            addresses.Add(new SelectListItem(value, value, true));
        }

        model.AvailableServiceAddresses = addresses;

        var queues = (await _queueManager.GetAllAsync())
            .OrderBy(queue => queue.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        model.AvailableQueues = queues
            .Select(queue => new SelectListItem(queue.Name, queue.ItemId, model.QueueIds.Contains(queue.ItemId, StringComparer.Ordinal)))
            .ToList();

        model.AvailableCampaigns = _campaigns is null
            ? []
            : (await _campaigns.GetAllAsync())
                .OrderBy(campaign => campaign.DisplayText, StringComparer.OrdinalIgnoreCase)
                .Select(campaign => new SelectListItem(campaign.DisplayText, campaign.ItemId, model.CampaignIds.Contains(campaign.ItemId, StringComparer.Ordinal)))
                .ToList();

        model.AvailableTeams = (await _teamManager.GetAllAsync())
            .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
            .Select(team => new SelectListItem(team.Name, team.ItemId, model.TeamIds.Contains(team.ItemId, StringComparer.Ordinal)))
            .ToList();

        model.AvailableRoles = (await _roleService.GetRoleNamesAsync())
            .Where(role => !string.Equals(role, OrchardCoreConstants.Roles.Anonymous, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(role, OrchardCoreConstants.Roles.Authenticated, StringComparison.OrdinalIgnoreCase))
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .Select(role => new SelectListItem(role, role, model.RoleNames.Contains(role, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        model.AvailableCandidateSources =
        [
            new(S["Agents signed in to a queue"], MessagingRoutingCandidateSources.QueueSignedIn),
            new(S["Every agent who works a queue"], MessagingRoutingCandidateSources.QueueMembers),
            new(S["Members of teams"], MessagingRoutingCandidateSources.Teams),
            new(S["Users in roles"], MessagingRoutingCandidateSources.Roles),
            new(S["Specific users"], MessagingRoutingCandidateSources.Users),
        ];

        model.AvailableStrategies =
        [
            new(S["Round robin: the agent who got a conversation longest ago"], MessagingRoutingRuleStrategies.RoundRobin),
            new(S["Fewest open conversations"], MessagingRoutingRuleStrategies.LeastOpenConversations),
            new(S["Least busy: open conversations and live calls"], MessagingRoutingRuleStrategies.LeastLoaded),
        ];
    }

    private static List<string> Clean(IEnumerable<string> values)
        => (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
