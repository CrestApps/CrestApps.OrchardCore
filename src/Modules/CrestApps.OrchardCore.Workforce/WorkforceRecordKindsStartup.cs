using System.Globalization;
using CrestApps.Core.Models;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Exposes the activity codes to the generic record tools (<c>listRecords</c>, <c>getRecord</c>) when the AI feature is
/// on. The Activity Codes page binds its own form, so the kind is read-only: records are changed on the admin page.
/// </summary>
[Feature(WorkforceConstants.Features.Workforce)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class WorkforceRecordKindsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceRecordKindsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceRecordKindsStartup(IStringLocalizer<WorkforceRecordKindsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIRecordKind<ActivityCode>("activityCode", kind => kind
            .WithTitle(S["Activity code"])
            .WithDescription(S["a workforce activity code (work, break, meal, training...) that shift segments, templates and the time clock use, with whether it is paid, counts as work and counts toward coverage; read-only, changed on the Activity Codes page"])
            .WithPermission(WorkforcePermissions.ManageActivityCodes)
            .UseCatalogManager<IActivityCodeManager>()
            .WithProjection(code => new
            {
                id = code.ItemId,
                name = code.Name,
                code = code.Code,
                kind = code.Kind.ToString(),
                color = code.Color,
                isPaid = code.IsPaid,
                countsAsWork = code.CountsAsWork,
                countsTowardCoverage = code.CountsTowardCoverage,
                enabled = code.Enabled,
                timeStageId = code.StageId,
            })
            .ReadOnly());
    }
}

/// <summary>
/// Exposes the shift templates, rotations and coverage requirements to the generic record tools when the AI feature is
/// on. Their admin pages bind their own forms, so the kinds are read-only. Rotations and coverage requirements are listed
/// and read only for the teams the caller manages, as on their pages.
/// </summary>
[Feature(WorkforceConstants.Features.ShiftPlanning)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class ShiftPlanningRecordKindsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShiftPlanningRecordKindsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ShiftPlanningRecordKindsStartup(IStringLocalizer<ShiftPlanningRecordKindsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIRecordKind<ShiftTemplate>("shiftTemplate", kind => kind
            .WithTitle(S["Shift template"])
            .WithDescription(S["a reusable shift (start time, length, breaks and activities, teams, required skills) that schedules, rotations and open shifts start from; read-only, changed on the Shift Templates page"])
            .WithPermission(WorkforcePermissions.ManageShiftTemplates)
            .UseCatalogManager<IShiftTemplateManager>()
            .WithProjection(template => new
            {
                id = template.ItemId,
                name = template.Name,
                color = template.Color,
                start = WorkforceRecordKinds.Time(template.Start),
                lengthMinutes = template.LengthMinutes,
                segments = template.Segments.Select(segment => new
                {
                    activityCodeId = segment.ActivityCodeId,
                    offsetMinutes = segment.OffsetMinutes,
                    lengthMinutes = segment.LengthMinutes,
                }).ToList(),
                teamIds = template.TeamIds.ToList(),
                requiredSkillIds = template.RequiredSkillIds.ToList(),
                enabled = template.Enabled,
            })
            .ReadOnly());

        services.AddAIRecordKind<ShiftPattern>("rotation", kind => kind
            .WithTitle(S["Rotation"])
            .WithDescription(S["a team's repeating shift rotation: the template of each weekday over a number of weeks and the workers on it with their start dates; only teams the user manages; read-only, changed on the Rotations page"])
            .WithPermission(WorkforcePermissions.ManageSchedules)
            .FindUsing(WorkforceRecordKinds.FindRotationAsync)
            .ListUsing(WorkforceRecordKinds.ListRotationsAsync)
            .WithProjection(pattern => new
            {
                id = pattern.ItemId,
                name = pattern.Name,
                teamId = pattern.TeamId,
                weekCount = pattern.WeekCount,
                days = pattern.Days.Select(day => new
                {
                    week = day.Week,
                    day = day.Day.ToString(),
                    templateId = day.TemplateId,
                }).ToList(),
                workers = pattern.Workers.Select(worker => new
                {
                    workerId = worker.WorkerId,
                    startDate = WorkforceRecordKinds.Day(worker.StartDate),
                }).ToList(),
                enabled = pattern.Enabled,
            })
            .ReadOnly());

        services.AddAIRecordKind<CoverageRequirement>("coverageRequirement", kind => kind
            .WithTitle(S["Coverage requirement"])
            .WithDescription(S["how many people a team needs at work by weekday or date and time of day (minimum, target, maximum), optionally for a skill; the auto-scheduler and the schedule's coverage check use it; only teams the user manages; read-only, changed on the Coverage page"])
            .WithPermission(WorkforcePermissions.ManageSchedules)
            .FindUsing(WorkforceRecordKinds.FindCoverageRequirementAsync)
            .ListUsing(WorkforceRecordKinds.ListCoverageRequirementsAsync)
            .WithProjection(requirement => new
            {
                id = requirement.ItemId,
                name = requirement.Name,
                teamId = requirement.TeamId,
                skillId = requirement.SkillId,
                source = requirement.Source.ToString(),
                effectiveFrom = WorkforceRecordKinds.Day(requirement.EffectiveFrom),
                effectiveTo = WorkforceRecordKinds.Day(requirement.EffectiveTo),
                rows = requirement.Rows.Select(row => new
                {
                    day = row.Day?.ToString(),
                    date = WorkforceRecordKinds.Day(row.Date),
                    start = WorkforceRecordKinds.Time(row.Start),
                    end = WorkforceRecordKinds.Time(row.End),
                    minimum = row.Minimum,
                    target = row.Target,
                    maximum = row.Maximum,
                }).ToList(),
            })
            .ReadOnly());
    }
}

