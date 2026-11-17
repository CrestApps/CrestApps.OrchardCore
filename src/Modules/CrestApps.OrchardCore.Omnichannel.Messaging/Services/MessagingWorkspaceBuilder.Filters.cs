using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

// The person, queue and campaign filters of the customer list: what each can be narrowed to, by name.
public sealed partial class MessagingWorkspaceBuilder
{
    // The people, queues and campaigns the list can be narrowed to, each by name. A filter already applied keeps its
    // option even when it is no longer offered, such as a disabled queue, so the page can still say what it shows.
    private async Task PopulateFilterOptionsAsync(InboxViewModel viewModel, MessagingInboxScope scope, CancellationToken cancellationToken)
    {
        if (viewModel.CanFilterByAgent)
        {
            var agents = new List<InboxFilterOption>();

            foreach (var agent in await _agentProfileManager.GetAllAsync(cancellationToken))
            {
                if (string.IsNullOrEmpty(agent?.ItemId))
                {
                    continue;
                }

                var name = await _agentNames.GetDisplayNameAsync(agent.ItemId, cancellationToken);

                if (!string.IsNullOrWhiteSpace(name))
                {
                    agents.Add(new InboxFilterOption(agent.ItemId, name));
                }
            }

            viewModel.Agents = await KeepSelectedAsync(agents, viewModel.AgentFilter, id => _agentNames.GetDisplayNameAsync(id, cancellationToken));
        }

        if (_queueManager is not null)
        {
            var queues = (await _queueManager.GetEnabledAsync(cancellationToken))
                .Where(queue => queue is not null &&
                    !string.IsNullOrWhiteSpace(queue.Name) &&
                    !ContactCenterConstants.IsDirectRoutingQueue(queue.ItemId) &&
                    (scope.IncludeAll || scope.QueueIds.Contains(queue.ItemId, StringComparer.OrdinalIgnoreCase)))
                .Select(queue => new InboxFilterOption(queue.ItemId, queue.Name))
                .ToList();

            viewModel.Queues = await KeepSelectedAsync(queues, viewModel.QueueFilter, async id => (await _queueManager.FindByIdAsync(id, cancellationToken))?.Name);
        }

        if (_campaignCatalog is not null)
        {
            // Only campaigns that run on a messaging channel can hand a conversation off to the workspace.
            var campaigns = (await _campaignCatalog.GetAllAsync(cancellationToken))
                .Where(campaign => campaign is not null &&
                    !string.IsNullOrWhiteSpace(campaign.DisplayText) &&
                    _channelResolver.Get(campaign.Channel) is not null)
                .Select(campaign => new InboxFilterOption(campaign.ItemId, campaign.DisplayText))
                .ToList();

            viewModel.Campaigns = await KeepSelectedAsync(campaigns, viewModel.CampaignFilter, async id => (await _campaignCatalog.FindByIdAsync(id, cancellationToken))?.DisplayText);
        }
    }

    private static async Task<IReadOnlyList<InboxFilterOption>> KeepSelectedAsync(List<InboxFilterOption> options, string selected, Func<string, Task<string>> getName)
    {
        if (!string.IsNullOrEmpty(selected) && !options.Any(option => string.Equals(option.Value, selected, StringComparison.OrdinalIgnoreCase)))
        {
            options.Add(new InboxFilterOption(selected, await getName(selected) ?? selected));
        }

        return options
            .OrderBy(option => option.Text, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
