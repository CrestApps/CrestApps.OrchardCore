using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Workforce.Models;
using Json.Schema;
using static CrestApps.OrchardCore.Workforce.Recipes.WorkforceSchemaBuilder;

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
        _cached ??= Step(
            TimeClockRecipeStep.TimeClockStepName,
            "Creates or updates the time clock settings: pay periods, policies and kiosks matched by itemId then name, and pay codes matched by itemId then code. Pay periods, pay codes and policies that are updated take every member of the entry, so a member left out goes back to its default.",
            ("PayPeriods", Array(PayPeriods(), "The pay period calendars to create or update.")),
            ("PayCodes", Array(PayCode(), "The pay codes to create or update.")),
            ("Policies", Array(Policy(), "The time clock policies to create or update. Pay periods are imported first, so a policy can name one of the same step.")),
            ("Kiosks", Array(Kiosk(), "The time clock kiosks to create or update. Only the configuration is imported: a kiosk's token is never exported or imported, so issue a new address on the kiosk's page.")),
            ("PayrollCsv", PayrollCsv()));

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder PayPeriods()
        => Object(
            "A pay period calendar.",
            ("itemId", ItemId("pay period calendar")),
            ("name", Text("The calendar name, unique.")),
            ("frequency", Enum<PayPeriodFrequency>("How often a pay period starts. Defaults to Biweekly.")),
            ("anchorDate", Date("The first day of any one pay period, which the others count from.")),
            ("weekStart", Enum<DayOfWeek>("The first day of the week, for weekly overtime. Defaults to Sunday.")),
            ("enabled", Flag("Whether the calendar is in use. Defaults to true.")))
        .Required("name");

    private static JsonSchemaBuilder PayCode()
        => Object(
            "A pay code hours are paid under.",
            ("itemId", Text("The pay code's id. When it matches a stored pay code, that pay code is updated; otherwise it is matched by code, and a new one keeps this id.")),
            ("code", Text("The code, unique.")),
            ("name", Text("The pay code name.")),
            ("kind", Enum<PayCodeKind>("What the hours are. Defaults to Regular.")),
            ("multiplier", Number("The pay rate multiplier, such as 1.5 for overtime. Defaults to 1.")),
            ("premiumPerHour", OptionalNumber("For a shift differential: the amount added per hour.")),
            ("windowStart", OptionalTime("For a shift differential: when the window starts.")),
            ("windowEnd", OptionalTime("For a shift differential: when the window ends.")),
            ("weekdays", EnumArray<DayOfWeek>("For a shift differential: the days it applies; empty for every day.")),
            ("exportCode", Text("The code written in the payroll export, when not the code.")),
            ("enabled", Flag("Whether the pay code is in use. Defaults to true.")))
        .Required("code");

    private static JsonSchemaBuilder Policy()
    {
        var points = Object(
            "The attendance points an exception adds.",
            ("kind", Enum<AttendanceExceptionKind>("The kind of exception.")),
            ("points", Number("The points it adds.")));

        return Object(
            "A time clock policy: how punches become paid hours.",
            ("itemId", ItemId("policy")),
            ("name", Text("The policy name, unique.")),
            ("description", Text("What the policy is for.")),
            ("payPeriodsName", Text("The pay period calendar, by name. Wins over payPeriodCalendarId.")),
            ("payPeriodCalendarId", Text("The pay period calendar, by id.")),
            ("teamNames", Strings("The teams that follow the policy, by name; each team's time clock policy is set to it.")),
            ("roundingMinutes", Integer("The minutes punches are rounded to; 0 for none.")),
            ("roundingDirection", Enum<TimeRoundingDirection>("How punches are rounded. Defaults to Nearest.")),
            ("earlyGraceMinutes", Integer("The minutes before the shift a punch-in counts as on time. Defaults to 5.")),
            ("lateGraceMinutes", Integer("The minutes after the shift starts a punch-in counts as on time. Defaults to 5.")),
            ("blockEarlyPunchInMinutes", OptionalInteger("Refuses a punch-in more than these minutes before the shift; null to allow it.")),
            ("autoMealAfterHours", OptionalNumber("Takes a meal off a day longer than these hours that has none; null for never.")),
            ("autoMealMinutes", Integer("The length of that meal in minutes. Defaults to 30.")),
            ("longMealMinutes", OptionalInteger("A meal longer than these minutes is an exception; null for never.")),
            ("allowedSources", Array(new JsonSchemaBuilder().Type(SchemaValueType.String).Enum(TimePunchSource.Web, TimePunchSource.Mobile, TimePunchSource.Kiosk, TimePunchSource.Api, TimePunchSource.Manager), "Where workers may punch from; empty for anywhere. A manager can always punch for a worker.")),
            ("requireGeofence", Flag("Whether a mobile punch must be near the team's location.")),
            ("geofenceRadiusMeters", Integer("How near, in meters. Defaults to 200.")),
            ("missedPunchHandling", Enum<MissedPunchHandling>("What happens to a shift with no punch-out. Defaults to Flag.")),
            ("dailyOvertimeHours", OptionalNumber("Hours a day after which time is overtime; null for none.")),
            ("dailyDoubleTimeHours", OptionalNumber("Hours a day after which time is double time; null for none.")),
            ("weeklyOvertimeHours", OptionalNumber("Hours a week after which time is overtime; null for none. Defaults to 40.")),
            ("seventhDayOvertime", Flag("Whether the seventh day in a row worked is overtime.")),
            ("holidays", Dates("The paid holidays.")),
            ("weekendDays", EnumArray<DayOfWeek>("The days paid as weekend.")),
            ("attendancePoints", Array(points, "The points each kind of exception adds.")),
            ("pointsWindowDays", Integer("The days points count for. Defaults to 365.")),
            ("pointsThreshold", OptionalNumber("The points that raise the threshold event; null for none.")),
            ("enabled", Flag("Whether the policy is in use. Defaults to true.")))
        .Required("name");
    }

    private static JsonSchemaBuilder Kiosk()
        => Object(
            "A shared device workers punch on.",
            ("itemId", ItemId("kiosk")),
            ("name", Text("The kiosk name, unique.")),
            ("teamName", Text("The team whose workers punch on it, by name; null for every team.")),
            ("enabled", Flag("Whether the kiosk accepts punches. Defaults to true.")))
        .Required("name");

    private static JsonSchemaBuilder PayrollCsv()
    {
        var column = Object(
            "A column of the payroll CSV file.",
            ("header", Text("The column header. A column without one is dropped.")),
            ("field", Enum<PayrollCsvField>("What the column holds.")));

        return Object(
            "The payroll CSV file layout. When present, its columns replace the stored ones.",
            ("columns", Array(column, "The columns, in order.")));
    }
}