/// <summary>
/// Exposes the time-off types, policies, allotments and blackout dates to the generic record tools when the AI feature
/// is on. Their admin pages bind their own forms, so the kinds are read-only.
/// </summary>
[Feature(WorkforceConstants.Features.TimeOff)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class TimeOffRecordKindsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffRecordKindsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeOffRecordKindsStartup(IStringLocalizer<TimeOffRecordKindsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIRecordKind<TimeOffType>("timeOffType", kind => kind
            .WithTitle(S["Time-off type"])
            .WithDescription(S["a kind of time off (vacation, sick, personal...) with its unit, hours per day, whether it is paid, needs a balance or a reason and is shown to workers; read-only, changed on the Time-Off Types page"])
            .WithPermission(WorkforcePermissions.ManageTimeOffPolicies)
            .UseCatalogManager<ITimeOffTypeManager>()
            .WithProjection(type => new
            {
                id = type.ItemId,
                name = type.Name,
                description = type.Description,
                color = type.Color,
                unit = type.Unit.ToString(),
                hoursPerDay = type.HoursPerDay,
                isPaid = type.IsPaid,
                requiresBalance = type.RequiresBalance,
                allowPartialDay = type.AllowPartialDay,
                requiresReason = type.RequiresReason,
                countsAgainstAllotment = type.CountsAgainstAllotment,
                visibleToWorkers = type.VisibleToWorkers,
                enabled = type.Enabled,
                timeStageId = type.StageId,
            })
            .ReadOnly());

        services.AddAIRecordKind<TimeOffPolicy>("timeOffPolicy", kind => kind
            .WithTitle(S["Time-off policy"])
            .WithDescription(S["how a time-off type is earned (accrual method, frequency, rate and tenure tiers, waiting period, maximum balance, carry-over, reset date, negative limit) and the teams and workers it is assigned to; read-only, changed on the Time-Off Policies page; assign a policy to one worker with assignTimeOffPolicy"])
            .WithPermission(WorkforcePermissions.ManageTimeOffPolicies)
            .UseCatalogManager<ITimeOffPolicyManager>()
            .WithProjection(policy => new
            {
                id = policy.ItemId,
                name = policy.Name,
                typeId = policy.TypeId,
                accrualMethod = policy.AccrualMethod.ToString(),
                frequency = policy.Frequency.ToString(),
                anchorDate = WorkforceRecordKinds.Day(policy.AnchorDate),
                rate = policy.Rate,
                tenureTiers = policy.TenureTiers.Select(tier => new { fromMonths = tier.FromMonths, rate = tier.Rate }).ToList(),
                waitingPeriodDays = policy.WaitingPeriodDays,
                maxBalance = policy.MaxBalance,
                carryoverMax = policy.CarryoverMax,
                resetMonth = policy.ResetMonth,
                resetDay = policy.ResetDay,
                negativeBalanceLimit = policy.NegativeBalanceLimit,
                assignments = policy.Assignments.Select(assignment => new
                {
                    teamId = assignment.TeamId,
                    workerId = assignment.WorkerId,
                    effectiveFrom = WorkforceRecordKinds.Day(assignment.EffectiveFrom),
                    effectiveTo = WorkforceRecordKinds.Day(assignment.EffectiveTo),
                }).ToList(),
                enabled = policy.Enabled,
            })
            .ReadOnly());

        services.AddAIRecordKind<TimeOffAllotment>("timeOffAllotment", kind => kind
            .WithTitle(S["Time-off allotment"])
            .WithDescription(S["how many people of a team may be off on each weekday or on given dates, for which time-off types, and what happens when it is full (waitlist or decline); read-only, changed on the Allotments page"])
            .WithPermission(WorkforcePermissions.ManageTimeOffPolicies)
            .UseCatalogManager<ITimeOffAllotmentManager>()
            .WithProjection(allotment => new
            {
                id = allotment.ItemId,
                name = allotment.Name,
                teamId = allotment.TeamId,
                typeIds = allotment.TypeIds.ToList(),

                // Slots per weekday, Sunday first, as stored.
                weekdaySlots = allotment.WeekdaySlots.ToList(),
                dates = allotment.Dates.Select(date => new { date = WorkforceRecordKinds.Day(date.Date), slots = date.Slots }).ToList(),
                autoApprove = allotment.AutoApprove,
                whenFull = allotment.WhenFull.ToString(),
                enabled = allotment.Enabled,
            })
            .ReadOnly());

        services.AddAIRecordKind<BlackoutPeriod>("blackoutPeriod", kind => kind
            .WithTitle(S["Blackout dates"])
            .WithDescription(S["dates when a team (or everyone) can't ask for time off, for some or all time-off types, with the message workers see; read-only, changed on the Blackout Dates page"])
            .WithPermission(WorkforcePermissions.ManageTimeOffPolicies)
            .UseCatalogManager<IBlackoutPeriodManager>()
            .WithProjection(period => new
            {
                id = period.ItemId,
                name = period.Name,
                teamId = period.TeamId,
                startDate = WorkforceRecordKinds.Day(period.StartDate),
                endDate = WorkforceRecordKinds.Day(period.EndDate),
                typeIds = period.TypeIds.ToList(),
                message = period.Message,
                enabled = period.Enabled,
            })
            .ReadOnly());
    }
}

