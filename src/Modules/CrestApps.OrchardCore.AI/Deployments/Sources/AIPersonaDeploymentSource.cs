using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

/// <summary>
/// Writes the saved AI personas to a deployment plan. Persona files are not exported: they ship with the code.
/// </summary>
internal sealed class AIPersonaDeploymentSource : CatalogDeploymentSourceBase<AIPersonaDeploymentStep, AIPersona>
{
    private readonly INamedCatalog<AIPersona> _personasCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIPersonaDeploymentSource"/> class.
    /// </summary>
    /// <param name="personasCatalog">The catalog of saved personas.</param>
    public AIPersonaDeploymentSource(INamedCatalog<AIPersona> personasCatalog)
        : base("personas")
    {
        _personasCatalog = personasCatalog;
    }

    protected override async Task<IEnumerable<AIPersona>> GetEntriesAsync()
        => await _personasCatalog.GetAllAsync();

    protected override bool IsSelected(AIPersonaDeploymentStep step, AIPersona entry)
        => IsSelectedByKey(step.IncludeAll, step.PersonaIds, entry.ItemId);

    protected override JsonObject Export(AIPersona entry)
        => new()
        {
            [nameof(AIPersona.ItemId)] = entry.ItemId,
            [nameof(AIPersona.Name)] = entry.Name,
            [nameof(AIPersona.DisplayText)] = entry.DisplayText,
            [nameof(AIPersona.Description)] = entry.Description,
            [nameof(AIPersona.Instructions)] = entry.Instructions,
            [nameof(AIPersona.AssistantName)] = entry.AssistantName,
            [nameof(AIPersona.Language)] = entry.Language,
            [nameof(AIPersona.VoiceName)] = entry.VoiceName,
            [nameof(AIPersona.VoiceDeploymentName)] = entry.VoiceDeploymentName,
            [nameof(AIPersona.AvatarUrl)] = entry.AvatarUrl,
            [nameof(AIPersona.CreatedUtc)] = entry.CreatedUtc,
            [nameof(AIPersona.OwnerId)] = entry.OwnerId,
            [nameof(AIPersona.Author)] = entry.Author,
            [nameof(AIPersona.Properties)] = JsonSerializer.SerializeToNode(entry.Properties),
        };
}
