using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Workforce.Recipes;

/// <summary>
/// Describes the WorkforceShiftPlanning recipe step to CrestApps Recipes.
/// </summary>
internal sealed class ShiftPlanningRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => ShiftPlanningRecipeStep.ShiftPlanningStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Creates or updates shift templates, matched by id or name.")
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("WorkforceShiftPlanning").Description("Recipe step discriminator. Must be 'WorkforceShiftPlanning'.")),
                ("Templates", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description("Shift templates: name, color, start (HH:mm:ss), lengthMinutes, enabled, teamNames, and segments with activityName, offsetMinutes and lengthMinutes.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
