using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.Quality;

/// <summary>
/// Deletes AI simulation test cases, as the Delete action and bulk Delete of the AI tests page do. Their past runs
/// stay on the Runs page.
/// </summary>
public sealed class DeleteAITestCasesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteAITestCases";

    private const int MaxTestCases = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "testCaseIds": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "maxItems": 50,
          "description": "The IDs of the test cases to delete, from listAITestCases."
        }
      },
      "required": ["testCaseIds"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes test cases by id. Their past runs are kept.";

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

        if (!AgentToolArguments.TryReadStrings(arguments, "testCaseIds", out var testCaseIds) || testCaseIds is not { Count: > 0 })
        {
            return "Nothing was deleted: give the IDs of the test cases to delete in testCaseIds.";
        }

        if (testCaseIds.Count > MaxTestCases)
        {
            return $"Nothing was deleted: at most {MaxTestCases} test cases can be deleted at once.";
        }

        var store = services.GetRequiredService<IAITestCaseStore>();
        var deleted = new List<object>();
        var skipped = new List<object>();

        foreach (var testCaseId in testCaseIds)
        {
            var testCase = await store.FindByIdAsync(testCaseId, cancellationToken);

            if (testCase is null)
            {
                skipped.Add(new { id = testCaseId, reason = "No test case has this ID." });

                continue;
            }

            if (!await store.DeleteAsync(testCase, cancellationToken))
            {
                skipped.Add(new { id = testCase.ItemId, name = testCase.Name, reason = "The store did not delete it." });

                continue;
            }

            deleted.Add(new { id = testCase.ItemId, name = testCase.Name });
        }

        return AgentToolResults.Json(new
        {
            deleted,
            skipped,
        });
    }
}
