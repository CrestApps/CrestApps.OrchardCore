using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.Profiles;

/// <summary>
/// Creates an AI profile, or changes the general settings of one, through the profile manager the profile editor and the
/// AI API use. It never changes what the profile can use (tools, tool instances, agents, connections), whether it can
/// be deleted, or whether its instructions are locked, and it refuses to change the assistant that is running.
/// </summary>
public sealed class SaveAIProfileTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveAIProfile";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "profileId": {
          "type": "string",
          "description": "The ID of the profile to change, from listAIProfiles. Leave it out to create a profile."
        },
        "name": {
          "type": "string",
          "description": "The technical name of a new profile: letters, digits and dashes, unique on the site. It cannot change after creation, so it is ignored on an update."
        },
        "title": {
          "type": "string",
          "description": "The title people see. Required for a new profile."
        },
        "description": {
          "type": "string",
          "description": "What the profile does. Required for an agent, because other profiles read it to decide when to call the agent."
        },
        "type": {
          "type": "string",
          "enum": ["Chat", "Utility", "TemplatePrompt", "Agent"],
          "description": "The kind of profile. Chat when left out on a new profile."
        },
        "instructions": {
          "type": "string",
          "description": "The system instructions the model follows. Refused when the profile's instructions are locked."
        },
        "welcomeMessage": {
          "type": "string",
          "description": "The first message a chat shows."
        },
        "initialPrompt": {
          "type": "string",
          "description": "The opening message automated conversations, such as outbound SMS or calls, start with."
        },
        "promptTemplate": {
          "type": "string",
          "description": "The Liquid prompt of a TemplatePrompt profile. Required for that type."
        },
        "promptSubject": {
          "type": "string",
          "description": "The subject of a TemplatePrompt profile."
        },
        "chatDeploymentName": {
          "type": "string",
          "description": "The name of the chat model deployment. Leave it out to keep the current one, or the site default on a new profile."
        },
        "utilityDeploymentName": {
          "type": "string",
          "description": "The name of the utility model deployment used for side tasks such as titles."
        },
        "temperature": {
          "type": "number",
          "minimum": 0,
          "maximum": 2,
          "description": "How varied the answers are, from 0 (focused) to 2 (very varied)."
        },
        "topP": {
          "type": "number",
          "minimum": 0,
          "maximum": 1,
          "description": "Nucleus sampling, from 0 to 1."
        },
        "frequencyPenalty": {
          "type": "number",
          "minimum": -2,
          "maximum": 2,
          "description": "Penalty for repeating the same words, from -2 to 2."
        },
        "presencePenalty": {
          "type": "number",
          "minimum": -2,
          "maximum": 2,
          "description": "Penalty for returning to topics already covered, from -2 to 2."
        },
        "maxTokens": {
          "type": "integer",
          "minimum": 1,
          "description": "The most tokens an answer may have."
        },
        "pastMessagesCount": {
          "type": "integer",
          "minimum": 0,
          "description": "How many earlier messages of the chat the model sees."
        },
        "personaName": {
          "type": "string",
          "description": "The technical name of the persona the profile speaks as, from listAIPersonas. An empty text removes the persona."
        },
        "promptTemplateIds": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The IDs or names of the SystemPrompt templates whose instructions come before the profile's own, from listAIProfileTemplates. Replaces the current selection; an empty list removes them all."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a profile or agent, or updates one by id: title, description, type, instructions, welcome message, deployments, model parameters, persona and prompt templates. It does not change tools, agents or connections (use setAIProfileCapabilities) and cannot change the assistant that is running or its agents.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIProfiles))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var profileManager = services.GetRequiredService<IAIProfileManager>();
        arguments.TryGetFirstString("profileId", out var profileId);

        var (changes, changedFields, error) = ReadChanges(arguments);

        if (error is not null)
        {
            return $"Nothing was saved: {error}";
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "promptTemplateIds", out var promptTemplateIds))
        {
            return "Nothing was saved: promptTemplateIds must be a list of template IDs or names.";
        }

        var (selections, templateError) = await ResolvePromptTemplatesAsync(services, promptTemplateIds, cancellationToken);

        if (templateError is not null)
        {
            return $"Nothing was saved: {templateError}";
        }

        if (selections is not null)
        {
            changedFields.Add("promptTemplateIds");
        }

        var personaError = await CheckPersonaAsync(services, changes, cancellationToken);

        if (personaError is not null)
        {
            return $"Nothing was saved: {personaError}";
        }

        return string.IsNullOrWhiteSpace(profileId)
            ? await CreateAsync(services, profileManager, arguments, changes, selections, cancellationToken)
            : await UpdateAsync(services, profileManager, profileId.Trim(), changes, changedFields, selections, cancellationToken);
    }

    private static async Task<string> CreateAsync(
        IServiceProvider services,
        IAIProfileManager profileManager,
        AIFunctionArguments arguments,
        JsonObject changes,
        List<PromptTemplateSelectionEntry> selections,
        CancellationToken cancellationToken)
    {
        arguments.TryGetFirstString("name", out var name);
        name = name?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return "Nothing was saved: a new profile needs a technical name.";
        }

        if (string.IsNullOrWhiteSpace(changes[nameof(AIProfile.DisplayText)]?.GetValue<string>()))
        {
            return "Nothing was saved: a new profile needs a title.";
        }

        if (await profileManager.FindByNameAsync(name, cancellationToken) is not null)
        {
            return $"Nothing was saved: another profile is named '{name}'. Pick another technical name, or update that profile by its ID.";
        }

        changes[nameof(AIProfile.Name)] = name;
        changes.TryAdd(nameof(AIProfile.Type), nameof(AIProfileType.Chat));

        var profile = await profileManager.NewAsync(changes, cancellationToken);

        if (profile is null)
        {
            return "Nothing was saved: the profile manager could not create a new profile.";
        }

        if (selections is not null)
        {
            ApplyPromptTemplates(profile, selections);
        }

        var validation = await profileManager.ValidateAsync(profile, cancellationToken);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: {string.Join(" ", validation.Errors.Select(e => e.ErrorMessage))}";
        }

        var caller = AgentToolAuthorization.GetCaller(services);

        profile.OwnerId ??= caller?.FindFirstValue(ClaimTypes.NameIdentifier);
        profile.Author ??= caller?.Identity?.Name;

        AIProfileToolSupport.SetVersionNote(services, profile, "Created");

        await profileManager.CreateAsync(profile, cancellationToken);

        return AgentToolResults.Json(new
        {
            created = true,
            id = profile.ItemId,
            name = profile.Name,
            title = profile.DisplayText,
            type = profile.Type.ToString(),
            editPath = AIProfileToolSupport.GetProfileEditPath(services, profile.ItemId),
            note = "The profile has no tools yet. Use setAIProfileCapabilities to give it tools, tool instances or agents.",
        });
    }

    private static async Task<string> UpdateAsync(
        IServiceProvider services,
        IAIProfileManager profileManager,
        string profileId,
        JsonObject changes,
        List<string> changedFields,
        List<PromptTemplateSelectionEntry> selections,
        CancellationToken cancellationToken)
    {
        var profile = await profileManager.FindByIdAsync(profileId, cancellationToken);

        if (profile is null)
        {
            return $"Nothing was saved: no AI profile has the ID '{profileId}'. Use listAIProfiles to find the ID.";
        }

        var title = AIConfigurationGuard.GetTitle(profile);
        var editPath = AIProfileToolSupport.GetProfileEditPath(services, profile.ItemId);
        var protectedConfiguration = await AIConfigurationGuard.GetProtectedAsync(services, cancellationToken);

        if (protectedConfiguration.IsProtected(profile))
        {
            return AIProfileToolSupport.RunningAssistantRefusal($"the profile '{title}'", editPath);
        }

        if (changedFields.Count == 0)
        {
            return $"Nothing was saved: no setting to change was given for the profile '{title}'.";
        }

        if (changes.ContainsKey(nameof(AIProfileMetadata.SystemMessage)) && profile.GetSettings<AIProfileSettings>().LockSystemMessage)
        {
            return $"Nothing was saved: the instructions of the profile '{title}' are locked. A person can unlock and change them on the profile's page: {editPath}";
        }

        if (changes[nameof(AIProfile.DisplayText)] is JsonValue displayText && string.IsNullOrWhiteSpace(displayText.GetValue<string>()))
        {
            return "Nothing was saved: the title cannot be empty.";
        }

        // Validate a merged copy first: the manager saves as it updates.
        var current = JsonSerializer.SerializeToNode(profile) as JsonObject ?? [];
        var copy = await profileManager.NewAsync(AISourceCatalogApi.Merge(current, (JsonObject)changes.DeepClone()), cancellationToken);
        copy.ItemId = profile.ItemId;
        copy.Name = profile.Name;

        if (selections is not null)
        {
            ApplyPromptTemplates(copy, selections);
        }

        var validation = await profileManager.ValidateAsync(copy, cancellationToken);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: {string.Join(" ", validation.Errors.Select(e => e.ErrorMessage))}";
        }

        // A change of type can turn a stored always-available agent setting back on.
        var refusal = await AIConfigurationGuard.CheckCapabilityChangeAsync(services, profile, copy, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        if (selections is not null)
        {
            ApplyPromptTemplates(profile, selections);
        }

        AIProfileToolSupport.SetVersionNote(services, profile, "Changed");

        await profileManager.UpdateAsync(profile, changes, cancellationToken);

        return AgentToolResults.Json(new
        {
            updated = true,
            id = profile.ItemId,
            name = profile.Name,
            title = profile.DisplayText,
            type = profile.Type.ToString(),
            changed = changedFields,
            editPath,
            versionsPath = AIProfileToolSupport.GetProfileVersionsPath(services, profile.ItemId),
        });
    }

    private static (JsonObject Changes, List<string> ChangedFields, string Error) ReadChanges(AIFunctionArguments arguments)
    {
        var changes = new JsonObject();
        var changed = new List<string>();

        void Text(string argument, string key)
        {
            if (arguments.TryGetFirstString(argument, out var value) && value is not null)
            {
                changes[key] = value.Trim();
                changed.Add(argument);
            }
        }

        void Number(string argument, string key, bool integer)
        {
            if (!arguments.TryGetFirst(argument, out _))
            {
                return;
            }

            if (integer && arguments.TryGetFirst<int>(argument, out var whole))
            {
                changes[key] = whole;
                changed.Add(argument);
            }
            else if (!integer && arguments.TryGetFirst<float>(argument, out var real))
            {
                changes[key] = real;
                changed.Add(argument);
            }
        }

        Text("title", nameof(AIProfile.DisplayText));
        Text("description", nameof(AIProfile.Description));
        Text("instructions", nameof(AIProfileMetadata.SystemMessage));
        Text("welcomeMessage", nameof(AIProfile.WelcomeMessage));
        Text("initialPrompt", nameof(AIProfileMetadata.InitialPrompt));
        Text("promptTemplate", nameof(AIProfile.PromptTemplate));
        Text("promptSubject", nameof(AIProfile.PromptSubject));
        Text("chatDeploymentName", nameof(AIProfile.ChatDeploymentName));
        Text("utilityDeploymentName", nameof(AIProfile.UtilityDeploymentName));
        Text("personaName", nameof(AIPersonaMetadata.PersonaId));
        Number("temperature", nameof(AIProfileMetadata.Temperature), integer: false);
        Number("topP", nameof(AIProfileMetadata.TopP), integer: false);
        Number("frequencyPenalty", nameof(AIProfileMetadata.FrequencyPenalty), integer: false);
        Number("presencePenalty", nameof(AIProfileMetadata.PresencePenalty), integer: false);
        Number("maxTokens", nameof(AIProfileMetadata.MaxTokens), integer: true);
        Number("pastMessagesCount", nameof(AIProfileMetadata.PastMessagesCount), integer: true);

        if (arguments.TryGetFirstString("type", out var type) && !string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<AIProfileType>(type.Trim(), ignoreCase: true, out var profileType) || !Enum.IsDefined(profileType))
            {
                return (null, null, $"'{type}' is not a profile type. Use Chat, Utility, TemplatePrompt or Agent.");
            }

            changes[nameof(AIProfile.Type)] = profileType.ToString();
            changed.Add("type");
        }

        return (changes, changed, null);
    }

    private static async Task<string> CheckPersonaAsync(IServiceProvider services, JsonObject changes, CancellationToken cancellationToken)
    {
        var personaName = changes[nameof(AIPersonaMetadata.PersonaId)]?.GetValue<string>();

        if (string.IsNullOrEmpty(personaName))
        {
            return null;
        }

        var personaManager = services.GetService<IAIPersonaManager>();

        if (personaManager is null)
        {
            return "personas are not enabled on this site, so no persona can be linked.";
        }

        return await personaManager.FindByNameAsync(personaName, cancellationToken) is null
            ? $"no persona is named '{personaName}'. Use listAIPersonas to find its technical name."
            : null;
    }

    private static async Task<(List<PromptTemplateSelectionEntry> Selections, string Error)> ResolvePromptTemplatesAsync(
        IServiceProvider services,
        List<string> templateIds,
        CancellationToken cancellationToken)
    {
        if (templateIds is null)
        {
            return (null, null);
        }

        var templateManager = services.GetRequiredService<IAIProfileTemplateManager>();
        var selections = new List<PromptTemplateSelectionEntry>();

        foreach (var templateId in templateIds)
        {
            var template = await templateManager.FindByIdAsync(templateId, cancellationToken)
                ?? await templateManager.FindByNameAsync(templateId, cancellationToken);

            if (template is null || !string.Equals(template.Source, AITemplateSources.SystemPrompt, StringComparison.OrdinalIgnoreCase))
            {
                return (null, $"no SystemPrompt template has the ID or name '{templateId}'. Use listAIProfileTemplates with source SystemPrompt to find it.");
            }

            // The template service finds a template by its name.
            selections.Add(new PromptTemplateSelectionEntry { TemplateId = template.Name });
        }

        return (selections, null);
    }

    private static void ApplyPromptTemplates(AIProfile profile, List<PromptTemplateSelectionEntry> selections)
    {
        var metadata = profile.GetOrCreate<PromptTemplateMetadata>();
        metadata.SetSelections(selections);
        profile.Put(metadata);
    }
}
