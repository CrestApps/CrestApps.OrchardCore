using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.Quality;

/// <summary>
/// Deletes AI alert rules, as the Delete action of the Alert rules page does. A rule keeps its incident history
/// inside itself, so the history goes with it.
/// </summary>
public sealed class DeleteAIAlertRuleTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteAIAlertRule";

    private const int MaxRules = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "ruleIds": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "maxItems": 50,
          "description": "The IDs of the alert rules to delete, from listAIAlerts."
        }
      },
      "required": ["ruleIds"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes alert rules by id, with their incident history.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIQuality))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "ruleIds", out var ruleIds) || ruleIds is not { Count: > 0 })
        {
            return "Nothing was deleted: give the IDs of the alert rules to delete in ruleIds.";
        }

        if (ruleIds.Count > MaxRules)
        {
            return $"Nothing was deleted: at most {MaxRules} alert rules can be deleted at once.";
        }

        var store = services.GetRequiredService<IAIAlertRuleStore>();
        var deleted = new List<object>();
        var skipped = new List<object>();

        foreach (var ruleId in ruleIds)
        {
            var rule = await store.FindByIdAsync(ruleId, cancellationToken);

            if (rule is null)
            {
                skipped.Add(new { id = ruleId, reason = "No alert rule has this ID." });

                continue;
            }

            if (!await store.DeleteAsync(rule, cancellationToken))
            {
                skipped.Add(new { id = rule.ItemId, name = rule.Name, reason = "The store did not delete it." });

                continue;
            }

            deleted.Add(new { id = rule.ItemId, name = rule.Name, wasOpen = rule.IsOpen, incidentsDeleted = rule.Incidents?.Count ?? 0 });
        }

        return AgentToolResults.Json(new
        {
            deleted,
            skipped,
        });
    }
}
