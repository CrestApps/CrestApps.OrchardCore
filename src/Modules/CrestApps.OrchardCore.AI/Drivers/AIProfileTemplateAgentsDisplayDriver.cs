using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIProfileTemplateAgentsDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly IAIProfileManager _profileManager;

    public AIProfileTemplateAgentsDisplayDriver(IAIProfileManager profileManager)
    {
        _profileManager = profileManager;
    }

    public override async Task<IDisplayResult> EditAsync(AIProfileTemplate template, BuildEditorContext context)
    {
        var agents = await GetAvailableAgentsAsync();

        return Initialize<EditProfileAgentsViewModel>("EditProfileAgents_Edit", model =>
        {
            var metadata = template.As<ProfileTemplateMetadata>();
            var selectedNames = metadata?.AgentNames ?? [];

            model.Agents = agents.Select(agent => new ToolEntry
            {
                ItemId = agent.Name,
                DisplayText = agent.DisplayText ?? agent.Name,
                Description = agent.Description,
                IsSelected = selectedNames.Contains(agent.Name),
            }).OrderBy(entry => entry.DisplayText).ToArray();

        }).Location("Content:8#Capabilities:5")
        .RenderWhen(() => Task.FromResult(template.Source == AITemplateSources.Profile));
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

        var model = new EditProfileAgentsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var selectedAgentNames = model.Agents?.Where(a => a.IsSelected).Select(a => a.ItemId);
        var agents = await GetAvailableAgentsAsync();
        var validAgentNames = agents.Select(a => a.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var metadata = template.As<ProfileTemplateMetadata>();

        if (selectedAgentNames is null || !selectedAgentNames.Any())
        {
            metadata.AgentNames = [];
        }
        else
        {
            metadata.AgentNames = selectedAgentNames
                .Where(name => validAgentNames.Contains(name))
                .ToArray();
        }

        template.Put(metadata);

        return await EditAsync(template, context);
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
