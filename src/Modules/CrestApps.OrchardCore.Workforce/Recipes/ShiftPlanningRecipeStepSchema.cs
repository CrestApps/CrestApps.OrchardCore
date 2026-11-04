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
            .Description("Creates or updates shift templates and rotations, matched by id or name, and coverage requirements, matched by id or by team and name.")
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("WorkforceShiftPlanning").Description("Recipe step discriminator. Must be 'WorkforceShiftPlanning'.")),
                ("Templates", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description("Shift templates: name, color, start (HH:mm:ss), lengthMinutes, enabled, teamNames, and segments with activityName, offsetMinutes and lengthMinutes.")),
                ("Patterns", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description("Rotations, matched by id or name: name, teamName, weekCount, enabled, days with week, day and templateName, and workers with workerId and startDate.")),
                ("CoverageRequirements", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description("Coverage requirements, matched by id or by team and name: name, teamName, skillName, source, effectiveFrom, effectiveTo, and rows with day or date, start, end, minimum, target and maximum.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
