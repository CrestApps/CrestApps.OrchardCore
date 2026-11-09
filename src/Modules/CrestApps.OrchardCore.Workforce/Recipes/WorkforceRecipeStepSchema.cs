using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Workforce.Models;
using Json.Schema;
using static CrestApps.OrchardCore.Workforce.Recipes.WorkforceSchemaBuilder;

namespace CrestApps.OrchardCore.Workforce.Recipes;

/// <summary>
/// Describes the Workforce recipe step to CrestApps Recipes.
/// </summary>
internal sealed class WorkforceRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => WorkforceConstants.RecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= Step(
            WorkforceConstants.RecipeStepName,
            "Creates or updates activity codes, teams and workers. Activity codes and teams are matched by itemId, then by name; workers by itemId, then employee number, then user. An activity code or a worker that is updated takes every member of the entry, so a member left out goes back to its default.",
            ("ActivityCodes", Array(ActivityCode(), "The activity codes to create or update.")),
            ("Teams", Array(Team(), "The teams to create or update, in any order: a team may name a parent listed after it.")),
            ("Workers", Array(Worker(), "The workers to create or update. Their teams must exist or be listed in Teams.")));

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder ActivityCode()
        => Object(
            "An activity code: a kind of time on a schedule.",
            ("itemId", ItemId("activity code")),
            ("name", Text("The activity name, unique.")),
            ("code", Text("A short code shown on schedules.")),
            ("kind", Enum<ActivityKind>("What kind of time it is. Defaults to Work.")),
            ("color", Text("The color, such as #2b8a3e.")),
            ("isPaid", Flag("Whether the time is paid. Defaults to true.")),
            ("countsAsWork", Flag("Whether the time counts toward hours rules. Defaults to true.")),
            ("countsTowardCoverage", Flag("Whether the worker counts toward staffing during it. Defaults to true.")),
            ("enabled", Flag("Whether the code can be scheduled. Defaults to true.")),
            ("stageId", Text("The id of the Time Tracker stage time clocked in this activity is kept in, when the time clock keeps time in Time Tracker.")))
        .Required("name");

    private static JsonSchemaBuilder Team()
        => Object(
            "A team with its Workforce settings. The generic members are those of the Teams step; the Workforce settings sit beside them. The team's calendar is never imported.",
            ("itemId", ItemId("team")),
            ("name", Text("The team name, unique.")),
            ("kind", Enum<TeamKind>("What the team is in the organization.")),
            ("parentName", Text("The name of the team it belongs to, which may be listed after it. Wins over parentId.")),
            ("parentId", Text("The id of the team it belongs to.")),
            ("description", Text("What the team does.")),
            ("linkedRoleName", Text("A role whose members can read the team's calendar.")),
            ("timeZoneId", Text("The team's IANA time zone, such as America/Chicago.")),
            ("enabled", Flag("Whether the team is in use.")),
            ("location", Location("Where the team works from.")),
            ("leadWorkerId", Text("The id of the worker who leads the crew.")),
            ("openShiftClaimPolicy", OptionalEnum<OpenShiftClaimPolicy>("How the team's open shifts are given out; null uses the parent team's policy.")),
            ("shiftTradesNeedApproval", OptionalFlag("Whether a manager approves shift trades once a colleague accepts; null uses the parent team's choice.")),
            ("timeOffShiftHandling", OptionalEnum<TimeOffShiftHandling>("What happens to a worker's published shifts when their time off is approved; null uses the parent team's choice.")),
            ("timeClockPolicyId", Text("The id of the time clock policy the team follows.")))
        .Required("name");

    private static JsonSchemaBuilder Worker()
    {
        var membership = Object(
            "A team the worker belongs to.",
            ("teamName", Text("The team, by name. Wins over teamId.")),
            ("teamId", Text("The team, by id.")),
            ("role", Enum<TeamMemberRole>("The worker's role in the team. Defaults to Member.")),
            ("effectiveFrom", OptionalDate("The first day of the membership; null for always.")),
            ("effectiveTo", OptionalDate("The last day of the membership; null for open-ended.")));

        var availability = Object(
            "A weekly window of availability.",
            ("day", Enum<DayOfWeek>("The day of the week.")),
            ("start", Time("When the window starts.")),
            ("end", Time("When the window ends.")),
            ("preference", Enum<AvailabilityPreference>("Whether the worker is unavailable, available or prefers to work then. Defaults to Available.")));

        return Object(
            "A worker profile.",
            ("itemId", Text("The worker's id. When it matches a stored worker, that worker is updated; otherwise the worker is matched by employee number, then by user, and a new one keeps this id.")),
            ("displayName", Text("The worker's name.")),
            ("userId", Text("The id of the user the worker signs in as.")),
            ("email", Text("The email address.")),
            ("phone", Text("The phone number.")),
            ("employeeNumber", Text("The employee number, unique when set.")),
            ("externalId", Text("The worker's id in a payroll or HR system.")),
            ("jobTitle", Text("The job title.")),
            ("employmentType", Enum<EmploymentType>("The kind of employment. Defaults to FullTime.")),
            ("status", Enum<WorkerStatus>("The employment status. Defaults to Active.")),
            ("hireDate", OptionalDate("The hire date.")),
            ("seniorityDate", OptionalDate("The date seniority counts from, when not the hire date.")),
            ("terminationDate", OptionalDate("The last day of employment.")),
            ("dateOfBirth", OptionalDate("The date of birth, used by the minor labor rules.")),
            ("managerWorkerId", Text("The id of the worker this worker reports to.")),
            ("primaryTeamId", Text("The id of the worker's main team.")),
            ("teamMemberships", Array(membership, "The teams the worker belongs to.")),
            ("timeZoneId", Text("The worker's IANA time zone, when not the team's.")),
            ("homeBase", Location("Where the worker starts and ends the day.")),
            ("contractedHoursPerWeek", OptionalNumber("The hours a week the worker is contracted for.")),
            ("minHoursPerWeek", OptionalNumber("The fewest hours a week the worker should be scheduled.")),
            ("maxHoursPerWeek", OptionalNumber("The most hours a week the worker may be scheduled.")),
            ("maxHoursPerDay", OptionalNumber("The most hours a day the worker may be scheduled.")),
            ("availability", Array(availability, "The worker's weekly availability.")))
        .Required("displayName");
    }
}
