using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="ITimesheetService"/>. A timesheet is worked out under its worker's time clock lock, and its
/// attendance exceptions are kept in step: new ones are raised, open ones the punches no longer show are dropped,
/// and decided ones stay.
/// </summary>
internal sealed class DefaultTimesheetService : ITimesheetService
{
    private readonly TimesheetStore _store;
    private readonly AttendanceExceptionStore _exceptionStore;
    private readonly PayPeriodCalendarStore _calendarStore;
    private readonly PayCodeStore _payCodeStore;
    private readonly WorkerStore _workerStore;
    private readonly ITimeClockService _timeClock;
    private readonly ITimesheetCalculator _calculator;
    private readonly WorkerShiftReader _shiftReader;
    private readonly WorkerTimeZones _timeZones;
    private readonly TimeOffLocks _locks;
    private readonly IWorkforceNotifier _notifier;
    private readonly IEnumerable<IAttendancePointsHandler> _pointsHandlers;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public DefaultTimesheetService(
        TimesheetStore store,
        AttendanceExceptionStore exceptionStore,
        PayPeriodCalendarStore calendarStore,
        PayCodeStore payCodeStore,
        WorkerStore workerStore,
        ITimeClockService timeClock,
        ITimesheetCalculator calculator,
        WorkerShiftReader shiftReader,
        WorkerTimeZones timeZones,
        TimeOffLocks locks,
        IWorkforceNotifier notifier,
        IEnumerable<IAttendancePointsHandler> pointsHandlers,
        IServiceProvider serviceProvider,
        ISession session,
        IClock clock,
        ILogger<DefaultTimesheetService> logger,
        IStringLocalizer<DefaultTimesheetService> stringLocalizer)
    {
        _store = store;
        _exceptionStore = exceptionStore;
        _calendarStore = calendarStore;
        _payCodeStore = payCodeStore;
        _workerStore = workerStore;
        _timeClock = timeClock;
        _calculator = calculator;
        _shiftReader = shiftReader;
        _timeZones = timeZones;
        _locks = locks;
        _notifier = notifier;
        _pointsHandlers = pointsHandlers;
        _serviceProvider = serviceProvider;
        _session = session;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    public async Task<PlanningResult<Timesheet>> GetForDateAsync(WorkerProfile worker, DateOnly date, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var result = new PlanningResult<Timesheet>();
        var policy = await _timeClock.GetPolicyAsync(worker, date, cancellationToken);

        if (policy is null)
        {
            result.Fail(S["{0}'s team has no time clock policy, so they have no timesheet.", worker.DisplayName]);

            return result;
        }

        var calendar = await _calendarStore.FindByIdAsync(policy.PayPeriodCalendarId, cancellationToken);

        if (calendar is null)
        {
            result.Fail(S["The time clock policy {0} has no pay periods.", policy.Name]);

            return result;
        }

        var (start, end) = calendar.PeriodOn(date);
        var lockers = await _locks.LockAsync([TimeOffLocks.TimeClock(worker.ItemId)]);

        if (lockers is null)
        {
            result.Fail(S["{0}'s timesheet is being saved. Try again.", worker.DisplayName]);

            return result;
        }

        try
        {
            var timesheet = await _store.FindForPeriodAsync(worker.ItemId, calendar.ItemId, start, cancellationToken);

            if (timesheet is null)
            {
                timesheet = new Timesheet
                {
                    ItemId = UniqueId.GenerateId(),
                    WorkerId = worker.ItemId,
                    TeamId = WorkerTimeZones.TeamOn(worker, start),
                    PayPeriodCalendarId = calendar.ItemId,
                    PeriodStart = start,
                    PeriodEnd = end,
                    CreatedUtc = _clock.UtcNow,
                };

                await _store.CreateAsync(timesheet, cancellationToken);
            }

            if (timesheet.IsEditable)
            {
                await RecalculateAsync(timesheet, worker, policy, calendar, cancellationToken);
            }

            await _session.SaveChangesAsync(cancellationToken);

            result.Value = timesheet;

            return result;
        }
        finally
        {
            await TimeOffLocks.ReleaseAsync(lockers);
        }
    }

    public async Task<Timesheet> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(id) ? null : await _store.FindByIdAsync(id, cancellationToken);
    }

