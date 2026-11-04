using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Workforce.Recipes;

/// <summary>
/// Describes the WorkforceTimeOff recipe step to CrestApps Recipes.
/// </summary>
internal sealed class TimeOffRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => TimeOffRecipeStep.TimeOffStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Creates or updates time-off settings, matched by id or name.")
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("WorkforceTimeOff").Description("Recipe step discriminator. Must be 'WorkforceTimeOff'.")),
                ("Types", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description("Time-off types, with the fields of the type editor: name, unit (Hours or Days), hoursPerDay, isPaid, requiresBalance, payCode and the rest.")),
                ("Policies", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description("Time-off policies: name, typeName, the accrual fields, and teamNames for the teams they apply to.")),
                ("Allotments", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description("Allotments: name, teamName, typeNames, weekdaySlots (seven numbers, Monday first) and dates.")),
                ("BlackoutPeriods", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description("Blackout dates: name, teamName, startDate, endDate, typeNames and message.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