/// <summary>
/// Exposes the time clock policies to the generic record tools when the AI feature is on. The Time Clock Policies page
/// binds its own form, so the kind is read-only. The pay period calendar and pay codes are left out.
/// </summary>
[Feature(WorkforceConstants.Features.TimeClock)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class TimeClockRecordKindsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeClockRecordKindsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeClockRecordKindsStartup(IStringLocalizer<TimeClockRecordKindsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIRecordKind<TimeClockPolicy>("timeClockPolicy", kind => kind
            .WithTitle(S["Time clock policy"])
            .WithDescription(S["the time clock rules a team uses: rounding, grace minutes, early punch blocking, automatic meals, allowed punch sources, geofence, missed punches, daily, weekly and seventh-day overtime, holidays, weekend days and attendance points; read-only, changed on the Time Clock Policies page"])
            .WithPermission(WorkforcePermissions.ManageTimeClockPolicies)
            .UseCatalogManager<ITimeClockPolicyManager>()
            .WithProjection(policy => new
            {
                id = policy.ItemId,
                name = policy.Name,
                description = policy.Description,
                roundingMinutes = policy.RoundingMinutes,
                roundingDirection = policy.RoundingDirection.ToString(),
                earlyGraceMinutes = policy.EarlyGraceMinutes,
                lateGraceMinutes = policy.LateGraceMinutes,
                blockEarlyPunchInMinutes = policy.BlockEarlyPunchInMinutes,
                autoMealAfterHours = policy.AutoMealAfterHours,
                autoMealMinutes = policy.AutoMealMinutes,
                longMealMinutes = policy.LongMealMinutes,
                allowedSources = policy.AllowedSources.ToList(),
                requireGeofence = policy.RequireGeofence,
                geofenceRadiusMeters = policy.GeofenceRadiusMeters,
                missedPunchHandling = policy.MissedPunchHandling.ToString(),
                dailyOvertimeHours = policy.DailyOvertimeHours,
                dailyDoubleTimeHours = policy.DailyDoubleTimeHours,
                weeklyOvertimeHours = policy.WeeklyOvertimeHours,
                seventhDayOvertime = policy.SeventhDayOvertime,
                holidays = policy.Holidays.Select(day => WorkforceRecordKinds.Day(day)).ToList(),
                weekendDays = policy.WeekendDays.Select(day => day.ToString()).ToList(),
                attendancePoints = policy.AttendancePoints.Select(rule => new { kind = rule.Kind.ToString(), points = rule.Points }).ToList(),
                pointsWindowDays = policy.PointsWindowDays,
                pointsThreshold = policy.PointsThreshold,
                enabled = policy.Enabled,
            })
            .ReadOnly());
    }
}