    public async Task<(Timesheet Timesheet, TimesheetCalculation Calculation)> CalculateAsync(string id, CancellationToken cancellationToken = default)
    {
        var timesheet = await FindAsync(id, cancellationToken);
        var worker = timesheet is null ? null : await _workerStore.FindByIdAsync(timesheet.WorkerId, cancellationToken);

        if (worker is null)
        {
            return (timesheet, null);
        }

        var (policy, calendar) = await PolicyAsync(worker, timesheet, cancellationToken);

        if (policy is null || calendar is null)
        {
            return (timesheet, null);
        }

        if (!timesheet.IsEditable)
        {
            return (timesheet, await BuildAsync(timesheet, worker, policy, calendar, cancellationToken));
        }

        var lockers = await _locks.LockAsync([TimeOffLocks.TimeClock(worker.ItemId)]);

        if (lockers is null)
        {
            return (timesheet, await BuildAsync(timesheet, worker, policy, calendar, cancellationToken));
        }

        try
        {
            var calculation = await RecalculateAsync(timesheet, worker, policy, calendar, cancellationToken);

            await _session.SaveChangesAsync(cancellationToken);

            return (timesheet, calculation);
        }
        finally
        {
            await TimeOffLocks.ReleaseAsync(lockers);
        }
    }

    public Task<(int Count, IReadOnlyList<Timesheet> Items)> PageAsync(TimesheetQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _store.PageAsync(query, skip, take, cancellationToken);
    }

    public Task<PlanningResult<Timesheet>> SubmitAsync(string id, string attestation, string actor, CancellationToken cancellationToken = default)
    {
        return ChangeAsync(id, recalculate: true, async (timesheet, worker, result) =>
        {
            if (!timesheet.IsEditable)
            {
                result.Fail(S["This timesheet is {0} and can't be submitted.", StatusName(timesheet.Status)]);

                return;
            }

            if (string.IsNullOrWhiteSpace(attestation))
            {
                result.Fail(S["Confirm the hours are right to submit the timesheet."]);

                return;
            }

            timesheet.Status = TimesheetStatus.Submitted;
            timesheet.Attestation = attestation.Trim();
            timesheet.SubmittedUtc = _clock.UtcNow;
            timesheet.RejectionReason = null;

            await Task.CompletedTask;
        }, cancellationToken);
    }

    public Task<PlanningResult<Timesheet>> ApproveAsync(string id, string actor, CancellationToken cancellationToken = default)
    {
        return ChangeAsync(id, recalculate: true, async (timesheet, worker, result) =>
        {
            if (timesheet.Status is TimesheetStatus.Approved or TimesheetStatus.Locked)
            {
                result.Fail(S["This timesheet is already {0}.", StatusName(timesheet.Status)]);

                return;
            }

            timesheet.Status = TimesheetStatus.Approved;
            timesheet.DecidedBy = actor;
            timesheet.DecidedUtc = _clock.UtcNow;
            timesheet.RejectionReason = null;

            await NotifyAsync(worker, timesheet, S["Your timesheet was approved"], S["Your hours for {0} were approved.", PeriodText(timesheet)], cancellationToken);
        }, cancellationToken);
    }

    public Task<PlanningResult<Timesheet>> RejectAsync(string id, string reason, string actor, CancellationToken cancellationToken = default)
    {
        return ChangeAsync(id, recalculate: false, async (timesheet, worker, result) =>
        {
            if (timesheet.Status != TimesheetStatus.Submitted)
            {
                result.Fail(S["Only a submitted timesheet can be sent back."]);

                return;
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                result.Fail(S["Say what to fix."]);

                return;
            }

            timesheet.Status = TimesheetStatus.Rejected;
            timesheet.RejectionReason = reason.Trim();
            timesheet.DecidedBy = actor;
            timesheet.DecidedUtc = _clock.UtcNow;

            await NotifyAsync(worker, timesheet, S["Your timesheet was sent back"], S["Fix your hours for {0}: {1}", PeriodText(timesheet), timesheet.RejectionReason], cancellationToken);
        }, cancellationToken);
    }

