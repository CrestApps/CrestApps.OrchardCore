using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using YesSql;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Agent.Deployment;

/// <summary>
/// Deletes a deployment plan, as the Delete action of the Deployment Plans page does: the plan document is removed
/// from the session.
/// </summary>
public sealed class DeleteDeploymentPlanTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteDeploymentPlan";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "integer",
          "minimum": 1,
          "description": "The ID of the deployment plan to delete, from listDeploymentPlans."
        }
      },
      "required": [
        "id"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Permanently deletes a deployment plan. It does not touch data already exported.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, DeploymentPermissions.ManageDeploymentPlan))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var id = arguments.GetFirstValueOrDefault("id", 0L);

        if (id <= 0)
        {
            return "Nothing was deleted: give the ID of the plan, from listDeploymentPlans.";
        }

        var session = services.GetRequiredService<YSession>();
        var plan = await session.GetAsync<DeploymentPlan>(id);

        if (plan is null)
        {
            return $"Nothing was deleted: there is no deployment plan with the ID {id}. Use listDeploymentPlans to find it.";
        }

        session.Delete(plan);

        return AgentToolResults.Json(new
        {
            deleted = true,
            id,
            name = plan.Name,
            stepCount = plan.DeploymentSteps.Count,
            note = "The plan cannot be restored. Data already exported with it is not affected.",
        });
    }
}
