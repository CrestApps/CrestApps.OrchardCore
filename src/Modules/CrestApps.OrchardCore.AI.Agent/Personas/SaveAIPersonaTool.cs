using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.Personas;

/// <summary>
/// Creates an AI persona or updates one, through the persona manager the Personas admin page uses and with the same
/// rules as its editor: a technical name that never changes, required instructions, and a persona file that is saved
/// as a stored copy with the same name. A persona the running assistant speaks as cannot be changed.
/// </summary>
public sealed class SaveAIPersonaTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveAIPersona";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "personaId": {
          "type": "string",
          "description": "The ID of the persona to change, from listAIPersonas. Leave it out to create a persona."
        },
        "name": {
          "type": "string",
          "description": "The technical name of a new persona, which profiles link to. It cannot change after creation, so it is ignored on an update."
        },
        "title": {
          "type": "string",
          "description": "The title people see. The technical name is used when it is left empty."
        },
        "description": {
          "type": "string",
          "description": "What the persona is for."
        },
        "instructions": {
          "type": "string",
          "description": "How the assistant speaks and behaves as this persona, in Liquid. Required for a new persona."
        },
        "assistantName": {
          "type": "string",
          "description": "The name the assistant gives itself."
        },
        "language": {
          "type": "string",
          "description": "The language the persona speaks, for example en-GB."
        },
        "voiceName": {
          "type": "string",
          "description": "The voice of the persona, as its voice deployment's provider names it. An empty text removes the voice."
        },
        "voiceDeploymentName": {
          "type": "string",
          "description": "The realtime or text-to-speech deployment the voice belongs to."
        },
        "avatarUrl": {
          "type": "string",
          "description": "The address of the persona's picture."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a persona or updates one by id. The change reaches every linked profile from its next message, so a persona linked to the running assistant cannot be changed. The technical name cannot change.";

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

        var personaManager = services.GetRequiredService<IAIPersonaManager>();
        var catalog = services.GetService<INamedCatalog<AIPersona>>();
        arguments.TryGetFirstString("personaId", out var personaId);

        AIPersona target;
        var isNew = true;

        if (string.IsNullOrWhiteSpace(personaId))
        {
            arguments.TryGetFirstString("name", out var name);
            name = Normalize(name);

            if (name is null)
            {
                return "Nothing was saved: the technical name is required.";
            }

            if (await personaManager.FindByNameAsync(name, cancellationToken) is not null)
            {
                return $"Nothing was saved: a persona is already named '{name}'. Update it by its ID instead.";
            }

            target = await personaManager.NewAsync(name, cancellationToken: cancellationToken);
        }
        else
        {
            var persona = await personaManager.FindByIdAsync(personaId.Trim(), cancellationToken);

            if (persona is null)
            {
                return $"Nothing was saved: no persona has the ID '{personaId}'. Use listAIPersonas to find it.";
            }

            if ((await AIConfigurationGuard.GetProtectedAsync(services, cancellationToken)).IsProtectedPersona(persona.Name))
            {
                return AIProfileToolSupport.RunningAssistantRefusal(
                    $"the persona '{PersonaToolSupport.GetTitle(persona)}'",
                    AIProfileToolSupport.GetPersonaEditPath(services, persona.ItemId));
            }

            var saved = catalog is null ? null : await catalog.FindByIdAsync(persona.ItemId, cancellationToken);

            if (saved is not null)
            {
                target = saved;
                isNew = false;
            }
            else
            {
                // A persona file is never changed in place: its saved copy keeps the name, so every link follows it.
                target = await personaManager.NewAsync(persona.Name, cancellationToken: cancellationToken);
                CopyFrom(target, persona);
            }
        }

        if (target is null)
        {
            return "Nothing was saved: the persona manager could not create a persona.";
        }

        Apply(arguments, target);

        if (string.IsNullOrWhiteSpace(target.Instructions))
        {
            return "Nothing was saved: the persona instructions are required.";
        }

        var validation = await personaManager.ValidateAsync(target, cancellationToken);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: {string.Join(" ", validation.Errors.Select(e => e.ErrorMessage))}";
        }

        if (isNew)
        {
            var caller = AgentToolAuthorization.GetCaller(services);
            target.OwnerId ??= caller?.FindFirstValue(ClaimTypes.NameIdentifier);
            target.Author ??= caller?.Identity?.Name;

            await personaManager.CreateAsync(target, cancellationToken);
        }
        else
        {
            await personaManager.UpdateAsync(target, cancellationToken: cancellationToken);
        }

        var linked = await PersonaToolSupport.GetLinkedProfilesAsync(services.GetRequiredService<IAIProfileManager>(), cancellationToken);

        return AgentToolResults.Json(new
        {
            created = isNew,
            updated = !isNew,
            id = target.ItemId,
            name = target.Name,
            title = target.DisplayText,
            linkedProfileCount = linked.TryGetValue(target.Name ?? string.Empty, out var titles) ? titles.Count : 0,
            editPath = AIProfileToolSupport.GetPersonaEditPath(services, target.ItemId),
        });
    }

    private static void CopyFrom(AIPersona target, AIPersona source)
    {
        target.DisplayText = source.DisplayText;
        target.Description = source.Description;
        target.Instructions = source.Instructions;
        target.AssistantName = source.AssistantName;
        target.Language = source.Language;
        target.VoiceName = source.VoiceName;
        target.VoiceDeploymentName = source.VoiceDeploymentName;
        target.AvatarUrl = source.AvatarUrl;
    }

    private static void Apply(AIFunctionArguments arguments, AIPersona persona)
    {
        if (arguments.TryGetFirstString("title", out var title) && title is not null)
        {
            persona.DisplayText = Normalize(title);
        }

        persona.DisplayText ??= persona.Name;

        if (arguments.TryGetFirstString("description", out var description) && description is not null)
        {
            persona.Description = Normalize(description);
        }

        if (arguments.TryGetFirstString("instructions", out var instructions) && instructions is not null)
        {
            persona.Instructions = instructions.Trim();
        }

        if (arguments.TryGetFirstString("assistantName", out var assistantName) && assistantName is not null)
        {
            persona.AssistantName = Normalize(assistantName);
        }

        if (arguments.TryGetFirstString("language", out var language) && language is not null)
        {
            persona.Language = Normalize(language);
        }

        if (arguments.TryGetFirstString("voiceName", out var voiceName) && voiceName is not null)
        {
            persona.VoiceName = Normalize(voiceName);
        }

        if (arguments.TryGetFirstString("voiceDeploymentName", out var voiceDeploymentName) && voiceDeploymentName is not null)
        {
            persona.VoiceDeploymentName = Normalize(voiceDeploymentName);
        }

        // The deployment only qualifies a voice; without one there is nothing to qualify.
        if (persona.VoiceName is null)
        {
            persona.VoiceDeploymentName = null;
        }

        if (arguments.TryGetFirstString("avatarUrl", out var avatarUrl) && avatarUrl is not null)
        {
            persona.AvatarUrl = Normalize(avatarUrl);
        }
    }

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
