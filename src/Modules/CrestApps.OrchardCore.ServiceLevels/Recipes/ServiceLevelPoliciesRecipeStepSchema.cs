using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.ServiceLevels.Recipes;

/// <summary>
/// Describes the service level policies recipe step to CrestApps Recipes.
/// </summary>
internal sealed class ServiceLevelPoliciesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => ServiceLevelsConstants.RecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(ServiceLevelsConstants.RecipeStepName).Description("Recipe step discriminator. Must be 'ServiceLevelPolicies'.")),
                (ServiceLevelPoliciesRecipeStep.EntriesName, new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(Policy()).Description("The policies to create, or update by id or by scope and name.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String).Description(description);

    private static JsonSchemaBuilder Policy()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", Text("The policy id; omit to match by scope and name.")),
                ("Name", Text("The policy name, unique within its scope.")),
                ("Description", Text("A description for administrators.")),
                ("Scope", new JsonSchemaBuilder().Type(SchemaValueType.String).Examples("WorkOrders", "Tickets").Description("The kind of work the policy is for: the name of a registered scope. It names the metrics and priorities.")),
                ("HoursScheduleId", Text("The business hours schedule the targets are counted in. Empty counts around the clock.")),
                ("WarningPercent", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Maximum(99).Description("How much of a target, in percent, passes before a warning is raised. 80 when omitted.")),
                ("Targets", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Metric", new JsonSchemaBuilder().Type(SchemaValueType.String).Examples("FirstResponse", "NextResponse", "Resolution").Description("The metric.")),
                            ("Priority", Text("The priority key the target is for, as the scope keys its priorities. Empty for every priority without a target of its own.")),
                            ("Minutes", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Description("The time allowed, in minutes of the policy's hours.")))
                        .Required("Metric", "Minutes"))
                    .Description("The time allowed for each metric at each priority.")),
                ("Properties", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("Settings other modules keep on the policy, such as the work order statuses that pause its timers.")))
            .Required("Name", "Scope")
            .AdditionalProperties(true);
    }
}
