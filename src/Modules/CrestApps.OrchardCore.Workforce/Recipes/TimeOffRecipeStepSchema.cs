using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Workforce.Models;
using Json.Schema;
using static CrestApps.OrchardCore.Workforce.Recipes.WorkforceSchemaBuilder;

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
        _cached ??= Step(
            TimeOffRecipeStep.TimeOffStepName,
            "Creates or updates time-off types, policies, allotments and blackout periods, each matched by itemId then name. A record that is updated takes every member of the entry, so a member left out goes back to its default. Types are imported first, so the other records can name a type of the same step; teams named must exist.",
            ("Types", Array(TimeOffType(), "The time-off types to create or update.")),
            ("Policies", Array(Policy(), "The accrual policies to create or update.")),
            ("Allotments", Array(Allotment(), "The allotments (how many people may be off a day) to create or update.")),
            ("BlackoutPeriods", Array(Blackout(), "The blackout periods (when time off can't be asked for) to create or update.")));

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder TimeOffType()
        => Object(
            "A time-off type, such as vacation or sick leave.",
            ("itemId", ItemId("type")),
            ("name", Text("The type name, unique.")),
            ("description", Text("What the type is for.")),
            ("color", Text("The color, such as #2b8a3e.")),
            ("unit", Enum<TimeOffUnit>("Whether balances are kept in hours or days. Defaults to Hours.")),
            ("hoursPerDay", Number("The hours a day of time off counts as. Defaults to 8.")),
            ("isPaid", Flag("Whether the time off is paid. Defaults to true.")),
            ("requiresBalance", Flag("Whether a request needs enough balance. Defaults to true.")),
            ("allowPartialDay", Flag("Whether part of a day can be asked for. Defaults to true.")),
            ("requiresReason", Flag("Whether a request needs a reason.")),
            ("countsAgainstAllotment", Flag("Whether the time off takes a place in the team's allotment. Defaults to true.")),
            ("payCode", Text("The pay code the time off is paid under.")),
            ("visibleToWorkers", Flag("Whether workers can ask for it themselves. Defaults to true.")),
            ("enabled", Flag("Whether the type can be used. Defaults to true.")),
            ("stageId", Text("The id of the Time Tracker stage approved time off of this type is kept in, when Time Tracker is on.")))
        .Required("name");

    private static JsonSchemaBuilder Policy()
    {
        var tier = Object(
            "A rate that applies from a length of service.",
            ("fromMonths", Integer("The months of service the rate starts at.")),
            ("rate", Number("The accrual rate from then.")));

        var assignment = Object(
            "Who the policy applies to.",
            ("teamId", Text("The team's id.")),
            ("workerId", Text("A worker's id, for a policy of one worker.")),
            ("effectiveFrom", OptionalDate("The first day it applies.")),
            ("effectiveTo", OptionalDate("The last day it applies.")));

        return Object(
            "An accrual policy: how a balance of a type grows.",
            ("itemId", ItemId("policy")),
            ("name", Text("The policy name, unique.")),
            ("typeName", Text("The time-off type, by name. Wins over typeId.")),
            ("typeId", Text("The time-off type, by id.")),
            ("accrualMethod", Enum<TimeOffAccrualMethod>("How the balance grows. Defaults to PerPeriod.")),
            ("frequency", Enum<TimeOffAccrualFrequency>("How often a per-period accrual is added. Defaults to Monthly.")),
            ("anchorDate", OptionalDate("A date the accrual periods count from.")),
            ("rate", Number("The amount added each period, each hour worked, or each year, in the type's unit.")),
            ("tenureTiers", Array(tier, "Higher rates after a length of service.")),
            ("waitingPeriodDays", Integer("The days after hire before the balance grows.")),
            ("maxBalance", OptionalNumber("The largest balance; null for no cap.")),
            ("carryoverMax", OptionalNumber("The most that carries over at the reset; null for all of it.")),
            ("resetMonth", Integer("The month the balance resets, 1 to 12. Defaults to 1.")),
            ("resetDay", Integer("The day of the month the balance resets. Defaults to 1.")),
            ("negativeBalanceLimit", Number("How far below zero the balance may go.")),
            ("teamNames", Strings("The teams the policy applies to, by name; each is added to assignments.")),
            ("assignments", Array(assignment, "The teams or workers the policy applies to.")),
            ("enabled", Flag("Whether the policy is in use. Defaults to true.")))
        .Required("name");
    }

    private static JsonSchemaBuilder Allotment()
    {
        var date = Object(
            "The places on one date, instead of the weekday's.",
            ("date", Date("The date.")),
            ("slots", Integer("How many people may be off that day.")));

        return Object(
            "An allotment: how many people of a team may be off each day.",
            ("itemId", ItemId("allotment")),
            ("name", Text("The allotment name, unique.")),
            ("teamName", Text("The team, by name. Wins over teamId.")),
            ("teamId", Text("The team, by id.")),
            ("typeNames", Strings("The time-off types it counts, by name; added to typeIds.")),
            ("typeIds", Strings("The time-off types it counts, by id; empty for every type that counts against allotments.")),
            ("weekdaySlots", Array(new JsonSchemaBuilder().Type(SchemaValueType.Integer), "How many people may be off on each weekday: seven numbers, Monday first.")),
            ("dates", Array(date, "Dates with their own number of places.")),
            ("autoApprove", Flag("Whether a request is approved while there are places. Defaults to true.")),
            ("whenFull", Enum<TimeOffAllotmentFullAction>("What happens to a request when the day is full. Defaults to Waitlist.")),
            ("enabled", Flag("Whether the allotment is in use. Defaults to true.")))
        .Required("name");
    }

    private static JsonSchemaBuilder Blackout()
        => Object(
            "A blackout period: dates when time off can't be asked for.",
            ("itemId", ItemId("blackout period")),
            ("name", Text("The blackout name, unique.")),
            ("teamName", Text("The team, by name. Wins over teamId.")),
            ("teamId", Text("The team, by id; null for every team.")),
            ("startDate", Date("The first day.")),
            ("endDate", Date("The last day.")),
            ("typeNames", Strings("The time-off types it blocks, by name; added to typeIds.")),
            ("typeIds", Strings("The time-off types it blocks, by id; empty for every type.")),
            ("message", Text("What a worker who asks is told.")),
            ("enabled", Flag("Whether the blackout is in use. Defaults to true.")))
        .Required("name");
}
