using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agent.Profiles;

/// <summary>
/// Creates an AI profile from a profile template, as the New Profile wizard does: the template's values, a title, a
/// technical name and optionally a chat deployment. A profile whose template gives it tools is checked like a
/// capability change.
/// </summary>
public sealed class CreateAIProfileFromTemplateTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "createAIProfileFromTemplate";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "templateId": {
          "type": "string",
          "description": "The ID of a profile template of source Profile, from listAIProfileTemplates."
        },
        "title": {
          "type": "string",
          "description": "The title of the new profile."
        },
        "name": {
          "type": "string",
          "description": "The technical name of the new profile: unique on the site, it cannot change later."
        },
        "chatDeploymentName": {
          "type": "string",
          "description": "The name of the chat model deployment. Leave it out to use the template's, or the site default."
        }
      },
      "required": ["templateId", "title", "name"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a profile from a profile template (the New Profile wizard): give the template id, a title, a technical name and optionally a chat deployment. Fails with the missing feature names when the template's scenario is not available.";

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

        arguments.TryGetFirstString("templateId", out var templateId);
        arguments.TryGetFirstString("title", out var title);
        arguments.TryGetFirstString("name", out var name);
        arguments.TryGetFirstString("chatDeploymentName", out var chatDeploymentName);

        var templateManager = services.GetRequiredService<IAIProfileTemplateManager>();
        var template = string.IsNullOrWhiteSpace(templateId) ? null : await templateManager.FindByIdAsync(templateId.Trim(), cancellationToken);

        if (template is null || !string.Equals(template.Source, AITemplateSources.Profile, StringComparison.OrdinalIgnoreCase))
        {
            return $"Nothing was created: no profile template has the ID '{templateId}'. Use listAIProfileTemplates with source Profile to find it.";
        }

        var shellFeaturesManager = services.GetService<IShellFeaturesManager>();

        if (shellFeaturesManager is not null)
        {
            var features = await ProfileScenarioFeatureLookup.CreateAsync(shellFeaturesManager);
            var missing = features.GetMissingFeatureNames(template.GetOrCreate<ProfileScenarioMetadata>());

            if (missing.Count > 0)
            {
                return $"Nothing was created: the template '{template.DisplayText ?? template.Name}' needs features that are not enabled: {string.Join(", ", missing)}. Enable them first.";
            }
        }

        var factory = services.GetService<AIProfileTemplateProfileFactory>();
        var profileManager = services.GetRequiredService<IAIProfileManager>();
        var profile = factory is null ? null : await factory.NewFromTemplateAsync(template);

        if (profile is null)
        {
            return "Nothing was created: a new profile could not be made from the template.";
        }

        profile.Name = name?.Trim();
        profile.DisplayText = title?.Trim();

        if (!string.IsNullOrWhiteSpace(chatDeploymentName))
        {
            profile.ChatDeploymentName = chatDeploymentName.Trim();
        }

        // The wizard asks for both; the profile manager does not.
        if (string.IsNullOrEmpty(profile.Name))
        {
            return "Nothing was created: the technical name is required.";
        }

        if (string.IsNullOrEmpty(profile.DisplayText))
        {
            return "Nothing was created: the title is required.";
        }

        var validation = await profileManager.ValidateAsync(profile, cancellationToken);

        if (!validation.Succeeded)
        {
            return $"Nothing was created: {string.Join(" ", validation.Errors.Select(e => e.ErrorMessage))}";
        }

        // A template can carry tools, tool instances, agents and connections.
        var refusal = await AIConfigurationGuard.CheckCapabilityChangeAsync(services, before: null, profile, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var caller = AgentToolAuthorization.GetCaller(services);

        profile.OwnerId ??= caller?.FindFirstValue(ClaimTypes.NameIdentifier);
        profile.Author ??= caller?.Identity?.Name;

        AIProfileToolSupport.SetVersionNote(services, profile, $"Created from the template '{template.Name}'");

        await factory.CreateAsync(profile, template);

        var capabilities = AIProfileCapabilities.Read(profile);

        return AgentToolResults.Json(new
        {
            created = true,
            id = profile.ItemId,
            name = profile.Name,
            title = profile.DisplayText,
            type = profile.Type.ToString(),
            templateId = template.ItemId,
            toolNames = capabilities.ToolNames.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            agentNames = capabilities.AgentNames.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            editPath = AIProfileToolSupport.GetProfileEditPath(services, profile.ItemId),
        });
    }
}
