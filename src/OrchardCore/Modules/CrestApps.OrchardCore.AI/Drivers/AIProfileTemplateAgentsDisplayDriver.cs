using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.AI.Profiles;
using CrestApps.OrchardCore.AI.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using CrestApps;

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
        var allAgents = await _profileManager.GetAsync(AIProfileType.Agent) ?? [];

        var alwaysAvailableCount = allAgents
            .Count(a => a.As<AgentMetadata>()?.Availability == AgentAvailability.AlwaysAvailable);

        var onDemandAgents = allAgents
            .Where(a => !string.IsNullOrEmpty(a.Description))
            .Where(a => a.As<AgentMetadata>()?.Availability != AgentAvailability.AlwaysAvailable);

        return Initialize<EditProfileAgentsViewModel>("EditProfileAgents_Edit", model =>
        {
            var metadata = template.As<ProfileTemplateMetadata>();
            var selectedNames = metadata?.AgentNames ?? [];

            model.AlwaysAvailableAgentCount = alwaysAvailableCount;
            model.Agents = onDemandAgents.Select(agent => new ToolEntry
            {
                ItemId = agent.Name,
                DisplayText = agent.DisplayText ?? agent.Name,
                Description = agent.Description,
                IsSelected = selectedNames.Contains(agent.Name),
            }).OrderBy(entry => entry.DisplayText).ToArray();

        }).Location("Content:5#Capabilities;8")
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

        var allAgents = await _profileManager.GetAsync(AIProfileType.Agent) ?? [];

        var validAgentNames = allAgents
            .Where(a => !string.IsNullOrEmpty(a.Description))
            .Where(a => a.As<AgentMetadata>()?.Availability != AgentAvailability.AlwaysAvailable)
            .Select(a => a.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
}
