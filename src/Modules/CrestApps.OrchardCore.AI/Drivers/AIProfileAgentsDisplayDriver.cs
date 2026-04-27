using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIProfileAgentsDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IAIProfileManager _profileManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileAgentsDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileManager">The profile manager.</param>
    public AIProfileAgentsDisplayDriver(IAIProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public override async Task<IDisplayResult> EditAsync(AIProfile profile, BuildEditorContext context)
    {
        return Initialize<EditProfileAgentsViewModel>("EditProfileAgents_Edit", async model =>
        {
            var allAgents = await _profileManager.GetAsync(AIProfileType.Agent) ?? [];

            var alwaysAvailableCount = allAgents
                .Count(a => !string.Equals(a.Name, profile.Name, StringComparison.OrdinalIgnoreCase)
                    && a.GetOrCreate<AgentMetadata>()?.Availability == AgentAvailability.AlwaysAvailable);

            var onDemandAgents = allAgents
                .Where(a => !string.Equals(a.Name, profile.Name, StringComparison.OrdinalIgnoreCase))
                .Where(a => a.GetOrCreate<AgentMetadata>()?.Availability != AgentAvailability.AlwaysAvailable);

            var selectedNames = GetSelectedAgentNames(profile);

            model.AlwaysAvailableAgentCount = alwaysAvailableCount;
            model.Agents = onDemandAgents.Select(agent => new ToolEntry
            {
                ItemId = agent.Name,
                DisplayText = agent.DisplayText ?? agent.Name,
                Description = agent.Description,
                IsSelected = selectedNames?.Contains(agent.Name) ?? false,
            }).OrderBy(entry => entry.DisplayText).ToArray();
        }).Location("Content:5#Capabilities;8");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var model = new EditProfileAgentsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var selectedAgentNames = model.Agents?.Where(a => a.IsSelected).Select(a => a.ItemId);

        var allAgents = await _profileManager.GetAsync(AIProfileType.Agent) ?? [];

        var validAgentNames = allAgents
            .Where(a => !string.Equals(a.Name, profile.Name, StringComparison.OrdinalIgnoreCase))
            .Where(a => a.GetOrCreate<AgentMetadata>()?.Availability != AgentAvailability.AlwaysAvailable)
            .Select(a => a.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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

    private static string[] GetSelectedAgentNames(AIProfile profile)
    {
        var metadata = profile.GetOrCreate<AgentInvocationMetadata>();

        return metadata?.Names;
    }
}
