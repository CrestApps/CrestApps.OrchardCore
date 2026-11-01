using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The built-in <see cref="IShiftAssignmentValidator"/>: the worker must be active and on the team that day, hold
/// the shift's skills, not have an overlapping shift, rest long enough between shifts, and stay within their weekly
/// and daily hours.
/// </summary>
internal sealed class DefaultShiftAssignmentValidator : IShiftAssignmentValidator
{
    private readonly WorkerShiftReader _shiftReader;
    private readonly ISkillAssignmentService _skillAssignments;
    private readonly ISkillManager _skillManager;
    private readonly ActivityCodeStore _activityCodeStore;
    private readonly TeamTimeZoneResolver _timeZones;
    private readonly IWorkerScheduleLimitsProvider _limits;

    internal readonly IStringLocalizer S;

    public DefaultShiftAssignmentValidator(
        WorkerShiftReader shiftReader,
        ISkillAssignmentService skillAssignments,
        ISkillManager skillManager,
        ActivityCodeStore activityCodeStore,
        TeamTimeZoneResolver timeZones,
        IWorkerScheduleLimitsProvider limits,
        IStringLocalizer<DefaultShiftAssignmentValidator> stringLocalizer)
    {
        _shiftReader = shiftReader;
        _skillAssignments = skillAssignments;
        _skillManager = skillManager;
        _activityCodeStore = activityCodeStore;
        _timeZones = timeZones;
        _limits = limits;
        S = stringLocalizer;
    }

    public async Task ValidateAsync(ShiftAssignmentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var worker = context.Worker;

        if (worker is null)
        {
            context.Problems.Add(S["The worker doesn't exist."]);

            return;
        }

        var name = worker.DisplayName;
        var day = context.Date.ToString("d", null);

        if (worker.Status != WorkerStatus.Active || (worker.TerminationDate.HasValue && worker.TerminationDate < context.Date))
        {
            context.Problems.Add(S["{0} isn't an active worker.", name]);
        }

        if (!worker.MembershipsOn(context.Date).Any(membership => membership.TeamId == context.TeamId))
        {
            context.Problems.Add(S["{0} isn't on the team on {1}.", name, day]);
        }

        foreach (var skillId in context.RequiredSkillIds ?? [])
        {
            if (!await _skillAssignments.HoldsAsync(SkillSubject.Worker(worker.ItemId), skillId, 0, context.Date, cancellationToken))
            {
                var skill = await _skillManager.FindByIdAsync(skillId, cancellationToken);

                context.Problems.Add(S["{0} doesn't hold the {1} skill.", name, skill?.Name ?? skillId]);
            }
        }

        var timeZoneId = await _timeZones.GetAsync(context.TeamId, cancellationToken);
        var codes = (await _activityCodeStore.GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);
        var limits = await _limits.GetAsync(worker, context.TeamId, cancellationToken);

        // With a labor rule set, its rules check rest and hours, with their own enforcement.
        var checkLimits = !limits.FromRuleSet;
        var restMinutes = (double)Math.Max(0, limits.MinRestHours) * 60;
        var others = (await _shiftReader.ListAsync(worker, context.StartUtc.AddDays(-8), context.EndUtc.AddDays(8), cancellationToken))
            .Where(calendarEvent => !context.IgnoredShiftKeys.Contains(calendarEvent.SourceId ?? string.Empty))
            .ToArray();

        foreach (var other in others)
        {
            if (other.StartUtc < context.EndUtc && other.EndUtc > context.StartUtc)
            {
                var start = CalendarTime.ToLocal(other.StartUtc, timeZoneId);
                var end = CalendarTime.ToLocal(other.EndUtc, timeZoneId);

                context.Problems.Add(S["{0} already works {1:t}-{2:t} on {3}; the shifts overlap.", name, start, end, start.ToString("d", null)]);

                continue;
            }

            var gap = other.EndUtc <= context.StartUtc ? (context.StartUtc - other.EndUtc).TotalMinutes : (other.StartUtc - context.EndUtc).TotalMinutes;

            if (checkLimits && gap < restMinutes)
            {
                context.Problems.Add(S["{0} would rest only {1:0.#} hours between shifts; at least {2} are needed.", name, gap / 60, limits.MinRestHours]);
            }
        }

        var weekStart = context.Date.AddDays(-(((int)context.Date.DayOfWeek + 6) % 7));
        var weekMinutes = context.PaidMinutes;
        var dayMinutes = context.PaidMinutes;

        foreach (var other in others)
        {
            if (!ShiftEvents.TryRead(other, out var data))
            {
                continue;
            }

            var date = DateOnly.FromDateTime(CalendarTime.ToLocal(other.StartUtc, timeZoneId));
            var paid = ShiftEvents.PaidMinutes(other, data, codes);

            if (date >= weekStart && date <= weekStart.AddDays(6))
            {
                weekMinutes += paid;
            }

            if (date == context.Date)
            {
                dayMinutes += paid;
            }
        }

        if (checkLimits && limits.MaxHoursPerWeek.HasValue && weekMinutes > limits.MaxHoursPerWeek.Value * 60)
        {
            context.Problems.Add(S["{0} would work {1:0.#} hours that week, more than their {2:0.#}.", name, weekMinutes / 60.0, limits.MaxHoursPerWeek.Value]);
        }

        if (checkLimits && limits.MaxHoursPerDay.HasValue && dayMinutes > limits.MaxHoursPerDay.Value * 60)
        {
            context.Problems.Add(S["{0} would work {1:0.#} hours that day, more than their {2:0.#}.", name, dayMinutes / 60.0, limits.MaxHoursPerDay.Value]);
        }
    }
}
