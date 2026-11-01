using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="ISchedulePlanningService"/>.
/// </summary>
internal sealed class DefaultSchedulePlanningService : ISchedulePlanningService
{
    private const int MaxWeeks = 12;
    private const int MaxShiftHours = 24;

    private readonly SchedulePeriodStore _periodStore;
    private readonly ScheduleScenarioStore _scenarioStore;
    private readonly DraftShiftStore _draftStore;
    private readonly ScheduleChangeStore _changeStore;
    private readonly ScheduleChangeRecorder _changeRecorder;
    private readonly IEnumerable<ISchedulePublishValidator> _publishValidators;
    private readonly TeamStore _teamStore;
    private readonly WorkerStore _workerStore;
    private readonly ActivityCodeStore _activityCodeStore;
    private readonly IShiftPatternManager _patternManager;
    private readonly IShiftTemplateManager _templateManager;
    private readonly WorkforceCalendarService _calendarService;
    private readonly ICalendarQueryService _queryService;
    private readonly ICalendarEventManager _eventManager;
    private readonly ICalendarService _calendars;
    private readonly TeamTimeZoneResolver _timeZones;
    private readonly IEnumerable<ISchedulePublishHandler> _publishHandlers;
    private readonly ScheduleAutoScheduler _autoScheduler;
    private readonly OpenShiftPosting _openShiftPosting;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DefaultSchedulePlanningService(
        SchedulePeriodStore periodStore,
        ScheduleScenarioStore scenarioStore,
        DraftShiftStore draftStore,
        ScheduleChangeStore changeStore,
        ScheduleChangeRecorder changeRecorder,
        IEnumerable<ISchedulePublishValidator> publishValidators,
        TeamStore teamStore,
        WorkerStore workerStore,
        ActivityCodeStore activityCodeStore,
        IShiftPatternManager patternManager,
        IShiftTemplateManager templateManager,
        WorkforceCalendarService calendarService,
        ICalendarQueryService queryService,
        ICalendarEventManager eventManager,
        ICalendarService calendars,
        TeamTimeZoneResolver timeZones,
        IEnumerable<ISchedulePublishHandler> publishHandlers,
        ScheduleAutoScheduler autoScheduler,
        OpenShiftPosting openShiftPosting,
        IClock clock,
        IStringLocalizer<DefaultSchedulePlanningService> stringLocalizer)
    {
        _periodStore = periodStore;
        _scenarioStore = scenarioStore;
        _draftStore = draftStore;
        _changeStore = changeStore;
        _changeRecorder = changeRecorder;
        _publishValidators = publishValidators;
        _teamStore = teamStore;
        _workerStore = workerStore;
        _activityCodeStore = activityCodeStore;
        _patternManager = patternManager;
        _templateManager = templateManager;
        _calendarService = calendarService;
        _queryService = queryService;
        _eventManager = eventManager;
        _calendars = calendars;
        _timeZones = timeZones;
        _publishHandlers = publishHandlers;
        _autoScheduler = autoScheduler;
        _openShiftPosting = openShiftPosting;
        _clock = clock;
        S = stringLocalizer;
    }

    public async Task<PlanningResult<SchedulePeriod>> CreatePeriodAsync(string teamId, DateOnly startDate, int weeks, string name, string actor, CancellationToken cancellationToken = default)
    {
        var result = new PlanningResult<SchedulePeriod>();
        var team = string.IsNullOrEmpty(teamId) ? null : await _teamStore.FindByIdAsync(teamId, cancellationToken);

        if (team is null)
        {
            result.Fail(S["Choose a team."]);
        }

        if (weeks is < 1 or > MaxWeeks)
        {
            result.Fail(S["A schedule lasts from 1 to {0} weeks.", MaxWeeks]);
        }

        if (!result.Succeeded)
        {
            return result;
        }

        var now = _clock.UtcNow;
        var period = new SchedulePeriod
        {
            ItemId = UniqueId.GenerateId(),
            TeamId = team.ItemId,
            Name = string.IsNullOrWhiteSpace(name) ? S["{0}, week of {1}", team.Name, startDate.ToString("d", null)].Value : name.Trim(),
            StartDate = startDate,
            EndDate = startDate.AddDays((weeks * 7) - 1),
            CreatedUtc = now,
        };

        var scenario = new ScheduleScenario
        {
            ItemId = UniqueId.GenerateId(),
            PeriodId = period.ItemId,
            Name = S["Primary"],
            CreatedBy = actor,
            CreatedUtc = now,
        };

        period.PrimaryScenarioId = scenario.ItemId;

        await _periodStore.CreateAsync(period, cancellationToken);
        await _scenarioStore.CreateAsync(scenario, cancellationToken);

        result.Value = period;

        return result;
    }

