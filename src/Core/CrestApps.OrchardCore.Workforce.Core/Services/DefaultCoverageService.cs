using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="ICoverageService"/>.
/// </summary>
internal sealed class DefaultCoverageService : ICoverageService
{
    private const int MaxDays = 92;

    private readonly CoverageRequirementStore _requirementStore;
    private readonly DraftShiftStore _draftStore;
    private readonly TeamStore _teamStore;
    private readonly ActivityCodeStore _activityCodeStore;
    private readonly ICalendarQueryService _queryService;
    private readonly TeamTimeZoneResolver _timeZones;
    private readonly ISkillAssignmentService _skillAssignments;

    internal readonly IStringLocalizer S;

    public DefaultCoverageService(
        CoverageRequirementStore requirementStore,
        DraftShiftStore draftStore,
        TeamStore teamStore,
        ActivityCodeStore activityCodeStore,
        ICalendarQueryService queryService,
        TeamTimeZoneResolver timeZones,
        ISkillAssignmentService skillAssignments,
        IStringLocalizer<DefaultCoverageService> stringLocalizer)
    {
        _requirementStore = requirementStore;
        _draftStore = draftStore;
        _teamStore = teamStore;
        _activityCodeStore = activityCodeStore;
        _queryService = queryService;
        _timeZones = timeZones;
        _skillAssignments = skillAssignments;
        S = stringLocalizer;
    }

    public async Task<IReadOnlyList<CoverageRequirement>> ListRequirementsAsync(string teamId, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(teamId) ? [] : await _requirementStore.ListForTeamAsync(teamId, cancellationToken);
    }

    public async Task<CoverageRequirement> FindRequirementAsync(string id, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(id) ? null : await _requirementStore.FindByIdAsync(id, cancellationToken);
    }

    public async Task<PlanningResult<CoverageRequirement>> SaveRequirementAsync(CoverageRequirement requirement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        var result = new PlanningResult<CoverageRequirement>();

        if (string.IsNullOrEmpty(requirement.TeamId) || await _teamStore.FindByIdAsync(requirement.TeamId, cancellationToken) is null)
        {
            result.Fail(S["Choose a team."]);
        }

        if (string.IsNullOrWhiteSpace(requirement.Name))
        {
            result.Fail(S["Give the requirement a name."]);
        }

        if (requirement.EffectiveFrom.HasValue && requirement.EffectiveTo.HasValue && requirement.EffectiveTo < requirement.EffectiveFrom)
        {
            result.Fail(S["The requirement must end after it starts."]);
        }

        foreach (var row in requirement.Rows)
        {
            if (row.Day is null && row.Date is null)
            {
                result.Fail(S["Every row needs a weekday or a date."]);
            }

            if (CoverageRows.EndMinute(row) <= CoverageRows.StartMinute(row))
            {
                result.Fail(S["Every row must end after it starts."]);
            }

            if (row.Minimum < 0 || row.Target < row.Minimum || (row.Maximum.HasValue && row.Maximum < row.Target))
            {
                result.Fail(S["Every row needs a minimum no larger than its target, and a maximum, when set, no smaller."]);
            }
        }

        if (!result.Succeeded)
        {
            return result;
        }

        requirement.Name = requirement.Name.Trim();

        var stored = string.IsNullOrEmpty(requirement.ItemId) ? null : await _requirementStore.FindByIdAsync(requirement.ItemId, cancellationToken);

        if (stored is null)
        {
            requirement.ItemId = string.IsNullOrEmpty(requirement.ItemId) ? UniqueId.GenerateId() : requirement.ItemId;
            await _requirementStore.CreateAsync(requirement, cancellationToken);
            result.Value = requirement;

            return result;
        }

        if (!ReferenceEquals(stored, requirement))
        {
            stored.TeamId = requirement.TeamId;
            stored.Name = requirement.Name;
            stored.SkillId = requirement.SkillId;
            stored.Source = requirement.Source;
            stored.Rows = requirement.Rows;
            stored.EffectiveFrom = requirement.EffectiveFrom;
            stored.EffectiveTo = requirement.EffectiveTo;
        }

        await _requirementStore.UpdateAsync(stored, cancellationToken);
        result.Value = stored;

        return result;
    }

    public async Task<bool> DeleteRequirementAsync(string id, CancellationToken cancellationToken = default)
    {
        var requirement = await FindRequirementAsync(id, cancellationToken);

        return requirement is not null && await _requirementStore.DeleteAsync(requirement, cancellationToken);
    }

