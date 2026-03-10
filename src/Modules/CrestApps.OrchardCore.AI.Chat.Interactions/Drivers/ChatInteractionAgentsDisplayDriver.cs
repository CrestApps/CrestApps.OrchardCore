using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

internal sealed class ChatInteractionAgentsDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly IAIProfileManager _profileManager;

    public ChatInteractionAgentsDisplayDriver(IAIProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public override async Task<IDisplayResult> EditAsync(ChatInteraction interaction, BuildEditorContext context)
    {
        var agents = await GetAvailableAgentsAsync();

        return Initialize<EditChatInteractionAgentsViewModel>("ChatInteractionAgents_Edit", model =>
        {
            model.Agents = agents.Select(agent => new ToolEntry
            {
                ItemId = agent.Name,
                DisplayText = agent.DisplayText ?? agent.Name,
                Description = agent.Description,
                IsSelected = interaction.AgentNames?.Contains(agent.Name) ?? false,
            }).OrderBy(entry => entry.DisplayText).ToArray();

        }).Location("Parameters:5#Capabilities;5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ChatInteraction interaction, UpdateEditorContext context)
    {
        var model = new EditChatInteractionAgentsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var selectedAgentNames = model.Agents?.Where(a => a.IsSelected).Select(a => a.ItemId);
        var agents = await GetAvailableAgentsAsync();
        var validAgentNames = agents.Select(a => a.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (selectedAgentNames is null || !selectedAgentNames.Any())
        {
            interaction.AgentNames = [];
        }
        else
        {
            interaction.AgentNames = selectedAgentNames
                .Where(name => validAgentNames.Contains(name))
                .ToList();
        }

        return await EditAsync(interaction, context);
    }

    private async Task<AIProfile[]> GetAvailableAgentsAsync()
    {
        var agents = await _profileManager.GetAsync(AIProfileType.Agent);

        if (agents is null)
        {
            return [];
        }

        // Exclude always-available agents from user selection.
        return agents
            .Where(a => !string.IsNullOrEmpty(a.Description))
            .Where(a => a.As<AgentMetadata>()?.Availability != AgentAvailability.AlwaysAvailable)
            .ToArray();
    }
}
