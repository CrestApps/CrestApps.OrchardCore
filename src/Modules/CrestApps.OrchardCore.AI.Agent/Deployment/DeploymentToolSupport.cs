using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Deployment;
using OrchardCore.Json;
using YesSql;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Agent.Deployment;

/// <summary>
/// What the deployment plan tools share: how a plan and its steps are described, in the shape the <c>deployment</c>
/// recipe step takes.
/// </summary>
internal static class DeploymentToolSupport
{
    /// <summary>
    /// The name of the recipe step that creates or replaces deployment plans by name.
    /// </summary>
    public const string RecipeStepName = "deployment";

    /// <summary>
    /// Reads every deployment plan from the session. <see cref="IDeploymentPlanService"/> keeps the plans it read for
    /// the rest of the scope, and all tool calls of one chat message share one scope, so a plan saved earlier in the
    /// message would be missing from it. A session query sees it, because the session saves pending changes first.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The plans.</returns>
    public static async Task<List<DeploymentPlan>> GetPlansAsync(IServiceProvider services)
        => (await services.GetRequiredService<YSession>().Query<DeploymentPlan>().ListAsync()).ToList();

    /// <summary>
    /// Describes the steps of a plan: the step type and the fields of each step, in the plan's order.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="plan">The plan.</param>
    /// <returns>The steps.</returns>
    public static List<object> DescribeSteps(IServiceProvider services, DeploymentPlan plan)
    {
        var options = services.GetRequiredService<IOptions<DocumentJsonSerializerOptions>>().Value.SerializerOptions;

        return plan.DeploymentSteps
            .Select(step =>
            {
                var values = JsonSerializer.SerializeToNode(step, step.GetType(), options) as JsonObject ?? new JsonObject();

                // The type and the ID are given apart; the type discriminator is not a field of the step.
                foreach (var key in values.Select(member => member.Key).Where(key => key is "$type" or nameof(DeploymentStep.Id) or nameof(DeploymentStep.Name)).ToList())
                {
                    values.Remove(key);
                }

                var unavailable = step is UnknownDeploymentStep;

                return (object)new
                {
                    type = unavailable ? step.Name : step.GetType().Name,
                    id = step.Id,
                    unavailable = unavailable ? true : (bool?)null,
                    step = values,
                };
            })
            .ToList();
    }
}
