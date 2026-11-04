using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Recipes;

/// <summary>
/// Describes the WorkforceForecasting recipe step to CrestApps Recipes.
/// </summary>
internal sealed class ForecastingRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => ForecastingRecipeStep.ForecastingStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Creates or updates contact center forecasting settings: planning groups, forecast events and capacity plans, matched by id or name.")
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(ForecastingRecipeStep.ForecastingStepName).Description("Recipe step discriminator. Must be 'WorkforceForecasting'.")),
                ("PlanningGroups", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description("Planning groups: name, queueNames, teamName, channel, serviceLevelPercent, serviceLevelSeconds, averageSpeedOfAnswerSeconds, maxOccupancyPercent, concurrency, patienceSeconds, shrinkagePercent and historyWeeks.")),
                ("ForecastEvents", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description("Forecast events: name, kind (Holiday, Closure, Promotion, Outage or Custom), startDate, endDate, volumeMultiplier, recursYearly and planningGroupNames (empty for every planning group).")),
                ("CapacityPlans", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(true)).Description("Capacity plans: name, planningGroupName, startDate, weeks, weeklyWorkloadHours, growthPercentPerYear, hoursPerFte, currentFte, attritionPercentPerMonth and hiringClasses (startDate, size, trainingWeeks, rampWeeks).")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
