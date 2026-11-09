using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using Json.Schema;
using static CrestApps.OrchardCore.Workforce.ContactCenter.Recipes.WorkforceContactCenterSchemaBuilder;

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
        _cached ??= Step(
            ForecastingRecipeStep.ForecastingStepName,
            "Creates or updates contact center forecasting settings: planning groups, forecast events and capacity plans, each matched by itemId then name. A record that is updated takes every member of the entry, so a member left out goes back to its default. Planning groups are imported first, so events and plans can name one of the same step.",
            ("PlanningGroups", Array(PlanningGroup(), "The planning groups to create or update.")),
            ("ForecastEvents", Array(ForecastEvent(), "The forecast events to create or update.")),
            ("CapacityPlans", Array(CapacityPlan(), "The capacity plans to create or update.")));

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder PlanningGroup()
        => Object(
            "A planning group: queues forecast and staffed together.",
            ("itemId", ItemId("planning group")),
            ("name", Text("The planning group name, unique.")),
            ("queueNames", Strings("The queues it forecasts, by name; added to queueIds. Queues named must exist.")),
            ("queueIds", Strings("The queues it forecasts, by id.")),
            ("channel", OptionalEnum<InteractionChannel>("The channel it forecasts; null for every channel of its queues.")),
            ("teamName", Text("The team that staffs it, by name. Wins over teamId.")),
            ("teamId", Text("The team that staffs it, by id.")),
            ("serviceLevelPercent", OptionalNumber("The share of contacts to answer within serviceLevelSeconds, in percent; null to plan by average speed of answer. Defaults to 80.")),
            ("serviceLevelSeconds", Integer("The service level time, in seconds. Defaults to 20.")),
            ("averageSpeedOfAnswerSeconds", OptionalInteger("The average speed of answer to plan for, in seconds, when there is no service level.")),
            ("maxOccupancyPercent", Number("The highest share of time agents should be busy, in percent. Defaults to 85.")),
            ("concurrency", Integer("How many contacts an agent handles at once. Defaults to 1.")),
            ("patienceSeconds", OptionalInteger("How long contacts wait before they give up, in seconds, to plan for abandons.")),
            ("shrinkagePercent", Number("The share of paid time agents are not available, in percent. Defaults to 30.")),
            ("historyWeeks", Integer("How many weeks of history the forecast reads. Defaults to 8.")))
        .Required("name");

    private static JsonSchemaBuilder ForecastEvent()
        => Object(
            "A forecast event: days whose volume differs from the usual.",
            ("itemId", ItemId("forecast event")),
            ("name", Text("The event name, unique.")),
            ("kind", Enum<ForecastEventKind>("What the event is.")),
            ("startDate", Date("The first day.")),
            ("endDate", Date("The last day.")),
            ("volumeMultiplier", Number("How the usual volume is multiplied on those days, such as 0 for a closure or 1.5 for a promotion. Defaults to 1.")),
            ("recursYearly", Flag("Whether the event comes back on the same dates every year.")),
            ("planningGroupNames", Strings("The planning groups it affects, by name; added to planningGroupIds. Empty for every planning group.")),
            ("planningGroupIds", Strings("The planning groups it affects, by id.")))
        .Required("name");

    private static JsonSchemaBuilder CapacityPlan()
    {
        var hiringClass = Object(
            "A class of new hires.",
            ("startDate", Date("The day the class starts.")),
            ("size", Number("How many people join.")),
            ("trainingWeeks", Integer("The weeks of training before they take contacts. Defaults to 4.")),
            ("rampWeeks", Integer("The weeks until they are fully productive. Defaults to 4.")));

        return Object(
            "A long-term capacity plan.",
            ("itemId", ItemId("capacity plan")),
            ("name", Text("The plan name, unique.")),
            ("planningGroupName", Text("The planning group it plans for, by name. Wins over planningGroupId.")),
            ("planningGroupId", Text("The planning group it plans for, by id.")),
            ("startDate", Date("The first week of the plan; moved back to its Monday.")),
            ("weeks", Integer("How many weeks the plan covers. Defaults to 26.")),
            ("weeklyWorkloadHours", OptionalNumber("The weekly workload to plan for, in hours; null to use the planning group's forecast.")),
            ("growthPercentPerYear", Number("How much the workload grows a year, in percent.")),
            ("hoursPerFte", Number("The weekly hours of one full-time agent. Defaults to 37.5.")),
            ("currentFte", Number("The full-time agents staffed now.")),
            ("attritionPercentPerMonth", Number("The share of agents who leave each month, in percent. Defaults to 2.")),
            ("hiringClasses", Array(hiringClass, "The planned classes of new hires.")))
        .Required("name");
    }
}
