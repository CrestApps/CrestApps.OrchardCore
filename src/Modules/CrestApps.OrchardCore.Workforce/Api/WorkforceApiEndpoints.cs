using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Workforce.Controllers;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Workforce.Api;

/// <summary>
/// The Workforce HTTP API at <c>api/workforce/v1</c>: the caller's own schedule, balances, time off, clock and
/// timesheet under <c>me</c>, and teams, workers, shifts, time off, punches and timesheets of the teams the caller
/// manages. A POST that records something takes an <c>Idempotency-Key</c> header.
/// </summary>
internal static class WorkforceApiEndpoints
{
    public const string RoutePrefix = "api/workforce/v1";

    private const int MaxDays = 62;

    public static IEndpointRouteBuilder MapWorkforceApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutePrefix)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = OrchardCoreConstants.AuthenticationSchemes.Api })
            .DisableAntiforgery()
            .WithTags("Workforce");

        group.MapGet("me", MeAsync).WithName("WorkforceApiMe").WithSummary("Gets the caller's worker and teams.");
        group.MapGet("me/schedule", MyScheduleAsync).WithName("WorkforceApiMySchedule").WithSummary("Lists the caller's published shifts.");
        group.MapGet("me/balances", MyBalancesAsync).WithName("WorkforceApiMyBalances").WithSummary("Lists the caller's time-off balances.");
        group.MapGet("me/time-off", MyTimeOffAsync).WithName("WorkforceApiMyTimeOff").WithSummary("Lists the caller's time-off requests.");
        group.MapPost("me/time-off", RequestTimeOffAsync).WithName("WorkforceApiRequestTimeOff").WithSummary("Asks for time off for the caller.");
        group.MapGet("me/clock", MyClockAsync).WithName("WorkforceApiMyClock").WithSummary("Gets where the caller stands on the time clock.");
        group.MapPost("me/punch", PunchAsync).WithName("WorkforceApiPunch").WithSummary("Records the caller's punch, once per Idempotency-Key.");
        group.MapGet("me/timesheet", MyTimesheetAsync).WithName("WorkforceApiMyTimesheet").WithSummary("Gets the caller's timesheet for the pay period a day falls in.");

        group.MapGet("teams", ListTeamsAsync).WithName("WorkforceApiTeams").WithSummary("Lists the teams the caller manages.");
        group.MapGet("workers", ListWorkersAsync).WithName("WorkforceApiWorkers").WithSummary("Lists the workers of a team the caller manages.");
        group.MapGet("shifts", ListShiftsAsync).WithName("WorkforceApiShifts").WithSummary("Lists the published shifts of the teams the caller manages between two days.");
        group.MapGet("time-off/requests", ListTimeOffAsync).WithName("WorkforceApiTimeOffRequests").WithSummary("Lists the time-off requests of the teams the caller manages.");
        group.MapPost("time-off/requests/{id}/approve", ApproveTimeOffAsync).WithName("WorkforceApiApproveTimeOff").WithSummary("Approves time off.");
        group.MapPost("time-off/requests/{id}/decline", DeclineTimeOffAsync).WithName("WorkforceApiDeclineTimeOff").WithSummary("Declines time off.");
        group.MapPost("punches", RecordPunchAsync).WithName("WorkforceApiRecordPunch").WithSummary("Records a worker's punch from another system, once per Idempotency-Key.");
        group.MapGet("timesheets", ListTimesheetsAsync).WithName("WorkforceApiTimesheets").WithSummary("Lists the timesheets of the teams the caller manages.");
        group.MapPost("timesheets/{id}/approve", ApproveTimesheetAsync).WithName("WorkforceApiApproveTimesheet").WithSummary("Approves a timesheet.");

        return routes;
    }

    internal static async Task<IResult> MeAsync(HttpContext httpContext, IServiceProvider services)
    {
        var worker = await WorkerAsync(httpContext, services);

        if (worker is null)
        {
            return Results.NotFound();
        }

        var teams = (await services.GetRequiredService<ITeamManager>().GetAllAsync()).ToDictionary(team => team.ItemId, team => team.Name, StringComparer.Ordinal);

        return Results.Ok(new
        {
            id = worker.ItemId,
            name = worker.DisplayName,
            employeeNumber = worker.EmployeeNumber,
            teams = worker.TeamMemberships.Select(membership => new { id = membership.TeamId, name = teams.GetValueOrDefault(membership.TeamId ?? string.Empty), role = membership.Role.ToString() }),
        });
    }

    internal static async Task<IResult> MyScheduleAsync(string from, int? days, HttpContext httpContext, IServiceProvider services)
    {
        var worker = await WorkerAsync(httpContext, services);

        if (worker is null)
        {
            return Results.NotFound();
        }

        var start = Day(from) ?? await PlanningOptions.TodayAsync(services.GetRequiredService<ILocalClock>());
        var count = Math.Clamp(days ?? 7, 1, MaxDays);
        var teams = (await services.GetRequiredService<ITeamManager>().GetAllAsync()).ToDictionary(team => team.ItemId, team => team.Name, StringComparer.Ordinal);
        var shifts = await services.GetRequiredService<IShiftService>().ListWorkerShiftsAsync(
            worker.ItemId,
            start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(-1),
            start.AddDays(count + 1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            httpContext.RequestAborted);

        return Results.Ok(shifts
            .Where(shift => DateOnly.FromDateTime(shift.Start) >= start && DateOnly.FromDateTime(shift.Start) < start.AddDays(count))
            .OrderBy(shift => shift.StartUtc)
            .Select(shift => Shift(shift, teams, null)));
    }

    internal static async Task<IResult> MyBalancesAsync(HttpContext httpContext, IServiceProvider services)
    {
        var balances = services.GetService<ITimeOffBalanceService>();
        var worker = await WorkerAsync(httpContext, services);

        if (balances is null || worker is null)
        {
            return Results.NotFound();
        }

        var today = await PlanningOptions.TodayAsync(services.GetRequiredService<ILocalClock>());

        return Results.Ok((await balances.GetBalancesAsync(worker.ItemId, today, httpContext.RequestAborted)).Select(balance => new
        {
            policyId = balance.Policy?.ItemId,
            type = balance.Type?.Name,
            unit = balance.Type?.Unit.ToString(),
            current = balance.Current,
            booked = balance.Upcoming,
            pending = balance.Pending,
            available = balance.Available,
        }));
    }

    internal static async Task<IResult> MyTimeOffAsync(HttpContext httpContext, IServiceProvider services)
    {
        var requests = services.GetService<ITimeOffRequestService>();
        var worker = await WorkerAsync(httpContext, services);

        if (requests is null || worker is null)
        {
            return Results.NotFound();
        }

        var (_, items) = await requests.PageAsync(new TimeOffRequestQuery { WorkerId = worker.ItemId }, 0, 100, httpContext.RequestAborted);

        return Results.Ok(items.Select(TimeOff));
    }

    internal static async Task<IResult> RequestTimeOffAsync(WorkforceTimeOffInput input, HttpContext httpContext, IServiceProvider services)
    {
        var requests = services.GetService<ITimeOffRequestService>();
        var worker = await WorkerAsync(httpContext, services);

        if (requests is null || worker is null)
        {
            return Results.NotFound();
        }

        if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(httpContext.User, WorkforcePermissions.RequestTimeOff))
        {
            return Results.Forbid();
        }

        if (input is null || string.IsNullOrEmpty(input.TypeId) || !input.StartDate.HasValue)
        {
            return Problem("Give the time-off type and the first day.");
        }

        var result = await requests.SubmitAsync(new TimeOffRequestInput
        {
            WorkerId = worker.ItemId,
            TypeId = input.TypeId,
            StartDate = input.StartDate.Value,
            EndDate = input.EndDate ?? input.StartDate.Value,
            StartTime = input.StartTime,
            EndTime = input.EndTime,
            Reason = input.Reason,
        }, httpContext.User.Identity?.Name, httpContext.RequestAborted);

        return result.Succeeded ? Results.Ok(TimeOff(result.Value)) : Problem(result.Errors);
    }

    internal static async Task<IResult> MyClockAsync(HttpContext httpContext, IServiceProvider services)
    {
        var timeClock = services.GetService<ITimeClockService>();
        var worker = await WorkerAsync(httpContext, services);

        if (timeClock is null || worker is null)
        {
            return Results.NotFound();
        }

        var status = await timeClock.GetStatusAsync(worker, httpContext.RequestAborted);

        return Results.Ok(new
        {
            state = status.State.ToString(),
            sinceUtc = status.SinceUtc,
            next = status.NextPunches.Select(kind => kind.ToString()),
            needsLocation = status.NeedsLocation,
            hasPolicy = status.Policy is not null,
        });
    }

    internal static async Task<IResult> PunchAsync(WorkforcePunchInput input, HttpContext httpContext, IServiceProvider services)
    {
        var timeClock = services.GetService<ITimeClockService>();
        var worker = await WorkerAsync(httpContext, services);

        if (timeClock is null || worker is null)
        {
            return Results.NotFound();
        }

        if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(httpContext.User, WorkforcePermissions.PunchTime))
        {
            return Results.Forbid();
        }

        if (input is null)
        {
            return Problem("Give the punch.");
        }

        var result = await timeClock.PunchAsync(new TimePunchRequest
        {
            WorkerId = worker.ItemId,
            Kind = input.Kind,
            Utc = input.Utc,
            Source = TimePunchSource.Api,
            Latitude = input.Latitude,
            Longitude = input.Longitude,
            ActivityCodeId = input.ActivityCodeId,
            Job = input.Job,
            DeviceId = input.DeviceId,
            IdempotencyKey = Key(httpContext, "me"),
        }, httpContext.User.Identity?.Name, httpContext.RequestAborted);

        return result.Succeeded ? Results.Ok(Punch(result.Value)) : Problem(result.Errors);
    }

    internal static async Task<IResult> MyTimesheetAsync(string date, HttpContext httpContext, IServiceProvider services)
    {
        var timesheets = services.GetService<ITimesheetService>();
        var worker = await WorkerAsync(httpContext, services);

        if (timesheets is null || worker is null)
        {
            return Results.NotFound();
        }

        var result = await timesheets.GetForDateAsync(worker, Day(date) ?? await PlanningOptions.TodayAsync(services.GetRequiredService<ILocalClock>()), httpContext.RequestAborted);

        return result.Succeeded ? Results.Ok(Timesheet(result.Value)) : Problem(result.Errors);
    }

    internal static async Task<IResult> ListTeamsAsync(HttpContext httpContext, IServiceProvider services)
    {
        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(httpContext.User, httpContext.RequestAborted);

        return Results.Ok((await services.GetRequiredService<ITeamManager>().ListTreeAsync(httpContext.RequestAborted))
            .Where(entry => scope.Covers(entry.Team.ItemId))
            .Select(entry => new { id = entry.Team.ItemId, name = entry.Team.Name, kind = entry.Team.Kind.ToString(), parentId = entry.Team.ParentId, timeZoneId = entry.Team.TimeZoneId }));
    }

    internal static async Task<IResult> ListWorkersAsync(string teamId, HttpContext httpContext, IServiceProvider services)
    {
        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(httpContext.User, httpContext.RequestAborted);

        if (string.IsNullOrEmpty(teamId) || !scope.Covers(teamId))
        {
            return Results.Forbid();
        }

        var today = await PlanningOptions.TodayAsync(services.GetRequiredService<ILocalClock>());

        return Results.Ok((await services.GetRequiredService<IWorkerManager>().ListByTeamAsync(teamId, today, httpContext.RequestAborted))
            .Select(worker => new { id = worker.ItemId, name = worker.DisplayName, employeeNumber = worker.EmployeeNumber, jobTitle = worker.JobTitle, status = worker.Status.ToString() }));
    }

    internal static async Task<IResult> ListShiftsAsync(string teamId, string from, string to, HttpContext httpContext, IServiceProvider services)
    {
        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(httpContext.User, httpContext.RequestAborted);
        var start = Day(from);
        var end = Day(to);

        if (!start.HasValue || !end.HasValue || end <= start || end.Value.DayNumber - start.Value.DayNumber > MaxDays)
        {
            return Problem($"Give from and to as yyyy-MM-dd days, to after from, at most {MaxDays} days apart.");
        }

        if (!string.IsNullOrEmpty(teamId) && !scope.Covers(teamId))
        {
            return Results.Forbid();
        }

        var teams = (await services.GetRequiredService<ITeamManager>().GetAllAsync()).Where(team => scope.Covers(team.ItemId) && (string.IsNullOrEmpty(teamId) || team.ItemId == teamId)).ToList();
        var workers = services.GetRequiredService<IWorkerManager>();
        var siteService = services.GetRequiredService<ISiteService>();
        var queries = services.GetRequiredService<ICalendarQueryService>();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var shifts = new List<object>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var team in teams.Where(team => !string.IsNullOrEmpty(team.CalendarId)))
        {
            var zone = await PlanningOptions.TimeZoneOfAsync(team, siteService);
            var occurrences = await queries.GetOccurrencesAsync(new CalendarOccurrenceQuery
            {
                CalendarIds = [team.CalendarId],
                FromUtc = CalendarTime.ToUtc(start.Value.ToDateTime(TimeOnly.MinValue), zone),
                ToUtc = CalendarTime.ToUtc(end.Value.ToDateTime(TimeOnly.MinValue), zone),
                EventTypes = [WorkforceConstants.EventTypes.Shift],
                Sources = [WorkforceConstants.Source],
            }, httpContext.RequestAborted);

            foreach (var occurrence in occurrences.Where(item => item.Event.Status != CalendarEventStatus.Cancelled && seen.Add(item.Event.ItemId)))
            {
                ShiftEvents.TryRead(occurrence.Event, out var data);

                var workerId = data?.WorkerId ?? string.Empty;

                if (!names.TryGetValue(workerId, out var workerName))
                {
                    names[workerId] = workerName = (await workers.FindByIdAsync(workerId))?.DisplayName;
                }

                shifts.Add(new
                {
                    id = occurrence.Event.ItemId,
                    teamId = team.ItemId,
                    team = team.Name,
                    workerId = data?.WorkerId,
                    worker = workerName,
                    start = Local(CalendarTime.ToLocal(occurrence.StartUtc, zone)),
                    end = Local(CalendarTime.ToLocal(occurrence.EndUtc, zone)),
                    startUtc = occurrence.StartUtc,
                    endUtc = occurrence.EndUtc,
                });
            }
        }

        return Results.Ok(shifts);
    }

    internal static async Task<IResult> ListTimeOffAsync(string teamId, string status, HttpContext httpContext, IServiceProvider services)
    {
        var requests = services.GetService<ITimeOffRequestService>();

        if (requests is null)
        {
            return Results.NotFound();
        }

        var (teamIds, denied) = await ManagedTeamsAsync(teamId, WorkforcePermissions.ApproveTimeOff, httpContext, services);

        if (denied is not null)
        {
            return denied;
        }

        var (_, items) = await requests.PageAsync(new TimeOffRequestQuery
        {
            TeamIds = teamIds,
            Status = Enum.TryParse<TimeOffRequestStatus>(status, true, out var parsed) ? parsed : null,
        }, 0, 200, httpContext.RequestAborted);

        return Results.Ok(items.Select(TimeOff));
    }

    internal static Task<IResult> ApproveTimeOffAsync(string id, HttpContext httpContext, IServiceProvider services)
    {
        return DecideTimeOffAsync(id, httpContext, services, (requests, actor) => requests.ApproveAsync(id, actor, null, httpContext.RequestAborted));
    }

    internal static Task<IResult> DeclineTimeOffAsync(string id, WorkforceDecisionInput input, HttpContext httpContext, IServiceProvider services)
    {
        return DecideTimeOffAsync(id, httpContext, services, (requests, actor) => requests.DeclineAsync(id, actor, input?.Note, httpContext.RequestAborted));
    }

    internal static async Task<IResult> RecordPunchAsync(WorkforcePunchInput input, HttpContext httpContext, IServiceProvider services)
    {
        var timeClock = services.GetService<ITimeClockService>();

        if (timeClock is null)
        {
            return Results.NotFound();
        }

        if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(httpContext.User, WorkforcePermissions.EditPunches))
        {
            return Results.Forbid();
        }

        var worker = string.IsNullOrEmpty(input?.WorkerId) ? null : await services.GetRequiredService<IWorkerManager>().FindByIdAsync(input.WorkerId);

        if (worker is null)
        {
            return Problem("Give the worker's id.");
        }

        if (!await services.GetRequiredService<IWorkforceScopeService>().ManagesWorkerAsync(httpContext.User, worker, httpContext.RequestAborted))
        {
            return Results.Forbid();
        }

        var result = await timeClock.PunchAsync(new TimePunchRequest
        {
            WorkerId = worker.ItemId,
            Kind = input.Kind,
            Utc = input.Utc,
            Source = input.Source is TimePunchSource.ContactCenter or TimePunchSource.Kiosk or TimePunchSource.Manager ? input.Source.Value : TimePunchSource.Api,
            Latitude = input.Latitude,
            Longitude = input.Longitude,
            ActivityCodeId = input.ActivityCodeId,
            Job = input.Job,
            DeviceId = input.DeviceId,
            Reason = input.Reason,
            IdempotencyKey = Key(httpContext, "api"),
        }, httpContext.User.Identity?.Name, httpContext.RequestAborted);

        return result.Succeeded ? Results.Ok(Punch(result.Value)) : Problem(result.Errors);
    }

    internal static async Task<IResult> ListTimesheetsAsync(string teamId, string periodStart, string status, HttpContext httpContext, IServiceProvider services)
    {
        var timesheets = services.GetService<ITimesheetService>();

        if (timesheets is null)
        {
            return Results.NotFound();
        }

        var (teamIds, denied) = await ManagedTeamsAsync(teamId, WorkforcePermissions.ApproveTimesheets, httpContext, services);

        if (denied is not null)
        {
            return denied;
        }

        var (_, items) = await timesheets.PageAsync(new TimesheetQuery
        {
            TeamIds = teamIds,
            PeriodStart = Day(periodStart),
            Statuses = Enum.TryParse<TimesheetStatus>(status, true, out var parsed) ? [parsed] : [],
        }, 0, 500, httpContext.RequestAborted);

        return Results.Ok(items.Select(Timesheet));
    }

    internal static async Task<IResult> ApproveTimesheetAsync(string id, HttpContext httpContext, IServiceProvider services)
    {
        var timesheets = services.GetService<ITimesheetService>();
        var timesheet = timesheets is null ? null : await timesheets.FindAsync(id, httpContext.RequestAborted);

        if (timesheet is null)
        {
            return Results.NotFound();
        }

        var (_, denied) = await ManagedTeamsAsync(timesheet.TeamId, WorkforcePermissions.ApproveTimesheets, httpContext, services);

        if (denied is not null)
        {
            return denied;
        }

        var result = await timesheets.ApproveAsync(id, httpContext.User.Identity?.Name, httpContext.RequestAborted);

        return result.Succeeded ? Results.Ok(Timesheet(result.Value)) : Problem(result.Errors);
    }

    private static async Task<IResult> DecideTimeOffAsync(string id, HttpContext httpContext, IServiceProvider services, Func<ITimeOffRequestService, string, Task<PlanningResult<TimeOffRequest>>> decide)
    {
        var requests = services.GetService<ITimeOffRequestService>();
        var request = requests is null ? null : await requests.FindAsync(id, httpContext.RequestAborted);

        if (request is null)
        {
            return Results.NotFound();
        }

        var (_, denied) = await ManagedTeamsAsync(request.TeamId, WorkforcePermissions.ApproveTimeOff, httpContext, services);

        if (denied is not null)
        {
            return denied;
        }

        var result = await decide(requests, httpContext.User.Identity?.Name);

        return result.Succeeded ? Results.Ok(TimeOff(result.Value)) : Problem(result.Errors);
    }

    /// <summary>
    /// Checks the permission and returns the teams to read: the one asked for, which the caller must manage, or all
    /// the caller manages.
    /// </summary>
    private static async Task<(IReadOnlyCollection<string> TeamIds, IResult Denied)> ManagedTeamsAsync(string teamId, global::OrchardCore.Security.Permissions.Permission permission, HttpContext httpContext, IServiceProvider services)
    {
        if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(httpContext.User, permission))
        {
            return (null, Results.Forbid());
        }

        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(httpContext.User, httpContext.RequestAborted);

        if (!string.IsNullOrEmpty(teamId))
        {
            return scope.Covers(teamId) ? ([teamId], null) : (null, Results.Forbid());
        }

        return (scope.All ? null : scope.TeamIds.ToArray(), null);
    }

    private static Task<WorkerProfile> WorkerAsync(HttpContext httpContext, IServiceProvider services)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrEmpty(userId) ? Task.FromResult<WorkerProfile>(null) : services.GetRequiredService<IWorkerManager>().FindByUserIdAsync(userId);
    }

    private static string Key(HttpContext httpContext, string prefix)
    {
        var key = httpContext.Request.Headers["Idempotency-Key"].ToString();

        return string.IsNullOrWhiteSpace(key) ? null : prefix + ":" + key.Trim();
    }

    private static DateOnly? Day(string value)
    {
        return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;
    }

    private static string Local(DateTime value)
    {
        return value.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
    }

    private static IResult Problem(params IEnumerable<string> errors)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { [string.Empty] = [.. errors] });
    }

    private static object Shift(CalendarEvent shift, IReadOnlyDictionary<string, string> teams, string workerName)
    {
        ShiftEvents.TryRead(shift, out var data);

        return new
        {
            id = shift.ItemId,
            teamId = data?.TeamId,
            team = teams.GetValueOrDefault(data?.TeamId ?? string.Empty),
            worker = workerName,
            start = Local(shift.Start),
            end = Local(shift.End),
            startUtc = shift.StartUtc,
            endUtc = shift.EndUtc,
        };
    }

    private static object TimeOff(TimeOffRequest request)
    {
        return new
        {
            id = request.ItemId,
            workerId = request.WorkerId,
            teamId = request.TeamId,
            typeId = request.TypeId,
            startDate = request.StartDate,
            endDate = request.EndDate,
            startTime = request.StartTime,
            endTime = request.EndTime,
            amount = request.Amount,
            status = request.Status.ToString(),
            reason = request.Reason,
        };
    }

    private static object Punch(TimePunch punch)
    {
        return new
        {
            id = punch.ItemId,
            workerId = punch.WorkerId,
            kind = punch.Kind.ToString(),
            utc = punch.Utc,
            source = punch.Source.ToString(),
            withinGeofence = punch.WithinGeofence,
        };
    }

    private static object Timesheet(Timesheet timesheet)
    {
        return new
        {
            id = timesheet.ItemId,
            workerId = timesheet.WorkerId,
            teamId = timesheet.TeamId,
            periodStart = timesheet.PeriodStart,
            periodEnd = timesheet.PeriodEnd,
            status = timesheet.Status.ToString(),
            hours = timesheet.TotalHours,
            openExceptions = timesheet.OpenExceptions,
            lines = timesheet.Lines.Select(line => new { date = line.Date, payCodeId = line.PayCodeId, hours = line.Hours, source = line.Source.ToString(), activityCodeId = line.ActivityCodeId }),
        };
    }
}

