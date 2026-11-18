using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.Quality;

/// <summary>
/// Deletes QA cohorts, as the Delete action of the QA page does. The scores already recorded stay.
/// </summary>
public sealed class DeleteAIQACohortTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteAIQACohort";

    private const int MaxCohorts = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "cohortIds": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "maxItems": 50,
          "description": "The IDs of the QA cohorts to delete, from listAIQACohorts."
        }
      },
      "required": ["cohortIds"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes QA cohorts by id. Scores already recorded stay.";

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

        if (!AgentToolArguments.TryReadStrings(arguments, "cohortIds", out var cohortIds) || cohortIds is not { Count: > 0 })
        {
            return "Nothing was deleted: give the IDs of the QA cohorts to delete in cohortIds.";
        }

        if (cohortIds.Count > MaxCohorts)
        {
            return $"Nothing was deleted: at most {MaxCohorts} QA cohorts can be deleted at once.";
        }

        var store = services.GetRequiredService<IAIQACohortStore>();
        var deleted = new List<object>();
        var skipped = new List<object>();

        foreach (var cohortId in cohortIds)
        {
            var cohort = await store.FindByIdAsync(cohortId, cancellationToken);

            if (cohort is null)
            {
                skipped.Add(new { id = cohortId, reason = "No QA cohort has this ID." });

                continue;
            }

            if (!await store.DeleteAsync(cohort, cancellationToken))
            {
                skipped.Add(new { id = cohort.ItemId, name = cohort.Name, reason = "The store did not delete it." });

                continue;
            }

            deleted.Add(new { id = cohort.ItemId, name = cohort.Name, scoresKeptUnderLabel = cohort.GetLabel() });
        }

        return AgentToolResults.Json(new
        {
            deleted,
            skipped,
        });
    }
}
