using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Workforce.Recipes;

/// <summary>
/// Describes the WorkforceTimeClock recipe step to CrestApps Recipes.
/// </summary>
internal sealed class TimeClockRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => TimeClockRecipeStep.TimeClockStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Creates or updates time clock settings: pay periods and policies matched by id or name, pay codes by code.")
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("WorkforceTimeClock").Description("Recipe step discriminator. Must be 'WorkforceTimeClock'.")),
                ("PayPeriods", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description("Pay periods: name, frequency (Weekly, Biweekly, SemiMonthly or Monthly), anchorDate and weekStart.")),
                ("PayCodes", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description("Pay codes: code, name, kind, multiplier, and for a shift differential premiumPerHour, windowStart, windowEnd and weekdays.")),
                ("Policies", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description("Time clock policies: name, payPeriodsName, the policy fields, and teamNames for the teams that use it.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
