using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

/// <summary>
/// Writes the saved AI personas to a deployment plan. Persona files are not exported: they ship with the code.
/// </summary>
internal sealed class AIPersonaDeploymentSource : DeploymentSourceBase<AIPersonaDeploymentStep>
{
    private readonly INamedCatalog<AIPersona> _personasCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIPersonaDeploymentSource"/> class.
    /// </summary>
    /// <param name="personasCatalog">The catalog of saved personas.</param>
    public AIPersonaDeploymentSource(INamedCatalog<AIPersona> personasCatalog)
    {
        _personasCatalog = personasCatalog;
    }

    protected override async Task ProcessAsync(AIPersonaDeploymentStep step, DeploymentPlanResult result)
    {
        var personaIds = step.IncludeAll
            ? []
            : step.PersonaIds ?? [];

        var personas = new JsonArray();

        foreach (var persona in await _personasCatalog.GetAllAsync())
        {
            if (personaIds.Length > 0 && !personaIds.Contains(persona.ItemId))
            {
                continue;
            }

            personas.Add(new JsonObject
            {
                [nameof(AIPersona.ItemId)] = persona.ItemId,
                [nameof(AIPersona.Name)] = persona.Name,
                [nameof(AIPersona.DisplayText)] = persona.DisplayText,
                [nameof(AIPersona.Description)] = persona.Description,
                [nameof(AIPersona.Instructions)] = persona.Instructions,
                [nameof(AIPersona.AssistantName)] = persona.AssistantName,
                [nameof(AIPersona.Language)] = persona.Language,
                [nameof(AIPersona.VoiceName)] = persona.VoiceName,
                [nameof(AIPersona.VoiceDeploymentName)] = persona.VoiceDeploymentName,
                [nameof(AIPersona.AvatarUrl)] = persona.AvatarUrl,
                [nameof(AIPersona.CreatedUtc)] = persona.CreatedUtc,
                [nameof(AIPersona.OwnerId)] = persona.OwnerId,
                [nameof(AIPersona.Author)] = persona.Author,
                [nameof(AIPersona.Properties)] = JsonSerializer.SerializeToNode(persona.Properties),
            });
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["personas"] = personas,
        });
    }
}
