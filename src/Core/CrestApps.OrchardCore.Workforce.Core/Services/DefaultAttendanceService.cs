using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="IAttendanceService"/>.
/// </summary>
internal sealed class DefaultAttendanceService : IAttendanceService
{
    private readonly AttendanceExceptionStore _exceptionStore;
    private readonly PunchCorrectionStore _correctionStore;
    private readonly TimesheetStore _timesheetStore;
    private readonly WorkerStore _workerStore;
    private readonly ITimeClockService _timeClock;
    private readonly WorkerTimeZones _timeZones;
    private readonly IWorkforceNotifier _notifier;
    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DefaultAttendanceService(
        AttendanceExceptionStore exceptionStore,
        PunchCorrectionStore correctionStore,
        TimesheetStore timesheetStore,
        WorkerStore workerStore,
        ITimeClockService timeClock,
        WorkerTimeZones timeZones,
        IWorkforceNotifier notifier,
        ISession session,
        IClock clock,
        IStringLocalizer<DefaultAttendanceService> stringLocalizer)
    {
        _exceptionStore = exceptionStore;
        _correctionStore = correctionStore;
        _timesheetStore = timesheetStore;
        _workerStore = workerStore;
        _timeClock = timeClock;
        _timeZones = timeZones;
        _notifier = notifier;
        _session = session;
        _clock = clock;
        S = stringLocalizer;
    }

    public Task<(int Count, IReadOnlyList<AttendanceException> Items)> PageAsync(AttendanceQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _exceptionStore.PageAsync(query, skip, take, cancellationToken);
    }

    public Task<PlanningResult<AttendanceException>> ExcuseAsync(string id, string note, string actor, CancellationToken cancellationToken = default)
    {
        return DecideAsync(id, actor, exception =>
        {
            if (exception.Status == AttendanceExceptionStatus.Excused)
            {
                return S["It is already excused."];
            }

            exception.Status = AttendanceExceptionStatus.Excused;
            exception.Note = string.IsNullOrWhiteSpace(note) ? exception.Note : note.Trim();

            return null;
        }, cancellationToken);
    }

    public Task<PlanningResult<AttendanceException>> ConfirmAsync(string id, string actor, CancellationToken cancellationToken = default)
    {
        return DecideAsync(id, actor, exception =>
        {
            if (exception.Status != AttendanceExceptionStatus.Open)
            {
                return S["Only an open exception can be confirmed."];
            }

            exception.Status = AttendanceExceptionStatus.Confirmed;

            return null;
        }, cancellationToken);
    }

    public async Task<decimal> GetPointsAsync(WorkerProfile worker, DateOnly date, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var policy = await _timeClock.GetPolicyAsync(worker, date, cancellationToken);

        if (policy is null || policy.AttendancePoints.Count == 0)
        {
            return 0;
        }

        var exceptions = await _exceptionStore.ListForWorkerAsync(worker.ItemId, date.AddDays(1 - policy.PointsWindowDays), date, cancellationToken);

        return exceptions.Where(exception => exception.Status != AttendanceExceptionStatus.Excused).Sum(exception => exception.Points);
    }