    public async Task<SchedulePeriod> FindPeriodAsync(string id, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(id) ? null : await _periodStore.FindByIdAsync(id, cancellationToken);
    }

    public Task<(int Count, IReadOnlyList<SchedulePeriod> Items)> ListPeriodsAsync(IReadOnlyCollection<string> teamIds, int skip, int take, CancellationToken cancellationToken = default)
    {
        return _periodStore.PageAsync(teamIds, skip, take, cancellationToken);
    }

    public async Task<bool> DeletePeriodAsync(string id, CancellationToken cancellationToken = default)
    {
        var period = await FindPeriodAsync(id, cancellationToken);

        if (period is null || period.PublishedVersion > 0)
        {
            return false;
        }

        foreach (var scenario in await _scenarioStore.ListForPeriodAsync(period.ItemId, cancellationToken))
        {
            foreach (var shift in await _draftStore.ListForScenarioAsync(scenario.ItemId, cancellationToken))
            {
                _draftStore.Delete(shift);
            }

            await _scenarioStore.DeleteAsync(scenario, cancellationToken);
        }

        return await _periodStore.DeleteAsync(period, cancellationToken);
    }

    public async Task<ScheduleScenario> FindScenarioAsync(string id, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(id) ? null : await _scenarioStore.FindByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<DraftShift>> ListDraftShiftsAsync(string scenarioId, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(scenarioId) ? [] : await _draftStore.ListForScenarioAsync(scenarioId, cancellationToken);
    }

    public async Task<PlanningResult<DraftShift>> SaveDraftShiftAsync(DraftShift shift, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shift);

        var result = new PlanningResult<DraftShift>();
        var scenario = string.IsNullOrEmpty(shift.ScenarioId) ? null : await _scenarioStore.FindByIdAsync(shift.ScenarioId, cancellationToken);
        var period = scenario is null ? null : await _periodStore.FindByIdAsync(scenario.PeriodId, cancellationToken);

        if (period is null)
        {
            result.Fail(S["The schedule doesn't exist."]);

            return result;
        }

        if (period.Status == SchedulePeriodStatus.Locked)
        {
            result.Fail(S["The schedule is locked."]);

            return result;
        }

        if (shift.Date < period.StartDate || shift.Date > period.EndDate)
        {
            result.Fail(S["The shift is outside the schedule's dates."]);
        }

        if (shift.End <= shift.Start || (shift.End - shift.Start).TotalHours > MaxShiftHours)
        {
            result.Fail(S["A shift must end after it starts, and last at most {0} hours.", MaxShiftHours]);
        }

        if (shift.Segments.Any(segment => segment.Start < shift.Start || segment.End > shift.End || segment.End <= segment.Start))
        {
            result.Fail(S["Every break or activity must fit inside the shift."]);
        }

        if (!string.IsNullOrEmpty(shift.WorkerId))
        {
            var worker = await _workerStore.FindByIdAsync(shift.WorkerId, cancellationToken);

            if (worker is null || !worker.MembershipsOn(shift.Date).Any(membership => membership.TeamId == period.TeamId))
            {
                result.Fail(S["The worker isn't on the team that day."]);
            }
        }

        if (!result.Succeeded)
        {
            return result;
        }

        var stored = string.IsNullOrEmpty(shift.ItemId) ? null : await _draftStore.FindByIdAsync(shift.ItemId, cancellationToken);

        if (stored is not null && !ReferenceEquals(stored, shift))
        {
            stored.WorkerId = shift.WorkerId;
            stored.Date = shift.Date;
            stored.Start = shift.Start;
            stored.End = shift.End;
            stored.TemplateId = shift.TemplateId;
            stored.Segments = shift.Segments;
            stored.Notes = shift.Notes;
            shift = stored;
        }

        shift.PeriodId = period.ItemId;
        shift.TeamId = period.TeamId;
        shift.TimeZoneId = await GetTimeZoneAsync(period.TeamId, cancellationToken);
        shift.WorkerId = string.IsNullOrEmpty(shift.WorkerId) ? null : shift.WorkerId;
        shift.ShiftKey = string.IsNullOrEmpty(shift.ShiftKey) ? UniqueId.GenerateId() : shift.ShiftKey;

        if (stored is null)
        {
            await _draftStore.CreateAsync(shift, cancellationToken);
        }
        else
        {
            await _draftStore.UpdateAsync(shift, cancellationToken);
        }

        result.Value = shift;

        return result;
    }