/// <summary>
/// Formatting and the team scope of the Workforce record kinds.
/// </summary>
internal static class WorkforceRecordKinds
{
    public static string Day(DateOnly? date)
        => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Time(TimeOnly time)
        => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Finds a rotation of a team the caller manages; another team's is not found, as on the Rotations page.
    /// </summary>
    public static async ValueTask<ShiftPattern> FindRotationAsync(IServiceProvider services, string id, CancellationToken cancellationToken)
    {
        var pattern = await services.GetRequiredService<IShiftPatternManager>().FindByIdAsync(id, cancellationToken);

        return pattern is not null && (await ScopeAsync(services, cancellationToken)).Covers(pattern.TeamId) ? pattern : null;
    }

    /// <summary>
    /// Lists the rotations of the teams the caller manages, by name.
    /// </summary>
    public static async ValueTask<PageResult<ShiftPattern>> ListRotationsAsync(IServiceProvider services, AIRecordQuery query, CancellationToken cancellationToken)
    {
        var scope = await ScopeAsync(services, cancellationToken);
        var patterns = (await services.GetRequiredService<IShiftPatternManager>().GetAllAsync(cancellationToken))
            .Where(pattern => scope.Covers(pattern.TeamId) && Matches(pattern.Name, query.Text))
            .OrderBy(pattern => pattern.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Page(patterns, query);
    }

    /// <summary>
    /// Finds a coverage requirement of a team the caller manages; another team's is not found, as on the Coverage page.
    /// </summary>
    public static async ValueTask<CoverageRequirement> FindCoverageRequirementAsync(IServiceProvider services, string id, CancellationToken cancellationToken)
    {
        var requirement = await services.GetRequiredService<ICoverageService>().FindRequirementAsync(id, cancellationToken);

        return requirement is not null && (await ScopeAsync(services, cancellationToken)).Covers(requirement.TeamId) ? requirement : null;
    }

    /// <summary>
    /// Lists the coverage requirements of the teams the caller manages, by team, as the Coverage page lists them per team.
    /// </summary>
    public static async ValueTask<PageResult<CoverageRequirement>> ListCoverageRequirementsAsync(IServiceProvider services, AIRecordQuery query, CancellationToken cancellationToken)
    {
        var scope = await ScopeAsync(services, cancellationToken);
        var teamIds = scope.All
            ? (await services.GetRequiredService<ITeamManager>().GetAllAsync(cancellationToken)).Select(team => team.ItemId).ToList()
            : scope.TeamIds.ToList();
        var coverage = services.GetRequiredService<ICoverageService>();
        var requirements = new List<CoverageRequirement>();

        foreach (var teamId in teamIds.Order(StringComparer.Ordinal))
        {
            requirements.AddRange((await coverage.ListRequirementsAsync(teamId, cancellationToken)).Where(requirement => Matches(requirement.Name, query.Text)));
        }

        return Page(requirements, query);
    }

    private static async Task<WorkforceScope> ScopeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        // The record tools have already refused a caller who is not signed in; the caller is read the same way.
        var published = services.GetService<IUserAccessor>()?.User;
        var user = published?.Identity?.IsAuthenticated == true
            ? published
            : services.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return WorkforceScope.None;
        }

        return await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(user, cancellationToken);
    }

    private static bool Matches(string name, string text)
        => string.IsNullOrWhiteSpace(text) || (name ?? string.Empty).Contains(text.Trim(), StringComparison.OrdinalIgnoreCase);

    private static PageResult<T> Page<T>(IReadOnlyList<T> items, AIRecordQuery query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Max(1, query.PageSize);

        return new PageResult<T>
        {
            Count = items.Count,
            Entries = items.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
        };
    }
}
