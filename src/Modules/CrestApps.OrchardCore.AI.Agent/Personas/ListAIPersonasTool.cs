using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.Personas;

/// <summary>
/// Lists the AI personas, the saved ones and the persona files, as the Personas admin page does, with the profiles that
/// link to each. Given a persona's name it also returns that persona's instructions.
/// </summary>
public sealed class ListAIPersonasTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listAIPersonas";

    private const int MaxLinkedTitles = 20;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "The technical name of one persona. When given, only that persona is returned, with its full instructions."
        },
        "search": {
          "type": "string",
          "description": "Only list personas whose name or title contains this text."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many personas a page holds, 20 when left out and at most 50."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists personas with name, title, assistant name, language, voice and the profiles that link to each. Pass name to also get that persona's full instructions.";

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

        arguments.TryGetFirstString("name", out var name);
        arguments.TryGetFirstString("search", out var search);
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        var personaManager = services.GetRequiredService<IAIPersonaManager>();
        var catalog = services.GetService<INamedCatalog<AIPersona>>();
        var storedIds = catalog is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await catalog.GetAllAsync(cancellationToken)).Select(persona => persona.ItemId).Where(id => id is not null).ToHashSet(StringComparer.Ordinal);
        var linked = await PersonaToolSupport.GetLinkedProfilesAsync(services.GetRequiredService<IAIProfileManager>(), cancellationToken);
        var single = !string.IsNullOrWhiteSpace(name);

        var personas = (await personaManager.GetAllAsync(cancellationToken))
            .Where(persona => !single || string.Equals(persona.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(persona => string.IsNullOrWhiteSpace(search) ||
                persona.Name?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) == true ||
                persona.DisplayText?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(persona => persona.DisplayText ?? persona.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (single && personas.Count == 0)
        {
            return $"No persona is named '{name}'. Use listAIPersonas without a name to see them all.";
        }

        var items = personas
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(persona =>
            {
                linked.TryGetValue(persona.Name ?? string.Empty, out var titles);

                return new
                {
                    id = persona.ItemId,
                    name = persona.Name,
                    title = persona.DisplayText,
                    description = persona.Description,
                    assistantName = persona.AssistantName,
                    language = persona.Language,
                    voiceName = persona.VoiceName,
                    voiceDeploymentName = persona.VoiceDeploymentName,
                    isFromFile = string.IsNullOrEmpty(persona.ItemId) || !storedIds.Contains(persona.ItemId),
                    linkedProfileCount = titles?.Count ?? 0,
                    linkedProfiles = titles?.Take(MaxLinkedTitles).ToArray() ?? [],
                    instructions = single ? persona.Instructions : null,
                };
            })
            .ToList();

        return AgentToolResults.Json(new
        {
            items,
            total = personas.Count,
            page,
            pageSize,
            truncated = (page * pageSize) < personas.Count,
        });
    }
}