    public async Task<bool> DeleteDraftShiftAsync(string id, string actor, CancellationToken cancellationToken = default)
    {
        var shift = string.IsNullOrEmpty(id) ? null : await _draftStore.FindByIdAsync(id, cancellationToken);

        if (shift is null)
        {
            return false;
        }

        var period = await _periodStore.FindByIdAsync(shift.PeriodId, cancellationToken);

        if (period?.Status == SchedulePeriodStatus.Locked)
        {
            return false;
        }

        _draftStore.Delete(shift);

        return true;
    }

    public async Task<int> ApplyPatternsAsync(string scenarioId, string actor, CancellationToken cancellationToken = default)
    {
        var (scenario, period) = await FindScenarioAndPeriodAsync(scenarioId, cancellationToken);

        if (period is null || period.Status == SchedulePeriodStatus.Locked)
        {
            return 0;
        }

        var patterns = await _patternManager.ListForTeamAsync(period.TeamId, cancellationToken);
        var templates = (await _templateManager.GetAllAsync(cancellationToken)).ToDictionary(template => template.ItemId, StringComparer.Ordinal);
        var existing = (await _draftStore.ListForScenarioAsync(scenario.ItemId, cancellationToken))
            .Where(shift => shift.WorkerId is not null)
            .Select(shift => (shift.WorkerId, shift.Date))
            .ToHashSet();
        var timeZoneId = await GetTimeZoneAsync(period.TeamId, cancellationToken);
        var workers = new Dictionary<string, WorkerProfile>(StringComparer.Ordinal);
        var added = 0;

        foreach (var pattern in patterns)
        {
            foreach (var follower in pattern.Workers)
            {
                if (!workers.TryGetValue(follower.WorkerId, out var worker))
                {
                    worker = await _workerStore.FindByIdAsync(follower.WorkerId, cancellationToken);
                    workers[follower.WorkerId] = worker;
                }

                if (worker is null || worker.Status != WorkerStatus.Active)
                {
                    continue;
                }

                for (var date = period.StartDate; date <= period.EndDate; date = date.AddDays(1))
                {
                    var days = date.DayNumber - follower.StartDate.DayNumber;

                    if (days < 0 || existing.Contains((worker.ItemId, date)) || !worker.MembershipsOn(date).Any(membership => membership.TeamId == period.TeamId))
                    {
                        continue;
                    }

                    var week = days / 7 % pattern.WeekCount;

                    foreach (var day in pattern.Days.Where(day => day.Week == week && day.Day == date.DayOfWeek))
                    {
                        if (!templates.TryGetValue(day.TemplateId, out var template) || !template.Enabled)
                        {
                            continue;
                        }

                        await _draftStore.CreateAsync(FromTemplate(scenario, period, worker.ItemId, date, template, timeZoneId), cancellationToken);
                        existing.Add((worker.ItemId, date));
                        added++;
                    }
                }
            }
        }

        return added;
    }

