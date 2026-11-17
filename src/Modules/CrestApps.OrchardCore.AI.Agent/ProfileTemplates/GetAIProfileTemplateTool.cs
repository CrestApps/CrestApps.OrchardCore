using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ProfileTemplates;

/// <summary>
/// Returns one AI profile template's content: its instructions and, for a Profile template, the profile settings, tool
/// names and agent names it gives a profile created from it. Built from a list of fields, never from the stored entry.
/// </summary>
public sealed class GetAIProfileTemplateTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getAIProfileTemplate";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "templateId": {
          "type": "string",
          "description": "The ID of the template, from listAIProfileTemplates."
        }
      },
      "required": ["templateId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one template's full content: instructions, profile settings, tool and agent names.";

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

        arguments.TryGetFirstString("templateId", out var templateId);

        var templateManager = services.GetRequiredService<IAIProfileTemplateManager>();
        var template = string.IsNullOrWhiteSpace(templateId) ? null : await templateManager.FindByIdAsync(templateId.Trim(), cancellationToken);

        if (template is null)
        {
            return $"No AI profile template has the ID '{templateId}'. Use listAIProfileTemplates to find it.";
        }

        var isShipped = await ProfileTemplateToolSupport.FindStoredAsync(services, template.ItemId, cancellationToken) is null;
        var isProfile = string.Equals(template.Source, AITemplateSources.Profile, StringComparison.OrdinalIgnoreCase);
        object profile = null;
        string instructions;

        if (isProfile)
        {
            var metadata = template.GetOrCreate<ProfileTemplateMetadata>();
            instructions = metadata.SystemMessage;

            profile = new
            {
                type = metadata.ProfileType?.ToString(),
                description = metadata.Description,
                welcomeMessage = metadata.WelcomeMessage,
                promptTemplate = metadata.PromptTemplate,
                promptSubject = metadata.PromptSubject,
                chatDeploymentName = metadata.ChatDeploymentName,
                utilityDeploymentName = metadata.UtilityDeploymentName,
                temperature = metadata.Temperature,
                topP = metadata.TopP,
                frequencyPenalty = metadata.FrequencyPenalty,
                presencePenalty = metadata.PresencePenalty,
                maxOutputTokens = metadata.MaxOutputTokens,
                pastMessagesCount = metadata.PastMessagesCount,
                toolNames = metadata.ToolNames ?? [],
                agentNames = metadata.AgentNames ?? [],
                a2aConnectionCount = metadata.A2AConnectionIds?.Length ?? 0,
                agentAvailability = metadata.AgentAvailability?.ToString(),
                allowToolInvocation = metadata.AllowToolInvocation,
                personaName = template.TryGet<AIPersonaMetadata>(out var persona) ? persona.PersonaId : null,
                requiresFeatures = template.GetOrCreate<ProfileScenarioMetadata>().RequiresFeatures ?? [],
            };
        }
        else
        {
            instructions = template.TryGet<SystemPromptTemplateMetadata>(out var systemPrompt) ? systemPrompt.SystemMessage : null;
        }

        return AgentToolResults.Json(new
        {
            id = template.ItemId,
            name = template.Name,
            title = template.DisplayText,
            description = template.Description,
            source = template.Source,
            category = template.Category,
            isListable = template.IsListable,
            isShipped,
            instructions,
            profile,
            editPath = isShipped ? null : AIProfileToolSupport.GetTemplateEditPath(services, template.ItemId),
        });
    }
}
