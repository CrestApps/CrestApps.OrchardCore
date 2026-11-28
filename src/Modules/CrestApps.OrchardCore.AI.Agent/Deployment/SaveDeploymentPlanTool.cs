using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Agent.Deployment;

/// <summary>
/// Creates a deployment plan or replaces the steps of the plan with the same name, through the <c>deployment</c>
/// recipe step, which builds each step with its registered step factory and saves the plan with the deployment plan
/// service. The step runs inside the call, so an unknown step type is reported.
/// </summary>
public sealed class SaveDeploymentPlanTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveDeploymentPlan";

    private const string NameMember = "Name";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "The name of the plan. A plan with this name is replaced; otherwise a new plan is created."
        },
        "steps": {
          "type": "array",
          "description": "Every step of the plan, in order. Steps left out are removed from an existing plan.",
          "items": {
            "type": "object",
            "properties": {
              "type": {
                "type": "string",
                "description": "The step type, from stepTypes of listDeploymentPlans with includeSteps, such as 'ContentDefinitionDeploymentStep'."
              },
              "step": {
                "type": "object",
                "description": "The fields of the step, as described by the step type. Leave it out or send an empty object for a step without fields."
              }
            },
            "required": [
              "type"
            ],
            "additionalProperties": false
          }
        }
      },
      "required": [
        "name",
        "steps"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a deployment plan or replaces an existing one: its name and the ordered list of steps. Send every step to keep; step types and their fields come from listDeploymentPlans.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, DeploymentPermissions.ManageDeploymentPlan))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("name", out var name);
        name = name?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            return "Nothing was saved: give the name of the plan.";
        }

        if (!AgentToolArguments.TryReadJsonArray(arguments, "steps", out var steps) || steps is null)
        {
            return "Nothing was saved: steps must be a list of { type, step } objects.";
        }

        var stepTypes = services.GetServices<IDeploymentStepFactory>()
            .Select(factory => factory.Name)
            .ToHashSet(StringComparer.Ordinal);

        var planSteps = new JsonArray();
        var unknown = new List<string>();

        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i] is not JsonObject step)
            {
                return $"Nothing was saved: step {i + 1} must be an object with a type and its fields.";
            }

            var type = Read(step, "type") is JsonValue typeValue && typeValue.TryGetValue<string>(out var typeText) ? typeText?.Trim() : null;
            var fields = Read(step, "step");

            if (string.IsNullOrEmpty(type))
            {
                return $"Nothing was saved: step {i + 1} has no type.";
            }

            if (fields is not null and not JsonObject)
            {
                return $"Nothing was saved: the fields of step {i + 1} ({type}) must be an object.";
            }

            if (!stepTypes.Contains(type))
            {
                unknown.Add(type);

                continue;
            }

            planSteps.Add(new JsonObject
            {
                ["Type"] = type,
                ["Step"] = fields?.DeepClone() ?? new JsonObject(),
            });
        }

        if (unknown.Count > 0)
        {
            return $"Nothing was saved: {string.Join(", ", unknown.Distinct())} is not a step type of this site; the feature that provides it may be off. Use listDeploymentPlans with includeSteps to see the step types.";
        }

        var existing = (await DeploymentToolSupport.GetPlansAsync(services))
            .FirstOrDefault(plan => string.Equals(plan.Name, name, StringComparison.OrdinalIgnoreCase));

        var errors = await RecipeStepRunner.RunAsync(services, DeploymentToolSupport.RecipeStepName, new JsonObject
        {
            ["name"] = DeploymentToolSupport.RecipeStepName,
            ["Plans"] = new JsonArray(new JsonObject
            {
                [NameMember] = name,
                ["Steps"] = planSteps,
            }),
        });

        if (errors.Count > 0)
        {
            return $"Nothing was saved: {string.Join(" ", errors)}";
        }

        // A new plan gets its ID when it is saved, so it is read back.
        var saved = existing ?? (await DeploymentToolSupport.GetPlansAsync(services))
            .FirstOrDefault(plan => string.Equals(plan.Name, name, StringComparison.OrdinalIgnoreCase));

        return AgentToolResults.Json(new
        {
            saved = true,
            created = existing is null,
            id = saved?.Id,
            name,
            stepCount = planSteps.Count,
            stepTypes = planSteps.Select(step => step["Type"]?.GetValue<string>()).ToList(),
            note = "The plan is saved. Use listDeploymentPlans with includeSteps to check its steps, and exportDeploymentPlan or the plan page to run it.",
        });
    }

    private static JsonNode Read(JsonObject node, string name)
        => node.FirstOrDefault(member => string.Equals(member.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
}