    public async Task<int> CopyFromPeriodAsync(string scenarioId, string sourcePeriodId, string actor, CancellationToken cancellationToken = default)
    {
        var (scenario, period) = await FindScenarioAndPeriodAsync(scenarioId, cancellationToken);
        var source = await FindPeriodAsync(sourcePeriodId, cancellationToken);

        if (period is null || source is null || period.Status == SchedulePeriodStatus.Locked)
        {
            return 0;
        }

        var offset = period.StartDate.DayNumber - source.StartDate.DayNumber;
        var timeZoneId = await GetTimeZoneAsync(period.TeamId, cancellationToken);
        var copied = 0;

        foreach (var shift in await _draftStore.ListForScenarioAsync(source.PrimaryScenarioId, cancellationToken))
        {
            var date = shift.Date.AddDays(offset);

            if (date < period.StartDate || date > period.EndDate)
            {
                continue;
            }

            await _draftStore.CreateAsync(new DraftShift
            {
                ItemId = UniqueId.GenerateId(),
                ScenarioId = scenario.ItemId,
                PeriodId = period.ItemId,
                TeamId = period.TeamId,
                WorkerId = shift.WorkerId,
                Date = date,
                Start = shift.Start.AddDays(offset),
                End = shift.End.AddDays(offset),
                TimeZoneId = timeZoneId,
                TemplateId = shift.TemplateId,
                Segments = shift.Segments.Select(segment => new DraftShiftSegment
                {
                    ActivityCodeId = segment.ActivityCodeId,
                    Start = segment.Start.AddDays(offset),
                    End = segment.End.AddDays(offset),
                }).ToList(),
                Notes = shift.Notes,
                ShiftKey = UniqueId.GenerateId(),
            }, cancellationToken);

            copied++;
        }

        return copied;
    }

    public Task<AutoScheduleResult> AutoScheduleAsync(string scenarioId, AutoScheduleOptions options, string actor, CancellationToken cancellationToken = default)
        => _autoScheduler.RunAsync(scenarioId, options, actor, cancellationToken);

    public Task<PlanningPublishResult> PublishAsync(string periodId, string actor, CancellationToken cancellationToken = default)
        => PublishAsync(periodId, actor, null, cancellationToken);

    public async Task<IReadOnlyList<LaborRuleViolation>> CheckPublishAsync(string periodId, CancellationToken cancellationToken = default)
    {
        var period = await FindPeriodAsync(periodId, cancellationToken);
        var team = period is null ? null : await _teamStore.FindByIdAsync(period.TeamId, cancellationToken);

        if (team is null || string.IsNullOrEmpty(team.CalendarId) || !_publishValidators.Any())
        {
            return [];
        }

        var timeZoneId = await GetTimeZoneAsync(team.ItemId, cancellationToken);
        var drafts = await _draftStore.ListForScenarioAsync(period.PrimaryScenarioId, cancellationToken);
        var codes = (await _activityCodeStore.GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);
        var workers = (await _workerStore.GetAsync(drafts.Select(draft => draft.WorkerId).Where(id => id is not null).Distinct(StringComparer.Ordinal), cancellationToken))
            .ToDictionary(worker => worker.ItemId, StringComparer.Ordinal);
        var published = await ListPublishedAsync(team.CalendarId, period, timeZoneId, cancellationToken);
        var context = await ValidatePublishAsync(period, timeZoneId, drafts, workers, published, codes, null, null, true, cancellationToken);

        return context.Violations.ToArray();
    }

