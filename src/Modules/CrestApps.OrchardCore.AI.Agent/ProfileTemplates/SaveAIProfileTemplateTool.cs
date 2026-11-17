using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Agent.ProfileTemplates;

/// <summary>
/// Creates or updates a stored AI profile template of source Profile or SystemPrompt, including a speaking style or a
/// guardrail topic, with the fields the template editor has. Shipped templates and the templates the running assistant
/// selects cannot be changed. The tools and agents of a Profile template are set on its admin page.
/// </summary>
public sealed class SaveAIProfileTemplateTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveAIProfileTemplate";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "templateId": {
          "type": "string",
          "description": "The ID of a stored template to change, from listAIProfileTemplates. Leave it out to create one."
        },
        "source": {
          "type": "string",
          "enum": ["Profile", "SystemPrompt"],
          "description": "The source of a new template: Profile (a whole profile setup) or SystemPrompt (a block of instructions profiles select). Ignored on an update."
        },
        "name": {
          "type": "string",
          "description": "The technical name of a new template, unique on the site. It cannot change after creation."
        },
        "title": {
          "type": "string",
          "description": "The title people see. Required for a new template."
        },
        "description": {
          "type": "string",
          "description": "What the template is for."
        },
        "category": {
          "type": "string",
          "description": "The category. Use 'Speaking style' for a speaking style and 'Guardrail topic' for a guardrail topic."
        },
        "isListable": {
          "type": "boolean",
          "description": "Whether the template is offered in pickers. A new template with a category is not listed unless this says so."
        },
        "instructions": {
          "type": "string",
          "description": "The instructions: the block a SystemPrompt template adds, or the system instructions a Profile template gives a profile."
        },
        "profileType": {
          "type": "string",
          "enum": ["Chat", "Utility", "TemplatePrompt", "Agent"],
          "description": "Profile templates only: the type of profile it creates. Required for a new Profile template."
        },
        "profileDescription": {
          "type": "string",
          "description": "Profile templates only: the description the created profile gets, which an agent needs."
        },
        "welcomeMessage": {
          "type": "string",
          "description": "Profile templates only: the welcome message the created profile gets."
        },
        "chatDeploymentName": {
          "type": "string",
          "description": "Profile templates only: the chat deployment the created profile uses."
        },
        "utilityDeploymentName": {
          "type": "string",
          "description": "Profile templates only: the utility deployment the created profile uses."
        },
        "temperature": {
          "type": "number",
          "minimum": 0,
          "maximum": 2,
          "description": "Profile templates only: the temperature of the created profile."
        },
        "topP": {
          "type": "number",
          "minimum": 0,
          "maximum": 1,
          "description": "Profile templates only: the nucleus sampling of the created profile."
        },
        "maxOutputTokens": {
          "type": "integer",
          "minimum": 1,
          "description": "Profile templates only: the most tokens an answer of the created profile may have."
        },
        "pastMessagesCount": {
          "type": "integer",
          "minimum": 0,
          "description": "Profile templates only: how many earlier messages the created profile sees."
        }
      },
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates a stored template of source Profile or SystemPrompt, including a speaking style or guardrail topic (set category). A SystemPrompt template changes every profile that selects it from the next message, so it cannot be one the running assistant uses. Shipped templates cannot be changed.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIProfileTemplates))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var templateManager = services.GetRequiredService<IAIProfileTemplateManager>();
        arguments.TryGetFirstString("templateId", out var templateId);

        AIProfileTemplate template;
        var isNew = string.IsNullOrWhiteSpace(templateId);

        if (isNew)
        {
            arguments.TryGetFirstString("source", out var source);
            arguments.TryGetFirstString("name", out var name);
            var sources = services.GetService<IOptions<AIOptions>>()?.Value.TemplateSources;
            source = source?.Trim();
            name = name?.Trim();

            if (string.IsNullOrEmpty(source) || sources?.ContainsKey(source) != true)
            {
                return "Nothing was saved: give the source of the new template, Profile or SystemPrompt.";
            }

            if (string.IsNullOrEmpty(name))
            {
                return "Nothing was saved: a new template needs a technical name.";
            }

            var catalog = services.GetService<INamedSourceCatalog<AIProfileTemplate>>();

            if (catalog is not null && await catalog.FindByNameAsync(name, cancellationToken) is not null)
            {
                return $"Nothing was saved: another template is named '{name}'.";
            }

            template = await templateManager.NewAsync(source, cancellationToken: cancellationToken);

            if (template is null)
            {
                return "Nothing was saved: the template manager could not create a new template.";
            }

            template.Name = name;

            var caller = AgentToolAuthorization.GetCaller(services);
            template.OwnerId ??= caller?.FindFirstValue(ClaimTypes.NameIdentifier);
            template.Author ??= caller?.Identity?.Name;
        }
        else
        {
            template = await ProfileTemplateToolSupport.FindStoredAsync(services, templateId, cancellationToken);

            if (template is null)
            {
                return await templateManager.FindByIdAsync(templateId.Trim(), cancellationToken) is null
                    ? $"Nothing was saved: no AI profile template has the ID '{templateId}'. Use listAIProfileTemplates to find it."
                    : "Nothing was saved: the template is shipped with a module and cannot be changed. Create a stored template instead.";
            }

            if ((await AIConfigurationGuard.GetProtectedAsync(services, cancellationToken)).IsProtectedTemplate(template))
            {
                return AIProfileToolSupport.RunningAssistantRefusal(
                    $"the template '{ProfileTemplateToolSupport.GetTitle(template)}'",
                    AIProfileToolSupport.GetTemplateEditPath(services, template.ItemId));
            }
        }

        var error = Apply(arguments, template, isNew);

        if (error is not null)
        {
            return $"Nothing was saved: {error}";
        }

        var validation = await templateManager.ValidateAsync(template, cancellationToken);

        if (!validation.Succeeded)
        {
            return $"Nothing was saved: {string.Join(" ", validation.Errors.Select(e => e.ErrorMessage))}";
        }

        if (isNew)
        {
            await templateManager.CreateAsync(template, cancellationToken);
        }
        else
        {
            await templateManager.UpdateAsync(template, cancellationToken: cancellationToken);
        }

        return AgentToolResults.Json(new
        {
            created = isNew,
            updated = !isNew,
            id = template.ItemId,
            name = template.Name,
            title = template.DisplayText,
            source = template.Source,
            category = template.Category,
            isListable = template.IsListable,
            editPath = AIProfileToolSupport.GetTemplateEditPath(services, template.ItemId),
        });
    }

    private static string Apply(AIFunctionArguments arguments, AIProfileTemplate template, bool isNew)
    {
        if (arguments.TryGetFirstString("title", out var title) && title is not null)
        {
            template.DisplayText = title.Trim();
        }

        if (string.IsNullOrWhiteSpace(template.DisplayText))
        {
            return "the title is required.";
        }

        if (arguments.TryGetFirstString("description", out var description) && description is not null)
        {
            template.Description = description.Trim();
        }

        var categoryGiven = arguments.TryGetFirstString("category", out var category) && category is not null;

        if (categoryGiven)
        {
            template.Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        }

        if (arguments.TryGetFirst<bool>("isListable", out var isListable))
        {
            template.IsListable = isListable;
        }
        else if (isNew && !string.IsNullOrEmpty(template.Category))
        {
            // The speaking style and guardrail topic pages create their templates unlisted.
            template.IsListable = false;
        }

        arguments.TryGetFirstString("instructions", out var instructions);

        if (string.Equals(template.Source, AITemplateSources.SystemPrompt, StringComparison.OrdinalIgnoreCase))
        {
            if (instructions is not null)
            {
                template.Alter<SystemPromptTemplateMetadata>(metadata => metadata.SystemMessage = instructions.Trim());
            }

            return null;
        }

        if (!string.Equals(template.Source, AITemplateSources.Profile, StringComparison.OrdinalIgnoreCase))
        {
            return $"templates of source '{template.Source}' cannot be changed here.";
        }

        var profile = template.GetOrCreate<ProfileTemplateMetadata>();

        if (arguments.TryGetFirstString("profileType", out var profileType) && !string.IsNullOrWhiteSpace(profileType))
        {
            if (!Enum.TryParse<AIProfileType>(profileType.Trim(), ignoreCase: true, out var type) || !Enum.IsDefined(type))
            {
                return $"'{profileType}' is not a profile type. Use Chat, Utility, TemplatePrompt or Agent.";
            }

            profile.ProfileType = type;
        }

        if (profile.ProfileType is null)
        {
            return "a Profile template needs a profile type.";
        }

        if (instructions is not null)
        {
            profile.SystemMessage = instructions.Trim();
        }

        if (arguments.TryGetFirstString("profileDescription", out var profileDescription) && profileDescription is not null)
        {
            profile.Description = profileDescription.Trim();
        }

        if (arguments.TryGetFirstString("welcomeMessage", out var welcomeMessage) && welcomeMessage is not null)
        {
            profile.WelcomeMessage = welcomeMessage.Trim();
        }

        if (arguments.TryGetFirstString("chatDeploymentName", out var chatDeploymentName) && chatDeploymentName is not null)
        {
            profile.ChatDeploymentName = string.IsNullOrWhiteSpace(chatDeploymentName) ? null : chatDeploymentName.Trim();
        }

        if (arguments.TryGetFirstString("utilityDeploymentName", out var utilityDeploymentName) && utilityDeploymentName is not null)
        {
            profile.UtilityDeploymentName = string.IsNullOrWhiteSpace(utilityDeploymentName) ? null : utilityDeploymentName.Trim();
        }

        if (arguments.TryGetFirst<float>("temperature", out var temperature))
        {
            profile.Temperature = temperature;
        }

        if (arguments.TryGetFirst<float>("topP", out var topP))
        {
            profile.TopP = topP;
        }

        if (arguments.TryGetFirst<int>("maxOutputTokens", out var maxOutputTokens))
        {
            profile.MaxOutputTokens = maxOutputTokens;
        }

        if (arguments.TryGetFirst<int>("pastMessagesCount", out var pastMessagesCount))
        {
            profile.PastMessagesCount = pastMessagesCount;
        }

        template.Put(profile);

        return null;
    }
}
