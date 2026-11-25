using CrestApps.Core.AI;
using CrestApps.OrchardCore.Workforce.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Adds the Workforce AI tools: who is working, a person's own schedule, and finding cover.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI")]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<WhoIsWorkingTool>(WhoIsWorkingTool.ToolName)
            .WithTitle(S["Who is working"])
            .WithDescription(S["Lists who is on shift at a time in the teams the user manages."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<MyScheduleTool>(MyScheduleTool.ToolName)
            .WithTitle(S["My schedule"])
            .WithDescription(S["Lists the user's own shifts."])
            .WithCategory(S["Workforce"])
            .Selectable();
    }
}

/// <summary>
/// Adds the shift planning AI tools: finding cover and explaining labor rule problems.
/// </summary>
[Feature(WorkforceConstants.Features.ShiftPlanning)]
[RequireFeatures("CrestApps.OrchardCore.AI")]
public sealed class ShiftPlanningAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShiftPlanningAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ShiftPlanningAIToolsStartup(IStringLocalizer<ShiftPlanningAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<FindCoverTool>(FindCoverTool.ToolName)
            .WithTitle(S["Find cover"])
            .WithDescription(S["Finds colleagues who can take a worker's shift."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<ExplainViolationsTool>(ExplainViolationsTool.ToolName)
            .WithTitle(S["Explain schedule problems"])
            .WithDescription(S["Lists the labor rule problems of a team's draft schedule."])
            .WithCategory(S["Workforce"])
            .Selectable();
    }
}

/// <summary>
/// Adds the time-off AI tools: balances and asking for time off.
/// </summary>
[Feature(WorkforceConstants.Features.TimeOff)]
[RequireFeatures("CrestApps.OrchardCore.AI")]
public sealed class TimeOffAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeOffAIToolsStartup(IStringLocalizer<TimeOffAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<TimeOffBalanceTool>(TimeOffBalanceTool.ToolName)
            .WithTitle(S["My time-off balance"])
            .WithDescription(S["Gives the user's own time-off balances."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<RequestTimeOffTool>(RequestTimeOffTool.ToolName)
            .WithTitle(S["Request time off"])
            .WithDescription(S["Asks for time off for the user."])
            .WithCategory(S["Workforce"])
            .Selectable();
    }
}

/// <summary>
/// Adds the time-off administration AI tools: the request queue, decisions, a worker's balances, balance adjustments
/// and policy assignments. They work only for a signed-in person with the permission of the matching admin page, in
/// the teams that person manages.
/// </summary>
[Feature(WorkforceConstants.Features.TimeOff)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class TimeOffAdminAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffAdminAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeOffAdminAIToolsStartup(IStringLocalizer<TimeOffAdminAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListTimeOffRequestsTool>(ListTimeOffRequestsTool.ToolName)
            .WithTitle(S["List time-off requests"])
            .WithDescription(S["Lists time-off requests of the teams the user manages, filtered by team, worker, status and dates, with the days asked for and the balance left. Use it for \"what is waiting for me\"."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<DecideTimeOffRequestTool>(DecideTimeOffRequestTool.ToolName)
            .WithTitle(S["Decide time-off request"])
            .WithDescription(S["Approves, declines or cancels a time-off request, with a note. Approving books the days, puts them on the team calendar and handles the worker's shifts by the team's rule; the worker is notified."])
            .WithCategory(S["Workforce"])
            .Selectable()
            .AsExternal();

        services.AddCoreAITool<GetWorkerTimeOffTool>(GetWorkerTimeOffTool.ToolName)
            .WithTitle(S["Get worker time off"])
            .WithDescription(S["Gets a worker's time-off picture: the policies assigned, the balance of each (earned, booked, pending, available) and the recent ledger entries."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<AdjustTimeOffBalanceTool>(AdjustTimeOffBalanceTool.ToolName)
            .WithTitle(S["Adjust time-off balance"])
            .WithDescription(S["Adds to or takes from a worker's time-off balance for a policy on a date, with a note and an optional expiry. It is written to the ledger and cannot be edited afterwards, only offset."])
            .WithCategory(S["Workforce"])
            .Selectable()
            .AsDestructive();

        services.AddCoreAITool<AssignTimeOffPolicyTool>(AssignTimeOffPolicyTool.ToolName)
            .WithTitle(S["Assign time-off policy"])
            .WithDescription(S["Assigns a time-off policy to a worker from a date, or takes it away. A worker without a policy for a type cannot ask for that type."])
            .WithCategory(S["Workforce"])
            .Selectable();
    }
}

/// <summary>
/// Adds the time clock administration AI tools: timesheets, punches, manual lines, punch corrections, attendance
/// exceptions and a worker's time clock setup. They work only for a signed-in person with the permission of the
/// matching admin page, in the teams that person manages. No tool reads or sets a kiosk PIN or a kiosk address, locks a
/// pay period, exports payroll or changes pay rates.
/// </summary>
[Feature(WorkforceConstants.Features.TimeClock)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class TimeClockAdminAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeClockAdminAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeClockAdminAIToolsStartup(IStringLocalizer<TimeClockAdminAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListTimesheetsTool>(ListTimesheetsTool.ToolName)
            .WithTitle(S["List timesheets"])
            .WithDescription(S["Lists timesheets filtered by pay period, team, worker and status, with each one's total and worked hours and open exceptions."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<GetTimesheetTool>(GetTimesheetTool.ToolName)
            .WithTitle(S["Get timesheet"])
            .WithDescription(S["Gets one timesheet (by id, or a worker and a date): each day's punches and hours, the totals by pay code, manual lines, attendance exceptions and pending punch corrections."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<PrepareTimesheetsTool>(PrepareTimesheetsTool.ToolName)
            .WithTitle(S["Prepare timesheets"])
            .WithDescription(S["Creates the timesheets of a pay period for the workers of a team (and its sub-teams) who do not have one yet, so the period can be reviewed."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<DecideTimesheetTool>(DecideTimesheetTool.ToolName)
            .WithTitle(S["Decide timesheet"])
            .WithDescription(S["Approves one or more timesheets, sends one back to the worker with a reason, or reopens an approved one. The worker is notified."])
            .WithCategory(S["Workforce"])
            .Selectable()
            .AsExternal();

        services.AddCoreAITool<SaveTimesheetPunchTool>(SaveTimesheetPunchTool.ToolName)
            .WithTitle(S["Save timesheet punch"])
            .WithDescription(S["Adds a missing punch to a worker's timesheet or corrects the kind or time of one, with a reason that is kept. It changes the hours the worker is paid for."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<RemoveTimesheetPunchTool>(RemoveTimesheetPunchTool.ToolName)
            .WithTitle(S["Remove timesheet punch"])
            .WithDescription(S["Removes a punch from a worker's timesheet, with a reason that is kept."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<AddTimesheetLineTool>(AddTimesheetLineTool.ToolName)
            .WithTitle(S["Add timesheet line"])
            .WithDescription(S["Adds a manual line to a timesheet: a pay code (such as a bonus or an allowance), hours on a date and a note."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<RemoveTimesheetLineTool>(RemoveTimesheetLineTool.ToolName)
            .WithTitle(S["Remove timesheet line"])
            .WithDescription(S["Removes a manual line from a timesheet."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<DecidePunchCorrectionTool>(DecidePunchCorrectionTool.ToolName)
            .WithTitle(S["Decide punch correction"])
            .WithDescription(S["Approves or declines a worker's request to add or correct a punch. Approving applies it to the timesheet."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<ListAttendanceExceptionsTool>(ListAttendanceExceptionsTool.ToolName)
            .WithTitle(S["List attendance exceptions"])
            .WithDescription(S["Lists attendance exceptions (late, left early, absent, missed punch) filtered by team, worker, kind, status and dates, with the worker's current attendance points."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<DecideAttendanceExceptionTool>(DecideAttendanceExceptionTool.ToolName)
            .WithTitle(S["Decide attendance exception"])
            .WithDescription(S["Confirms one or more attendance exceptions (which adds their points) or excuses them with a note."])
            .WithCategory(S["Workforce"])
            .Selectable();

        services.AddCoreAITool<GetWorkerTimeClockTool>(GetWorkerTimeClockTool.ToolName)
            .WithTitle(S["Get worker time clock"])
            .WithDescription(S["Gets a worker's time clock setup: the policy that applies, whether they have a kiosk PIN, whether they are clocked in, and their attendance points."])
            .WithCategory(S["Workforce"])
            .Selectable();
    }
}
