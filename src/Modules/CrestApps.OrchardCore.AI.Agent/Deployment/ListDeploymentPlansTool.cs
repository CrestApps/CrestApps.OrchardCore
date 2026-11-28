using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Agent.Deployment;

/// <summary>
/// Lists the deployment plans from the deployment plan service, as the Deployment Plans page does, with the same two
/// permissions that page asks for: managing deployment plans and exporting.
/// </summary>
public sealed class ListDeploymentPlansTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listDeploymentPlans";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Text the plan name must contain. Leave it out to list every plan."
        },
        "includeSteps": {
          "type": "boolean",
          "description": "Whether to return the ordered steps of each plan with their fields, and the step types available on this site. Ask for it before changing a plan. The default is false."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1. The default is 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of plans on a page. The default is 20 and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists deployment plans, optionally filtered by name and with their ordered steps, plus the step types available on this site. Use it to find a plan before exporting or changing it.";

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

        // As the Deployment Plans page does: both permissions are needed to see the plans.
        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, DeploymentPermissions.ManageDeploymentPlan) ||
            !await AgentToolAuthorization.IsAuthorizedAsync(services, DeploymentPermissions.Export))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("search", out var search);
        search = search?.Trim();

        var includeSteps = arguments.GetFirstValueOrDefault("includeSteps", false);
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        var plans = (await DeploymentToolSupport.GetPlansAsync(services))
            .Where(plan => string.IsNullOrEmpty(search) || plan.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(plan => plan.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return AgentToolResults.Json(new
        {
            items = plans
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(plan => new
                {
                    id = plan.Id,
                    name = plan.Name,
                    stepCount = plan.DeploymentSteps.Count,
                    steps = includeSteps ? DeploymentToolSupport.DescribeSteps(services, plan) : null,
                    link = AgentToolLinks.GetAdminPath(services, "OrchardCore.Deployment", "DeploymentPlan", "Display", new() { ["id"] = plan.Id }),
                })
                .ToList(),
            total = plans.Count,
            page,
            pageSize,
            truncated = plans.Count > page * pageSize
                ? true
                : (bool?)null,
            stepTypes = includeSteps ? await GetStepTypesAsync(services) : null,
            note = includeSteps
                ? "To change a plan, send its name and every step to keep, each as { type, step }, to saveDeploymentPlan."
                : "Call again with includeSteps true to see what each plan exports and the step types available.",
        });
    }

    private static async Task<List<object>> GetStepTypesAsync(IServiceProvider services)
    {
        var schemaService = services.GetService<IDeploymentSchemaService>();

        if (schemaService is not null)
        {
            return (await schemaService.GetStepDescriptorsAsync())
                .OrderBy(descriptor => descriptor.StepType, StringComparer.Ordinal)
                .Select(descriptor => (object)new
                {
                    type = descriptor.StepType,
                    title = descriptor.DisplayText,
                    description = descriptor.Description,
                    fields = descriptor.Properties.ToDictionary(
                        property => property.Name,
                        property => JsonSerializer.SerializeToNode(property.Schema.Build())),
                    required = descriptor.RequiredProperties.Count > 0 ? descriptor.RequiredProperties : null,
                })
                .ToList();
        }

        return services.GetServices<IDeploymentStepFactory>()
            .Select(factory => factory.Name)
            .Order(StringComparer.Ordinal)
            .Select(name => (object)new
            {
                type = name,
            })
            .ToList();
    }
}