    public async Task<PlanningPublishResult> PublishAsync(string periodId, string actor, string overrideReason, CancellationToken cancellationToken = default)
    {
        var result = new PlanningPublishResult();
        var period = await FindPeriodAsync(periodId, cancellationToken);
        var team = period is null ? null : await _teamStore.FindByIdAsync(period.TeamId, cancellationToken);

        if (period is null || team is null)
        {
            result.Errors.Add(S["The schedule doesn't exist."]);

            return result;
        }

        if (period.Status == SchedulePeriodStatus.Locked)
        {
            result.Errors.Add(S["The schedule is locked."]);

            return result;
        }

        await _calendarService.EnsureCalendarAsync(team, cancellationToken);

        var calendarId = team.CalendarId;
        var timeZoneId = await GetTimeZoneAsync(team.ItemId, cancellationToken);
        var version = period.PublishedVersion + 1;
        var republish = period.PublishedVersion > 0;
        var now = _clock.UtcNow;
        var drafts = await _draftStore.ListForScenarioAsync(period.PrimaryScenarioId, cancellationToken);
        var codes = (await _activityCodeStore.GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);
        var workers = (await _workerStore.GetAsync(drafts.Select(draft => draft.WorkerId).Where(id => id is not null).Distinct(StringComparer.Ordinal), cancellationToken))
            .ToDictionary(worker => worker.ItemId, StringComparer.Ordinal);
        var published = await ListPublishedAsync(calendarId, period, timeZoneId, cancellationToken);
        var options = new CalendarChangeOptions { Actor = actor ?? WorkforceConstants.Source, SendInvitations = false, SkipConflictCheck = true };
        var calendar = await _calendars.FindByIdAsync(calendarId, cancellationToken);
        var openDrafts = new List<DraftShift>();

        if (_publishValidators.Any())
        {
            var validation = await ValidatePublishAsync(period, timeZoneId, drafts, workers, published, codes, overrideReason, actor, false, cancellationToken);

            result.Violations = validation.Violations;

            if (validation.Violations.Any(violation => violation.Enforcement == LaborRuleEnforcement.Block))
            {
                var blocking = validation.Violations.Count(violation => violation.Enforcement == LaborRuleEnforcement.Block);

                result.Errors.Add(blocking == 1
                    ? S["The schedule breaks a labor rule that stops it being published: {0}", validation.Violations.First(violation => violation.Enforcement == LaborRuleEnforcement.Block).Message]
                    : S["The schedule breaks {0} labor rules that stop it being published; the board lists them.", blocking]);

                return result;
            }

            if (validation.Violations.Any(violation => violation.Enforcement == LaborRuleEnforcement.RequireOverride) && string.IsNullOrWhiteSpace(overrideReason))
            {
                result.NeedsOverride = true;
                result.Errors.Add(S["The schedule breaks labor rules that need an override. Give the reason to publish it anyway."]);

                return result;
            }

            foreach (var validator in _publishValidators)
            {
                await validator.ProceedingAsync(validation, cancellationToken);
            }
        }

        foreach (var draft in drafts)
        {
            if (draft.WorkerId is null || !workers.TryGetValue(draft.WorkerId, out var worker))
            {
                result.Open++;

                if (draft.WorkerId is null)
                {
                    openDrafts.Add(draft);
                }

                continue;
            }

            var data = ToShiftData(draft, period, version, codes, timeZoneId);
            var startUtc = CalendarTime.ToUtc(draft.Start, timeZoneId);
            var endUtc = CalendarTime.ToUtc(draft.End, timeZoneId);

            if (published.Remove(draft.ShiftKey, out var existing))
            {
                var before = existing.TryGet<ShiftData>(out var previous) ? previous : null;
                var kind = before?.WorkerId != draft.WorkerId
                    ? ScheduleChangeKind.Reassigned
                    : existing.StartUtc != startUtc || existing.EndUtc != endUtc
                        ? ScheduleChangeKind.Moved
                        : SameSegments(before, data)
                            ? (ScheduleChangeKind?)null
                            : ScheduleChangeKind.SegmentsChanged;

                if (kind is null)
                {
                    continue;
                }

                var beforeStart = existing.StartUtc;
                var beforeEnd = existing.EndUtc;
                var updated = await _eventManager.UpdateAsync(existing, target => Apply(target, draft, worker, data, timeZoneId), options, cancellationToken);

                if (!updated.Succeeded)
                {
                    result.Errors.Add(S["The shift of {0} on {1} could not be published: {2}", worker.DisplayName, draft.Date.ToString("d", null), string.Join(" ", updated.Errors.Select(error => error.Message))]);

                    continue;
                }

                result.Updated++;

                if (republish)
                {
                    result.Changes.Add(Change(period, draft, kind.Value, beforeStart, beforeEnd, startUtc, endUtc, before?.WorkerId, version, actor, now));
                }

                continue;
            }

            var calendarEvent = await _eventManager.NewAsync(calendar, WorkforceConstants.EventTypes.Shift, cancellationToken: cancellationToken);

            Apply(calendarEvent, draft, worker, data, timeZoneId);
            calendarEvent.Source = WorkforceConstants.Source;
            calendarEvent.SourceId = draft.ShiftKey;
            calendarEvent.Editable = false;

            var created = await _eventManager.CreateAsync(calendarEvent, options, cancellationToken);

            if (!created.Succeeded)
            {
                result.Errors.Add(S["The shift of {0} on {1} could not be published: {2}", worker.DisplayName, draft.Date.ToString("d", null), string.Join(" ", created.Errors.Select(error => error.Message))]);

                continue;
            }

            result.Added++;

            if (republish)
            {
                result.Changes.Add(Change(period, draft, ScheduleChangeKind.Added, null, null, startUtc, endUtc, null, version, actor, now));
            }
        }

        // Whatever is left on the calendar is no longer in the schedule.
        foreach (var (shiftKey, calendarEvent) in published)
        {
            var before = calendarEvent.TryGet<ShiftData>(out var data) ? data : null;
            var cancelled = await _eventManager.CancelAsync(calendarEvent, options, cancellationToken);

            if (!cancelled.Succeeded)
            {
                continue;
            }

            result.Removed++;

            if (republish)
            {
                result.Changes.Add(new ScheduleChange
                {
                    ItemId = UniqueId.GenerateId(),
                    PeriodId = period.ItemId,
                    TeamId = period.TeamId,
                    WorkerId = before?.WorkerId,
                    ShiftKey = shiftKey,
                    Kind = ScheduleChangeKind.Removed,
                    BeforeStartUtc = calendarEvent.StartUtc,
                    BeforeEndUtc = calendarEvent.EndUtc,
                    Source = ScheduleChangeSources.Publish,
                    PublishedVersion = version,
                    ChangedBy = actor,
                    ChangedUtc = now,
                });
            }
        }

        // The shifts with nobody are offered to the team as open shifts.
        await _openShiftPosting.SyncAsync(period, openDrafts, timeZoneId, actor, cancellationToken);

        foreach (var change in result.Changes)
        {
            await _changeRecorder.RecordAsync(change, cancellationToken);
        }

        period.PublishedVersion = version;
        period.PublishedUtc = now;
        period.PublishedBy = actor;
        period.Status = SchedulePeriodStatus.Published;
        await _periodStore.UpdateAsync(period, cancellationToken);

        var context = new SchedulePublishedContext { Period = period, Result = result, Actor = actor };

        foreach (var handler in _publishHandlers)
        {
            await handler.PublishedAsync(context, cancellationToken);
        }

        return result;
    }