    public Task<PlanningResult<Timesheet>> ReopenAsync(string id, string actor, CancellationToken cancellationToken = default)
    {
        return ChangeAsync(id, recalculate: false, (timesheet, worker, result) =>
        {
            if (timesheet.Status == TimesheetStatus.Locked)
            {
                result.Fail(S["A locked timesheet has gone to payroll and can't be reopened. Add a line to the next period instead."]);
            }
            else if (timesheet.Status is not (TimesheetStatus.Approved or TimesheetStatus.Submitted))
            {
                result.Fail(S["This timesheet is already open."]);
            }
            else
            {
                timesheet.Status = TimesheetStatus.Reopened;
                timesheet.DecidedBy = actor;
                timesheet.DecidedUtc = _clock.UtcNow;
            }

            return Task.CompletedTask;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<Timesheet>> LockPeriodAsync(string payPeriodCalendarId, DateOnly periodStart, string actor, CancellationToken cancellationToken = default)
    {
        var approved = await _store.ListForPeriodAsync(payPeriodCalendarId, periodStart, TimesheetStatus.Approved, cancellationToken);
        var now = _clock.UtcNow;
        var locked = new List<Timesheet>();

        foreach (var timesheet in approved)
        {
            // A period still running in the worker's time zone stays open, so their punches today still count.
            var worker = await _workerStore.FindByIdAsync(timesheet.WorkerId, cancellationToken);
            var zone = worker is null ? "UTC" : await _timeZones.GetAsync(worker, timesheet.TeamId, cancellationToken);

            if (DateOnly.FromDateTime(CalendarTime.ToLocal(now, zone)) <= timesheet.PeriodEnd)
            {
                continue;
            }

            timesheet.Status = TimesheetStatus.Locked;
            timesheet.LockedUtc = now;
            await _store.UpdateAsync(timesheet, cancellationToken);
            locked.Add(timesheet);
        }

        await _session.SaveChangesAsync(cancellationToken);

        return locked;
    }

    public Task<PlanningResult<Timesheet>> AddLineAsync(string id, TimesheetLine line, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(line);

        return ChangeAsync(id, recalculate: true, async (timesheet, worker, result) =>
        {
            if (!timesheet.IsEditable)
            {
                result.Fail(S["This timesheet is {0}. Reopen it to add a line.", StatusName(timesheet.Status)]);

                return;
            }

            if (line.Date < timesheet.PeriodStart || line.Date > timesheet.PeriodEnd)
            {
                result.Fail(S["The day is outside this pay period."]);
            }

            if (line.Hours is <= 0 or > 24)
            {
                result.Fail(S["A line has more than 0 and up to 24 hours."]);
            }

            if (string.IsNullOrEmpty(line.PayCodeId) || await _payCodeStore.FindByIdAsync(line.PayCodeId, cancellationToken) is null)
            {
                result.Fail(S["Choose the pay code."]);
            }

            if (!result.Succeeded)
            {
                return;
            }

            timesheet.Lines.Add(new TimesheetLine
            {
                Id = UniqueId.GenerateId(),
                Date = line.Date,
                PayCodeId = line.PayCodeId,
                Hours = Math.Round(line.Hours, 2),
                ActivityCodeId = line.ActivityCodeId,
                Job = line.Job,
                Source = TimesheetLineSource.Manual,
                Note = string.IsNullOrWhiteSpace(line.Note) ? null : line.Note.Trim(),
            });
        }, cancellationToken);
    }

    public Task<PlanningResult<Timesheet>> RemoveLineAsync(string id, string lineId, string actor, CancellationToken cancellationToken = default)
    {
        return ChangeAsync(id, recalculate: true, (timesheet, worker, result) =>
        {
            if (!timesheet.IsEditable)
            {
                result.Fail(S["This timesheet is {0}. Reopen it to remove a line.", StatusName(timesheet.Status)]);
            }
            else if (timesheet.Lines.FirstOrDefault(line => line.Source == TimesheetLineSource.Manual && line.Id == lineId) is { } line)
            {
                timesheet.Lines.Remove(line);
            }
            else
            {
                result.Fail(S["The line wasn't found."]);
            }

            return Task.CompletedTask;
        }, cancellationToken);
    }

    /// <summary>
    /// Changes a timesheet under its worker's lock, working it out again after the change when asked.
    /// </summary>
    private async Task<PlanningResult<Timesheet>> ChangeAsync(string id, bool recalculate, Func<Timesheet, WorkerProfile, PlanningResult<Timesheet>, Task> change, CancellationToken cancellationToken)
    {
        var result = new PlanningResult<Timesheet>();
        var timesheet = await FindAsync(id, cancellationToken);
        var worker = timesheet is null ? null : await _workerStore.FindByIdAsync(timesheet.WorkerId, cancellationToken);

        if (worker is null)
        {
            result.Fail(S["The timesheet wasn't found."]);

            return result;
        }

        var lockers = await _locks.LockAsync([TimeOffLocks.TimeClock(worker.ItemId)]);

        if (lockers is null)
        {
            result.Fail(S["{0}'s timesheet is being saved. Try again.", worker.DisplayName]);

            return result;
        }

        try
        {
            var (policy, calendar) = await PolicyAsync(worker, timesheet, cancellationToken);
            var editableBefore = timesheet.IsEditable;

            // Lines are worked out before a status change, so what is submitted or approved is current.
            if (recalculate && editableBefore && policy is not null && calendar is not null)
            {
                await RecalculateAsync(timesheet, worker, policy, calendar, cancellationToken);
            }

            await change(timesheet, worker, result);

            if (!result.Succeeded)
            {
                return result;
            }

            if (recalculate && timesheet.IsEditable && policy is not null && calendar is not null)
            {
                await RecalculateAsync(timesheet, worker, policy, calendar, cancellationToken);
            }

            await _store.UpdateAsync(timesheet, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            result.Value = timesheet;

            return result;
        }
        finally
        {
            await TimeOffLocks.ReleaseAsync(lockers);
        }
    }

    private async Task<(TimeClockPolicy Policy, PayPeriodCalendar Calendar)> PolicyAsync(WorkerProfile worker, Timesheet timesheet, CancellationToken cancellationToken)
    {
        var policy = await _timeClock.GetPolicyAsync(worker, timesheet.PeriodStart, cancellationToken)
            ?? await _timeClock.GetPolicyAsync(worker, timesheet.PeriodEnd, cancellationToken);
        var calendar = await _calendarStore.FindByIdAsync(timesheet.PayPeriodCalendarId, cancellationToken);

        return (policy, calendar);
    }

    private async Task<TimesheetCalculation> RecalculateAsync(Timesheet timesheet, WorkerProfile worker, TimeClockPolicy policy, PayPeriodCalendar calendar, CancellationToken cancellationToken)
    {
        var calculation = await BuildAsync(timesheet, worker, policy, calendar, cancellationToken);

        timesheet.Lines = calculation.Lines;
        timesheet.WorkedMinutes = calculation.WorkedMinutes;
        timesheet.CalculatedUtc = _clock.UtcNow;

        var existing = (await _exceptionStore.ListForWorkerAsync(worker.ItemId, timesheet.PeriodStart, timesheet.PeriodEnd, cancellationToken))
            .GroupBy(exception => exception.Key ?? exception.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var raised = new List<AttendanceException>();
        var open = 0;

        foreach (var exception in calculation.Exceptions)
        {
            if (existing.Remove(exception.Key, out var stored))
            {
                if (stored.Status == AttendanceExceptionStatus.Open)
                {
                    open++;

                    if (stored.Minutes != exception.Minutes || stored.Points != exception.Points)
                    {
                        stored.Minutes = exception.Minutes;
                        stored.Points = exception.Points;
                        await _exceptionStore.UpdateAsync(stored, cancellationToken);
                    }
                }

                continue;
            }

            exception.ItemId = UniqueId.GenerateId();
            exception.WorkerId = worker.ItemId;
            exception.TeamId = timesheet.TeamId;
            exception.CreatedUtc = _clock.UtcNow;
            await _exceptionStore.CreateAsync(exception, cancellationToken);
            raised.Add(exception);
            open++;
        }

        // Open exceptions the punches no longer show, such as a missed punch that was added, are gone.
        foreach (var stale in existing.Values.Where(exception => exception.Status == AttendanceExceptionStatus.Open))
        {
            await _exceptionStore.DeleteAsync(stale, cancellationToken);
        }

        timesheet.OpenExceptions = open;

        await _store.UpdateAsync(timesheet, cancellationToken);
        await AfterRaisedAsync(worker, policy, raised, cancellationToken);

        return calculation;
    }

    private async Task<TimesheetCalculation> BuildAsync(Timesheet timesheet, WorkerProfile worker, TimeClockPolicy policy, PayPeriodCalendar calendar, CancellationToken cancellationToken)
    {
        var zone = await _timeZones.GetAsync(worker, WorkerTimeZones.TeamOn(worker, timesheet.PeriodStart), cancellationToken);
        var weekStart = calendar.WeekOf(timesheet.PeriodStart);
        var fromUtc = CalendarTime.ToUtc(weekStart.AddDays(-1).ToDateTime(TimeOnly.MinValue), zone);
        var periodStartUtc = CalendarTime.ToUtc(timesheet.PeriodStart.ToDateTime(TimeOnly.MinValue), zone);
        var periodEndUtc = CalendarTime.ToUtc(timesheet.PeriodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var punches = await _timeClock.ListAsync(worker.ItemId, fromUtc, periodEndUtc.AddDays(1), cancellationToken);
        var shifts = new List<TimesheetShift>();

        foreach (var shift in await _shiftReader.ListAsync(worker, periodStartUtc, periodEndUtc, cancellationToken))
        {
            shifts.Add(new TimesheetShift
            {
                Key = ShiftEvents.TryRead(shift, out var data) && !string.IsNullOrEmpty(data.ShiftKey) ? data.ShiftKey : shift.ItemId,
                StartUtc = shift.StartUtc,
                EndUtc = shift.EndUtc,
            });
        }

        var payCodes = await _payCodeStore.GetAllAsync(cancellationToken);

        return _calculator.Calculate(new TimesheetCalculationInput
        {
            Policy = policy,
            TimeZoneId = zone,
            PeriodStart = timesheet.PeriodStart,
            PeriodEnd = timesheet.PeriodEnd,
            WeekStart = calendar.WeekStart,
            Punches = punches,
            Shifts = shifts,
            TimeOff = await TimeOffAsync(worker, timesheet, payCodes, cancellationToken),
            PredictabilityPay = await PredictabilityPayAsync(worker, cancellationToken),
            PayCodes = [.. payCodes],
            ManualLines = timesheet.Lines.Where(line => line.Source == TimesheetLineSource.Manual).ToArray(),
            NowUtc = _clock.UtcNow,
        });
    }

    private async Task<IReadOnlyList<TimesheetTimeOff>> TimeOffAsync(WorkerProfile worker, Timesheet timesheet, IEnumerable<PayCode> payCodes, CancellationToken cancellationToken)
    {
        var requestStore = _serviceProvider.GetService<TimeOffRequestStore>();
        var typeStore = _serviceProvider.GetService<TimeOffTypeStore>();

        if (requestStore is null || typeStore is null)
        {
            return [];
        }

        var codesByCode = payCodes.Where(code => code.Enabled).ToDictionary(code => code.Code ?? string.Empty, code => code, StringComparer.OrdinalIgnoreCase);
        var unpaid = payCodes.FirstOrDefault(code => code.Enabled && code.Kind == PayCodeKind.Unpaid);
        var days = new List<TimesheetTimeOff>();

        foreach (var request in await requestStore.ListLiveForWorkerAsync(worker.ItemId, timesheet.PeriodStart, timesheet.PeriodEnd, cancellationToken))
        {
            if (request.Status != TimeOffRequestStatus.Approved)
            {
                continue;
            }

            var type = await typeStore.FindByIdAsync(request.TypeId, cancellationToken);
            var payCode = type is null || string.IsNullOrEmpty(type.PayCode) ? null : codesByCode.GetValueOrDefault(type.PayCode);

            foreach (var day in request.Days.Where(day => day.Date >= timesheet.PeriodStart && day.Date <= timesheet.PeriodEnd))
            {
                var paid = type?.IsPaid != false;

                days.Add(new TimesheetTimeOff
                {
                    Date = day.Date,
                    RequestId = request.ItemId,
                    Hours = paid || unpaid is not null ? Math.Round(day.Minutes / 60m, 2) : 0,
                    PayCodeId = paid ? payCode?.ItemId : unpaid?.ItemId,
                });
            }
        }

        return days;
    }

    private async Task<IReadOnlyList<PredictabilityPayEntry>> PredictabilityPayAsync(WorkerProfile worker, CancellationToken cancellationToken)
    {
        var store = _serviceProvider.GetService<PredictabilityPayStore>();

        if (store is null)
        {
            return [];
        }

        var approved = await store.PageAsync(new PredictabilityPayQuery { WorkerId = worker.ItemId, Status = PredictabilityPayStatus.Approved }, 0, 1000, cancellationToken);
        var exported = await store.PageAsync(new PredictabilityPayQuery { WorkerId = worker.ItemId, Status = PredictabilityPayStatus.Exported }, 0, 1000, cancellationToken);

        return [.. approved.Items, .. exported.Items];
    }

    private async Task AfterRaisedAsync(WorkerProfile worker, TimeClockPolicy policy, IReadOnlyList<AttendanceException> raised, CancellationToken cancellationToken)
    {
        foreach (var exception in raised.Where(exception => exception.Kind == AttendanceExceptionKind.MissedPunch))
        {
            await _notifier.NotifyAsync(new WorkforceNotification
            {
                WorkerId = worker.ItemId,
                Kind = WorkforceNotificationKind.Attendance,
                Title = S["A punch is missing"],
                Body = S["Your punches on {0} don't add up. Ask for the punch you missed on your timesheet.", exception.Date.ToString("D", CultureInfo.CurrentCulture)],
                Url = "~/my-work/timesheet?date=" + exception.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            }, cancellationToken);
        }

        if (!policy.PointsThreshold.HasValue || !raised.Any(exception => exception.Points > 0))
        {
            return;
        }

        var latest = raised.Where(exception => exception.Points > 0).MaxBy(exception => exception.Date);
        var points = await PointsAsync(worker.ItemId, policy, latest.Date, cancellationToken);
        var before = points - raised.Sum(exception => exception.Points);

        if (points < policy.PointsThreshold || before >= policy.PointsThreshold)
        {
            return;
        }

        foreach (var handler in _pointsHandlers)
        {
            try
            {
                await handler.ThresholdReachedAsync(worker, latest, points, policy, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "An attendance points handler failed for worker '{WorkerId}'.", worker.ItemId);
            }
        }
    }

    internal async Task<decimal> PointsAsync(string workerId, TimeClockPolicy policy, DateOnly date, CancellationToken cancellationToken)
    {
        var exceptions = await _exceptionStore.ListForWorkerAsync(workerId, date.AddDays(1 - policy.PointsWindowDays), date, cancellationToken);

        return exceptions.Where(exception => exception.Status != AttendanceExceptionStatus.Excused).Sum(exception => exception.Points);
    }

    private Task NotifyAsync(WorkerProfile worker, Timesheet timesheet, string title, string body, CancellationToken cancellationToken)
    {
        return _notifier.NotifyAsync(new WorkforceNotification
        {
            WorkerId = worker.ItemId,
            Kind = WorkforceNotificationKind.Timesheet,
            Title = title,
            Body = body,
            Url = "~/my-work/timesheet?date=" + timesheet.PeriodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        }, cancellationToken);
    }

    private static string PeriodText(Timesheet timesheet)
    {
        return timesheet.PeriodStart.ToString("d MMM", CultureInfo.CurrentCulture) + " – " + timesheet.PeriodEnd.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
    }

    private string StatusName(TimesheetStatus status)
    {
        return status switch
        {
            TimesheetStatus.Submitted => S["submitted"],
            TimesheetStatus.Approved => S["approved"],
            TimesheetStatus.Locked => S["locked"],
            TimesheetStatus.Rejected => S["sent back"],
            TimesheetStatus.Reopened => S["reopened"],
            _ => S["open"],
        };
    }
}
