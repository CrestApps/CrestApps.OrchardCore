using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Creates or updates a cadence and its ordered steps with the fields the cadence editor sets. The catalog manager runs
/// the cadence's rules (a name, and message text on every defined-message step).
/// </summary>
public sealed class SaveOmnichannelCadenceTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveOmnichannelCadence";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the cadence to update. Leave it out to create a cadence."
        },
        "displayText": {
          "type": "string",
          "description": "The cadence's name. Required when creating."
        },
        "description": {
          "type": "string",
          "description": "A description of the cadence. An empty text clears it."
        },
        "enabled": {
          "type": "boolean",
          "description": "Whether activities that use the cadence send its follow-ups. A new cadence is enabled unless this is false."
        },
        "steps": {
          "type": "array",
          "description": "The complete ordered list of steps; it replaces the current steps. To add a step, send the current steps (from getRecord with the kind omnichannelCadence) with the new one in place. Steps with a delay of 0 or less are dropped, as in the editor.",
          "items": {
            "type": "object",
            "properties": {
              "delayMinutes": {
                "type": "integer",
                "minimum": 1,
                "description": "How many minutes after the previous step (or the last contact) the message is sent."
              },
              "isAiGenerated": {
                "type": "boolean",
                "description": "Whether the AI writes the message. When false, message is sent as written."
              },
              "message": {
                "type": "string",
                "description": "The message to send. Required when isAiGenerated is false; for an AI step it is optional guidance."
              }
            },
            "required": ["delayMinutes"],
            "additionalProperties": false
          }
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or updates a cadence and its ordered steps (delay in minutes, fixed message or AI-generated). A cadence drives automated follow-up messages for the activities that use it.";

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

        if (!await AIModuleTools.IsAuthorizedAsync(services, OmnichannelConstants.Permissions.ManageCadences))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        List<CadenceStep> steps = null;

        if (arguments.TryGetFirst("steps", out _))
        {
            if (!arguments.TryGetFirst<CadenceStep[]>("steps", out var given) || given is null)
            {
                return "Nothing was saved: steps must be a list of objects with delayMinutes, isAiGenerated and message.";
            }

            // As the editor does: keep only the rows with a positive delay, trim their text and keep their order.
            steps = given
                .Where(step => step is not null && step.DelayMinutes > 0)
                .Select(step => new CadenceStep
                {
                    DelayMinutes = step.DelayMinutes,
                    IsAiGenerated = step.IsAiGenerated,
                    Message = step.Message?.Trim(),
                })
                .ToList();
        }

        var manager = services.GetRequiredService<ICatalogManager<Cadence>>();
        var id = AIModuleTools.ReadString(arguments, "id");
        Cadence existing = null;

        if (id is not null)
        {
            existing = await manager.FindByIdAsync(id, cancellationToken);

            if (existing is null)
            {
                return $"Nothing was saved: no cadence has the ID '{id}'. Use listRecords with the kind omnichannelCadence to find it.";
            }
        }

        // A cadence has no Clone, so the rules run on a copy of the fields this tool sets.
        var working = existing is null
            ? await manager.NewAsync(cancellationToken: cancellationToken)
            : new Cadence
            {
                ItemId = existing.ItemId,
                DisplayText = existing.DisplayText,
                Description = existing.Description,
                Enabled = existing.Enabled,
                Steps = (existing.Steps ?? []).ToList(),
                CreatedUtc = existing.CreatedUtc,
                ModifiedUtc = existing.ModifiedUtc,
            };

        if (arguments.TryGetFirstString("displayText", true, out var displayText))
        {
            working.DisplayText = displayText?.Trim();
        }

        if (arguments.TryGetFirstString("description", true, out var description))
        {
            working.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        }

        if (arguments.TryGetFirst<bool>("enabled", out var enabled))
        {
            working.Enabled = enabled;
        }

        if (steps is not null)
        {
            working.Steps = steps;
        }

        var validation = await manager.ValidateAsync(working, cancellationToken);

        if (!validation.Succeeded)
        {
            return "Nothing was saved: " + OmnichannelToolSupport.Reasons(validation);
        }

        if (existing is null)
        {
            await manager.CreateAsync(working, cancellationToken);
        }
        else
        {
            existing.DisplayText = working.DisplayText;
            existing.Description = working.Description;
            existing.Enabled = working.Enabled;
            existing.Steps = working.Steps;

            await manager.UpdateAsync(existing, cancellationToken: cancellationToken);
        }

        var saved = existing ?? working;
        return AIModuleTools.Json(new
        {
            saved = existing is null ? "created" : "updated",
            cadence = OmnichannelRecordKinds.DescribeCadence(saved),
        });
    }
}