    public async Task<IReadOnlyList<CoverageInterval>> ComputeAsync(CoverageQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var team = string.IsNullOrEmpty(query.TeamId) ? null : await _teamStore.FindByIdAsync(query.TeamId, cancellationToken);

        if (team is null || query.To < query.From || query.To.DayNumber - query.From.DayNumber >= MaxDays)
        {
            return [];
        }

        var intervalMinutes = Math.Clamp(query.IntervalMinutes, 5, 240);
        var timeZoneId = await _timeZones.GetAsync(team.ItemId, cancellationToken);
        var requirements = (await _requirementStore.ListForTeamAsync(team.ItemId, cancellationToken))
            .Where(requirement => query.SkillId is null || requirement.SkillId == query.SkillId)
            .ToArray();
        var shifts = string.IsNullOrEmpty(query.ScenarioId)
            ? await ListPublishedAsync(team, query, timeZoneId, cancellationToken)
            : await ListDraftsAsync(query.ScenarioId, timeZoneId, cancellationToken);

        if (!string.IsNullOrEmpty(query.SkillId))
        {
            // Only the workers who hold the skill on the shift's day count for it.
            var holders = new Dictionary<(string WorkerId, DateOnly Date), bool>();

            foreach (var key in shifts.Select(shift => (shift.WorkerId, shift.Date)).Distinct())
            {
                holders[key] = key.WorkerId is not null
                    && await _skillAssignments.HoldsAsync(SkillSubject.Worker(key.WorkerId), query.SkillId, 0, key.Date, cancellationToken);
            }

            shifts = shifts.Where(shift => holders[(shift.WorkerId, shift.Date)]).ToArray();
        }
        var intervals = new List<CoverageInterval>();

        for (var date = query.From; date <= query.To; date = date.AddDays(1))
        {
            var rows = requirements
                .Where(requirement => CoverageRows.AppliesOn(requirement, date))
                .SelectMany(requirement => CoverageRows.RowsOn(requirement, date))
                .ToArray();

            for (var minute = 0; minute < 24 * 60; minute += intervalMinutes)
            {
                var end = Math.Min(minute + intervalMinutes, 24 * 60);
                var startUtc = CalendarTime.ToUtc(date.ToDateTime(TimeOnly.MinValue).AddMinutes(minute), timeZoneId);
                var endUtc = CalendarTime.ToUtc(date.ToDateTime(TimeOnly.MinValue).AddMinutes(end), timeZoneId);
                var interval = new CoverageInterval { StartUtc = startUtc, EndUtc = endUtc };
                var maximum = 0;
                var hasMaximum = false;

                foreach (var row in rows.Where(row => CoverageRows.StartMinute(row) <= minute && CoverageRows.EndMinute(row) >= end))
                {
                    interval.Minimum += row.Minimum;
                    interval.Target += row.Target;

                    if (row.Maximum.HasValue)
                    {
                        maximum += row.Maximum.Value;
                        hasMaximum = true;
                    }
                }

                interval.Maximum = hasMaximum ? maximum : null;
                interval.Scheduled = shifts.Count(shift => shift.Covers(startUtc, endUtc));
                intervals.Add(interval);
            }
        }

        return intervals;
    }

    private async Task<IReadOnlyList<ScheduledSpan>> ListDraftsAsync(string scenarioId, string timeZoneId, CancellationToken cancellationToken)
    {
        var excluded = await ExcludedCodesAsync(cancellationToken);
        var drafts = await _draftStore.ListForScenarioAsync(scenarioId, cancellationToken);

        return drafts
            .Where(draft => draft.WorkerId is not null)
            .Select(draft => new ScheduledSpan(
                draft.WorkerId,
                draft.Date,
                CalendarTime.ToUtc(draft.Start, timeZoneId),
                CalendarTime.ToUtc(draft.End, timeZoneId),
                draft.Segments
                    .Where(segment => segment.ActivityCodeId is not null && excluded.Contains(segment.ActivityCodeId))
                    .Select(segment => (CalendarTime.ToUtc(segment.Start, timeZoneId), CalendarTime.ToUtc(segment.End, timeZoneId)))
                    .ToArray()))
            .ToArray();
    }

    private async Task<IReadOnlyList<ScheduledSpan>> ListPublishedAsync(WorkforceTeam team, CoverageQuery query, string timeZoneId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(team.CalendarId))
        {
            return [];
        }

        var excluded = await ExcludedCodesAsync(cancellationToken);
        var occurrences = await _queryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            CalendarIds = [team.CalendarId],
            FromUtc = CalendarTime.ToUtc(query.From.ToDateTime(TimeOnly.MinValue), timeZoneId),
            ToUtc = CalendarTime.ToUtc(query.To.AddDays(1).ToDateTime(TimeOnly.MinValue), timeZoneId),
            EventTypes = [WorkforceConstants.EventTypes.Shift],
        }, cancellationToken);

        return occurrences
            .Where(occurrence => occurrence.Event.Status != CalendarEventStatus.Cancelled)
            .Select(occurrence =>
            {
                var hasData = occurrence.Event.TryGet<ShiftData>(out var data);
                var segments = hasData
                    ? data.Segments
                        .Where(segment => segment.ActivityCodeId is not null && excluded.Contains(segment.ActivityCodeId))
                        .Select(segment => (segment.StartUtc, segment.EndUtc))
                        .ToArray()
                    : [];

                return new ScheduledSpan(hasData ? data.WorkerId : null, DateOnly.FromDateTime(CalendarTime.ToLocal(occurrence.StartUtc, timeZoneId)), occurrence.StartUtc, occurrence.EndUtc, segments);
            })
            .ToArray();
    }

    private async Task<HashSet<string>> ExcludedCodesAsync(CancellationToken cancellationToken)
    {
        return (await _activityCodeStore.GetAllAsync(cancellationToken))
            .Where(code => !code.CountsTowardCoverage)
            .Select(code => code.ItemId)
            .ToHashSet(StringComparer.Ordinal);
    }

    private sealed record ScheduledSpan(string WorkerId, DateOnly Date, DateTime StartUtc, DateTime EndUtc, IReadOnlyList<(DateTime StartUtc, DateTime EndUtc)> Away)
    {
        public bool Covers(DateTime fromUtc, DateTime toUtc)
            => StartUtc <= fromUtc && EndUtc >= toUtc && !Away.Any(away => away.StartUtc < toUtc && away.EndUtc > fromUtc);
    }
}
