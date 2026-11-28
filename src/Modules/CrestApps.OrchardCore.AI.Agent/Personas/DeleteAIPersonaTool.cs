using System.Text.Json;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.Personas;

/// <summary>
/// Deletes saved AI personas, as the Delete action and bulk Delete of the Personas page do, and reports how many
/// profiles now run without a persona. Persona files and the persona the running assistant speaks as are skipped.
/// </summary>
public sealed class DeleteAIPersonaTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteAIPersona";

    private const int MaxPersonas = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "personaIds": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "maxItems": 50,
          "description": "The IDs of the saved personas to delete, from listAIPersonas."
        }
      },
      "required": ["personaIds"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes personas by id. Profiles that linked to a deleted persona then run without a persona; the result says how many. Persona files and the running assistant's persona are skipped.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIPersonas))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "personaIds", out var personaIds) || personaIds is not { Count: > 0 })
        {
            return "Nothing was deleted: give the IDs of the personas to delete in personaIds.";
        }

        if (personaIds.Count > MaxPersonas)
        {
            return $"Nothing was deleted: at most {MaxPersonas} personas can be deleted at once.";
        }

        var personaManager = services.GetRequiredService<IAIPersonaManager>();
        var catalog = services.GetService<INamedCatalog<AIPersona>>();
        var protectedConfiguration = await AIConfigurationGuard.GetProtectedAsync(services, cancellationToken);
        var linked = await PersonaToolSupport.GetLinkedProfilesAsync(services.GetRequiredService<IAIProfileManager>(), cancellationToken);
        var deleted = new List<object>();
        var skipped = new List<object>();

        foreach (var personaId in personaIds)
        {
            var persona = catalog is null ? null : await catalog.FindByIdAsync(personaId, cancellationToken);

            if (persona is null)
            {
                skipped.Add(new
                {
                    id = personaId,
                    reason = await personaManager.FindByIdAsync(personaId, cancellationToken) is null
                        ? "No persona has this ID."
                        : "The persona comes from a file and cannot be deleted.",
                });

                continue;
            }

            var title = PersonaToolSupport.GetTitle(persona);

            if (protectedConfiguration.IsProtectedPersona(persona.Name))
            {
                skipped.Add(new { id = persona.ItemId, title, reason = AIProfileToolSupport.RunningAssistantRefusal($"the persona '{title}'", AIProfileToolSupport.GetPersonaEditPath(services, persona.ItemId)) });

                continue;
            }

            if (!await personaManager.DeleteAsync(persona, cancellationToken))
            {
                skipped.Add(new { id = persona.ItemId, title, reason = "The persona manager did not delete it." });

                continue;
            }

            // A persona file with the same name takes the links over, so those profiles keep a persona.
            var takenOver = await personaManager.FindByNameAsync(persona.Name, cancellationToken) is not null;

            deleted.Add(new
            {
                id = persona.ItemId,
                name = persona.Name,
                title,
                profilesNowWithoutPersona = takenOver ? 0 : linked.TryGetValue(persona.Name ?? string.Empty, out var titles) ? titles.Count : 0,
                linksTakenOverByPersonaFile = takenOver,
            });
        }

        return AgentToolResults.Json(new
        {
            deleted,
            skipped,
        });
    }
}