/// <summary>
/// A punch sent to the API.
/// </summary>
public sealed class WorkforcePunchInput
{
    /// <summary>
    /// Gets or sets the worker, for <c>POST punches</c>.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets what the punch records.
    /// </summary>
    public TimePunchKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the UTC time of a punch made earlier; empty for now.
    /// </summary>
    public DateTime? Utc { get; set; }

    /// <summary>
    /// Gets or sets where it came from, for <c>POST punches</c>: Api (the default), ContactCenter, Kiosk or Manager.
    /// </summary>
    public TimePunchSource? Source { get; set; }

    /// <summary>
    /// Gets or sets the latitude.
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Gets or sets the longitude.
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Gets or sets the activity.
    /// </summary>
    public string ActivityCodeId { get; set; }

    /// <summary>
    /// Gets or sets the job.
    /// </summary>
    public string Job { get; set; }

    /// <summary>
    /// Gets or sets the device.
    /// </summary>
    public string DeviceId { get; set; }

    /// <summary>
    /// Gets or sets why, for a manager's punch.
    /// </summary>
    public string Reason { get; set; }
}

/// <summary>
/// Time off asked for through the API.
/// </summary>
public sealed class WorkforceTimeOffInput
{
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the last day.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Gets or sets when part of one day starts.
    /// </summary>
    public TimeOnly? StartTime { get; set; }

    /// <summary>
    /// Gets or sets when part of one day ends.
    /// </summary>
    public TimeOnly? EndTime { get; set; }

    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public string Reason { get; set; }
}

/// <summary>
/// A decision's note.
/// </summary>
public sealed class WorkforceDecisionInput
{
    /// <summary>
    /// Gets or sets the note.
    /// </summary>
    public string Note { get; set; }
}