    public async Task<PlanningResult<PunchCorrection>> RequestCorrectionAsync(WorkerProfile worker, TimePunchKind kind, DateTime utc, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var result = new PlanningResult<PunchCorrection>();

        utc = utc.ToUniversalTime();

        if (utc > _clock.UtcNow)
        {
            result.Fail(S["A punch can't be in the future."]);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            result.Fail(S["Say why the punch is missing."]);
        }

        var teamId = WorkerTimeZones.TeamOn(worker, DateOnly.FromDateTime(utc));
        var zone = await _timeZones.GetAsync(worker, teamId, cancellationToken);
        var date = DateOnly.FromDateTime(CalendarTime.ToLocal(utc, zone));
        var timesheet = await _timesheetStore.FindForDateAsync(worker.ItemId, date, cancellationToken);

        if (timesheet is not null && !timesheet.IsEditable)
        {
            result.Fail(S["The timesheet for {0} is no longer open. Ask your manager.", date.ToString("D", CultureInfo.CurrentCulture)]);
        }

        if (!result.Succeeded)
        {
            return result;
        }

        var correction = new PunchCorrection
        {
            ItemId = UniqueId.GenerateId(),
            WorkerId = worker.ItemId,
            TeamId = teamId,
            Kind = kind,
            Utc = utc,
            Reason = reason.Trim(),
            CreatedUtc = _clock.UtcNow,
        };

        await _correctionStore.CreateAsync(correction, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        result.Value = correction;

        return result;
    }

    public async Task<PlanningResult<PunchCorrection>> DecideCorrectionAsync(string id, bool approve, string actor, CancellationToken cancellationToken = default)
    {
        var result = new PlanningResult<PunchCorrection>();
        var correction = string.IsNullOrEmpty(id) ? null : await _correctionStore.FindByIdAsync(id, cancellationToken);

        if (correction is null)
        {
            result.Fail(S["The correction wasn't found."]);

            return result;
        }

        if (correction.Status != PunchCorrectionStatus.Pending)
        {
            result.Fail(S["The correction was already decided."]);

            return result;
        }

        if (approve)
        {
            var punch = await _timeClock.PunchAsync(new TimePunchRequest
            {
                WorkerId = correction.WorkerId,
                Kind = correction.Kind,
                Utc = correction.Utc,
                Source = TimePunchSource.Manager,
                Reason = correction.Reason,
                IdempotencyKey = "correction:" + correction.ItemId,
            }, actor, cancellationToken);

            if (!punch.Succeeded)
            {
                foreach (var error in punch.Errors)
                {
                    result.Fail(error);
                }

                return result;
            }

            correction.PunchId = punch.Value.ItemId;
        }

        correction.Status = approve ? PunchCorrectionStatus.Approved : PunchCorrectionStatus.Declined;
        correction.DecidedBy = actor;
        correction.DecidedUtc = _clock.UtcNow;
        await _correctionStore.UpdateAsync(correction, cancellationToken);

        var worker = await _workerStore.FindByIdAsync(correction.WorkerId, cancellationToken);
        var zone = worker is null ? "UTC" : await _timeZones.GetAsync(worker, correction.TeamId, cancellationToken);
        var when = CalendarTime.ToLocal(correction.Utc, zone);

        await _notifier.NotifyAsync(new WorkforceNotification
        {
            WorkerId = correction.WorkerId,
            Kind = WorkforceNotificationKind.Attendance,
            Title = approve ? S["Your punch was added"] : S["Your punch wasn't added"],
            Body = S["The punch you asked for at {0} was {1}.", when.ToString("g", CultureInfo.CurrentCulture), approve ? S["added"] : S["declined"]],
            Url = "~/my-work/timesheet?date=" + DateOnly.FromDateTime(when).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        }, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        result.Value = correction;

        return result;
    }

    public Task<IReadOnlyList<PunchCorrection>> ListPendingCorrectionsAsync(IReadOnlyCollection<string> teamIds, string workerId, CancellationToken cancellationToken = default)
    {
        return _correctionStore.ListAsync(teamIds, workerId, PunchCorrectionStatus.Pending, cancellationToken);
    }

    private async Task<PlanningResult<AttendanceException>> DecideAsync(string id, string actor, Func<AttendanceException, string> decide, CancellationToken cancellationToken)
    {
        var result = new PlanningResult<AttendanceException>();
        var exception = string.IsNullOrEmpty(id) ? null : await _exceptionStore.FindByIdAsync(id, cancellationToken);

        if (exception is null)
        {
            result.Fail(S["The exception wasn't found."]);

            return result;
        }

        var error = decide(exception);

        if (error is not null)
        {
            result.Fail(error);

            return result;
        }

        exception.DecidedBy = actor;
        exception.DecidedUtc = _clock.UtcNow;
        await _exceptionStore.UpdateAsync(exception, cancellationToken);

        // The timesheet's count of open exceptions follows.
        var timesheet = await _timesheetStore.FindForDateAsync(exception.WorkerId, exception.Date, cancellationToken);

        if (timesheet is not null && timesheet.OpenExceptions > 0)
        {
            timesheet.OpenExceptions--;
            await _timesheetStore.UpdateAsync(timesheet, cancellationToken);
        }

        await _session.SaveChangesAsync(cancellationToken);

        result.Value = exception;

        return result;
    }
}
