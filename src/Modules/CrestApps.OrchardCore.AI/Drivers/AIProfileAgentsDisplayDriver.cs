using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIProfileAgentsDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IAIProfileManager _profileManager;

    public AIProfileAgentsDisplayDriver(IAIProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public override async Task<IDisplayResult> EditAsync(AIProfile profile, BuildEditorContext context)
    {
        var agents = await GetAvailableAgentsAsync(profile.Name);

        if (agents.Length == 0)
        {
            return null;
        }

        return Initialize<EditProfileAgentsViewModel>("EditProfileAgents_Edit", model =>
        {
            var selectedNames = GetSelectedAgentNames(profile);

            model.Agents = agents.Select(agent => new ToolEntry
            {
                ItemId = agent.Name,
                DisplayText = agent.DisplayText ?? agent.Name,
                Description = agent.Description,
                IsSelected = selectedNames?.Contains(agent.Name) ?? false,
            }).OrderBy(entry => entry.DisplayText).ToArray();

        }).Location("Content:8#Capabilities:7");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditProfileAgentsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var selectedAgentNames = model.Agents?.Where(a => a.IsSelected).Select(a => a.ItemId);
        var agents = await GetAvailableAgentsAsync(profile.Name);
        var validAgentNames = agents.Select(a => a.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var metadata = new AgentInvocationMetadata();

        if (selectedAgentNames is null || !selectedAgentNames.Any())
        {
            metadata.Names = [];
        }
        else
        {
            metadata.Names = selectedAgentNames
                .Where(name => validAgentNames.Contains(name))
                .ToArray();
        }

        profile.Put(metadata);

        return await EditAsync(profile, context);
    }

    private async Task<AIProfile[]> GetAvailableAgentsAsync(string excludeProfileName)
    {
        var agents = await _profileManager.GetAsync(AIProfileType.Agent);

        if (agents is null)
        {
            return [];
        }

        // Exclude the current profile (prevent self-referencing) and system agents.
        return agents
            .Where(a => !string.Equals(a.Name, excludeProfileName, StringComparison.OrdinalIgnoreCase))
            .Where(a => !string.IsNullOrEmpty(a.Description))
            .Where(a => a.As<AgentMetadata>()?.IsSystemAgent != true)
            .ToArray();
    }

    private static string[] GetSelectedAgentNames(AIProfile profile)
    {
        var metadata = profile.As<AgentInvocationMetadata>();

        return metadata?.Names;
    }
}
