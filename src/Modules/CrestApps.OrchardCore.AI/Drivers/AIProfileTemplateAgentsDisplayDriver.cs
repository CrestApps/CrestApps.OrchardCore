using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIProfileTemplateAgentsDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly IAIProfileManager _profileManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateAgentsDisplayDriver"/> class.
    /// </summary>
    /// <param name="profileManager">The profile manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileTemplateAgentsDisplayDriver(
        IAIProfileManager profileManager,
        IStringLocalizer<AIProfileTemplateAgentsDisplayDriver> stringLocalizer)
    {
        _profileManager = profileManager;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(AIProfileTemplate template, BuildEditorContext context)
    {
        var allAgents = await _profileManager.GetAsync(AIProfileType.Agent) ?? [];

        var alwaysAvailableCount = allAgents
            .Count(a => a.GetOrCreate<AgentMetadata>()?.Availability == AgentAvailability.AlwaysAvailable);

        var onDemandAgents = allAgents
            .Where(a => a.GetOrCreate<AgentMetadata>()?.Availability != AgentAvailability.AlwaysAvailable);

        return Initialize<EditProfileAgentsViewModel>("EditProfileAgents_Edit", model =>
        {
            var metadata = template.GetOrCreate<ProfileTemplateMetadata>();
            var selectedNames = metadata?.AgentNames ?? [];

            model.AlwaysAvailableAgentCount = alwaysAvailableCount;
            model.Agents = onDemandAgents.Select(agent => new ToolEntry
            {
                ItemId = agent.Name,
                DisplayText = agent.DisplayText ?? agent.Name,
                Description = agent.Description,
                IsSelected = selectedNames.Contains(agent.Name),
            }).OrderBy(entry => entry.DisplayText).ToArray();
        }).Location(l => l.Zone("Content", "5").Tab(S["Capabilities"], "8").Card(S["Agents & Tools"], "10"))
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
            .Where(a => a.GetOrCreate<AgentMetadata>()?.Availability != AgentAvailability.AlwaysAvailable)
            .Select(a => a.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var metadata = template.GetOrCreate<ProfileTemplateMetadata>();

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