    public Task<IReadOnlyList<ScheduleChange>> ListChangesAsync(string periodId, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(periodId)
            ? Task.FromResult<IReadOnlyList<ScheduleChange>>([])
            : _changeStore.ListForPeriodAsync(periodId, cancellationToken);
    }

    // Lists the changes the publish would make, then lets the validators check the schedule.
    private async Task<SchedulePublishValidationContext> ValidatePublishAsync(SchedulePeriod period, string timeZoneId, IReadOnlyList<DraftShift> drafts, Dictionary<string, WorkerProfile> workers, Dictionary<string, CalendarEvent> published, Dictionary<string, ActivityCode> codes, string overrideReason, string actor, bool checkOnly, CancellationToken cancellationToken)
    {
        var version = period.PublishedVersion + 1;
        var changes = new List<SchedulePublishChange>();
        var assigned = drafts.Where(draft => draft.WorkerId is not null && workers.ContainsKey(draft.WorkerId)).ToArray();

        if (period.PublishedVersion > 0)
        {
            var kept = new HashSet<string>(StringComparer.Ordinal);

            foreach (var draft in assigned)
            {
                var startUtc = CalendarTime.ToUtc(draft.Start, timeZoneId);

                if (!published.TryGetValue(draft.ShiftKey, out var existing))
                {
                    changes.Add(new SchedulePublishChange { Kind = ScheduleChangeKind.Added, ShiftKey = draft.ShiftKey, WorkerId = draft.WorkerId, AfterStartUtc = startUtc });

                    continue;
                }

                kept.Add(draft.ShiftKey);

                var before = existing.TryGet<ShiftData>(out var previous) ? previous : null;
                var kind = before?.WorkerId != draft.WorkerId
                    ? ScheduleChangeKind.Reassigned
                    : existing.StartUtc != startUtc || existing.EndUtc != CalendarTime.ToUtc(draft.End, timeZoneId)
                        ? ScheduleChangeKind.Moved
                        : SameSegments(before, ToShiftData(draft, period, version, codes, timeZoneId))
                            ? (ScheduleChangeKind?)null
                            : ScheduleChangeKind.SegmentsChanged;

                if (kind.HasValue)
                {
                    changes.Add(new SchedulePublishChange { Kind = kind.Value, ShiftKey = draft.ShiftKey, WorkerId = draft.WorkerId, PreviousWorkerId = before?.WorkerId, BeforeStartUtc = existing.StartUtc, AfterStartUtc = startUtc });
                }
            }

            foreach (var (shiftKey, calendarEvent) in published.Where(pair => !kept.Contains(pair.Key)))
            {
                changes.Add(new SchedulePublishChange
                {
                    Kind = ScheduleChangeKind.Removed,
                    ShiftKey = shiftKey,
                    WorkerId = calendarEvent.TryGet<ShiftData>(out var data) ? data.WorkerId : null,
                    BeforeStartUtc = calendarEvent.StartUtc,
                });
            }
        }

        var context = new SchedulePublishValidationContext
        {
            Period = period,
            TimeZoneId = timeZoneId,
            Drafts = assigned,
            Workers = workers,
            PublishedShiftKeys = new HashSet<string>(published.Keys, StringComparer.Ordinal),
            Changes = changes,
            OverrideReason = overrideReason,
            Actor = actor,
            CheckOnly = checkOnly,
        };

        foreach (var validator in _publishValidators)
        {
            await validator.ValidateAsync(context, cancellationToken);
        }

        return context;
    }

