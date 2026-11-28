using System.Text.Json;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ProfileTemplates;

/// <summary>
/// Deletes stored AI profile templates, as the Delete action and bulk Delete of the Templates page do. Shipped
/// templates and the templates the running assistant selects are skipped and reported.
/// </summary>
public sealed class DeleteAIProfileTemplateTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteAIProfileTemplate";

    private const int MaxTemplates = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "templateIds": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "maxItems": 50,
          "description": "The IDs of the stored templates to delete, from listAIProfileTemplates."
        }
      },
      "required": ["templateIds"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes stored templates by id. Profiles that selected a deleted SystemPrompt template lose that block of instructions. Shipped templates and templates the running assistant uses are skipped.";

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

        if (!AgentToolArguments.TryReadStrings(arguments, "templateIds", out var templateIds) || templateIds is not { Count: > 0 })
        {
            return "Nothing was deleted: give the IDs of the templates to delete in templateIds.";
        }

        if (templateIds.Count > MaxTemplates)
        {
            return $"Nothing was deleted: at most {MaxTemplates} templates can be deleted at once.";
        }

        var templateManager = services.GetRequiredService<IAIProfileTemplateManager>();
        var protectedConfiguration = await AIConfigurationGuard.GetProtectedAsync(services, cancellationToken);
        var deleted = new List<object>();
        var skipped = new List<object>();

        foreach (var templateId in templateIds)
        {
            var template = await ProfileTemplateToolSupport.FindStoredAsync(services, templateId, cancellationToken);

            if (template is null)
            {
                skipped.Add(new
                {
                    id = templateId,
                    reason = await templateManager.FindByIdAsync(templateId, cancellationToken) is null
                        ? "No template has this ID."
                        : "The template is shipped with a module and cannot be deleted.",
                });

                continue;
            }

            var title = ProfileTemplateToolSupport.GetTitle(template);

            if (protectedConfiguration.IsProtectedTemplate(template))
            {
                skipped.Add(new { id = template.ItemId, title, reason = AIProfileToolSupport.RunningAssistantRefusal($"the template '{title}'", AIProfileToolSupport.GetTemplateEditPath(services, template.ItemId)) });

                continue;
            }

            if (await templateManager.DeleteAsync(template, cancellationToken))
            {
                deleted.Add(new { id = template.ItemId, name = template.Name, title, source = template.Source });
            }
            else
            {
                skipped.Add(new { id = template.ItemId, title, reason = "The template manager did not delete it." });
            }
        }

        return AgentToolResults.Json(new
        {
            deleted,
            skipped,
        });
    }
}