    private Task<string> GetTimeZoneAsync(string teamId, CancellationToken cancellationToken)
        => _timeZones.GetAsync(teamId, cancellationToken);

    private async Task<Dictionary<string, CalendarEvent>> ListPublishedAsync(string calendarId, SchedulePeriod period, string timeZoneId, CancellationToken cancellationToken)
    {
        var occurrences = await _queryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            CalendarIds = [calendarId],
            FromUtc = CalendarTime.ToUtc(period.StartDate.AddDays(-1).ToDateTime(TimeOnly.MinValue), timeZoneId),
            ToUtc = CalendarTime.ToUtc(period.EndDate.AddDays(2).ToDateTime(TimeOnly.MinValue), timeZoneId),
            EventTypes = [WorkforceConstants.EventTypes.Shift],
            Sources = [WorkforceConstants.Source],
        }, cancellationToken);

        var published = new Dictionary<string, CalendarEvent>(StringComparer.Ordinal);

        foreach (var occurrence in occurrences)
        {
            if (!string.IsNullOrEmpty(occurrence.Event.SourceId) && occurrence.Event.TryGet<ShiftData>(out var data) && data.ScheduleId == period.ItemId)
            {
                published.TryAdd(occurrence.Event.SourceId, occurrence.Event);
            }
        }

        return published;
    }

    private async Task<(ScheduleScenario Scenario, SchedulePeriod Period)> FindScenarioAndPeriodAsync(string scenarioId, CancellationToken cancellationToken)
    {
        var scenario = string.IsNullOrEmpty(scenarioId) ? null : await _scenarioStore.FindByIdAsync(scenarioId, cancellationToken);
        var period = scenario is null ? null : await _periodStore.FindByIdAsync(scenario.PeriodId, cancellationToken);

        return (scenario, period);
    }

    internal static DraftShift FromTemplate(ScheduleScenario scenario, SchedulePeriod period, string workerId, DateOnly date, ShiftTemplate template, string timeZoneId)
    {
        var start = date.ToDateTime(template.Start);

        return new DraftShift
        {
            ItemId = UniqueId.GenerateId(),
            ScenarioId = scenario.ItemId,
            PeriodId = period.ItemId,
            TeamId = period.TeamId,
            WorkerId = workerId,
            Date = date,
            Start = start,
            End = start.AddMinutes(template.LengthMinutes),
            TimeZoneId = timeZoneId,
            TemplateId = template.ItemId,
            Segments = template.Segments.Select(segment => new DraftShiftSegment
            {
                ActivityCodeId = segment.ActivityCodeId,
                Start = start.AddMinutes(segment.OffsetMinutes),
                End = start.AddMinutes(segment.OffsetMinutes + segment.LengthMinutes),
            }).ToList(),
            ShiftKey = UniqueId.GenerateId(),
        };
    }

    internal static ShiftData ToShiftData(DraftShift draft, SchedulePeriod period, int version, IReadOnlyDictionary<string, ActivityCode> codes, string timeZoneId)
        => ShiftEvents.ToShiftData(draft.WorkerId, draft.TeamId ?? period.TeamId, period.ItemId, draft.TemplateId, draft.ShiftKey, version, draft.Segments, codes, timeZoneId);

    private static void Apply(CalendarEvent calendarEvent, DraftShift draft, WorkerProfile worker, ShiftData data, string timeZoneId)
        => ShiftEvents.Apply(calendarEvent, worker, draft.Start, draft.End, draft.Notes, data, timeZoneId);

    private static bool SameSegments(ShiftData before, ShiftData after)
    {
        if (before is null || before.Segments.Count != after.Segments.Count)
        {
            return false;
        }

        return before.Segments.Zip(after.Segments).All(pair => pair.First.ActivityCodeId == pair.Second.ActivityCodeId
            && pair.First.StartUtc == pair.Second.StartUtc
            && pair.First.EndUtc == pair.Second.EndUtc);
    }

    private static ScheduleChange Change(SchedulePeriod period, DraftShift draft, ScheduleChangeKind kind, DateTime? beforeStart, DateTime? beforeEnd, DateTime afterStart, DateTime afterEnd, string previousWorkerId, int version, string actor, DateTime now)
    {
        return new ScheduleChange
        {
            ItemId = UniqueId.GenerateId(),
            PeriodId = period.ItemId,
            TeamId = period.TeamId,
            WorkerId = draft.WorkerId,
            ShiftKey = draft.ShiftKey,
            Kind = kind,
            BeforeStartUtc = beforeStart,
            BeforeEndUtc = beforeEnd,
            AfterStartUtc = afterStart,
            AfterEndUtc = afterEnd,
            PreviousWorkerId = kind == ScheduleChangeKind.Reassigned ? previousWorkerId : null,
            Source = ScheduleChangeSources.Publish,
            PublishedVersion = version,
            ChangedBy = actor,
            ChangedUtc = now,
        };
    }
}
