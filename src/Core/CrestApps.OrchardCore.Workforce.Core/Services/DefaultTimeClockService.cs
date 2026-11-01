using System.Security.Cryptography;
using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="ITimeClockService"/>. A worker's punches are recorded under their lock and saved before it
/// is let go, so two devices punching at once can't both clock them in.
/// </summary>
internal sealed class DefaultTimeClockService : ITimeClockService
{
    private const int MaxFailedPins = 5;
    private const int PinIterations = 100_000;

    // An open punch older than this no longer keeps the worker on the clock; it is a missed punch.
    private static readonly TimeSpan _staleAfter = TimeSpan.FromHours(16);
    private static readonly TimeSpan _pinLockout = TimeSpan.FromMinutes(15);

    private readonly TimePunchStore _punchStore;
    private readonly TimesheetStore _timesheetStore;
    private readonly TimeClockPolicyStore _policyStore;
    private readonly TimeClockKioskStore _kioskStore;
    private readonly WorkerStore _workerStore;
    private readonly TeamStore _teamStore;
    private readonly ITeamManager _teamManager;
    private readonly WorkerShiftReader _shiftReader;
    private readonly WorkerTimeZones _timeZones;
    private readonly TimeOffLocks _locks;
    private readonly IWorkforceEvents _events;
    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DefaultTimeClockService(
        TimePunchStore punchStore,
        TimesheetStore timesheetStore,
        TimeClockPolicyStore policyStore,
        TimeClockKioskStore kioskStore,
        WorkerStore workerStore,
        TeamStore teamStore,
        ITeamManager teamManager,
        WorkerShiftReader shiftReader,
        WorkerTimeZones timeZones,
        TimeOffLocks locks,
        IWorkforceEvents events,
        ISession session,
        IClock clock,
        IStringLocalizer<DefaultTimeClockService> stringLocalizer)
    {
        _punchStore = punchStore;
        _timesheetStore = timesheetStore;
        _policyStore = policyStore;
        _kioskStore = kioskStore;
        _workerStore = workerStore;
        _teamStore = teamStore;
        _teamManager = teamManager;
        _shiftReader = shiftReader;
        _timeZones = timeZones;
        _locks = locks;
        _events = events;
        _session = session;
        _clock = clock;
        S = stringLocalizer;
    }

    public async Task<TimeClockPolicy> GetPolicyAsync(WorkerProfile worker, DateOnly date, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        foreach (var team in await LineageAsync(WorkerTimeZones.TeamOn(worker, date), cancellationToken))
        {
            if (string.IsNullOrEmpty(team.TimeClockPolicyId))
            {
                continue;
            }

            var policy = await _policyStore.FindByIdAsync(team.TimeClockPolicyId, cancellationToken);

            return policy is { Enabled: true } ? policy : null;
        }

        return null;
    }

    public async Task<TimeClockStatus> GetStatusAsync(WorkerProfile worker, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var now = _clock.UtcNow;
        var zone = await _timeZones.GetAsync(worker, WorkerTimeZones.TeamOn(worker, DateOnly.FromDateTime(now)), cancellationToken);
        var policy = await GetPolicyAsync(worker, DateOnly.FromDateTime(CalendarTime.ToLocal(now, zone)), cancellationToken);
        var punches = await _punchStore.ListEffectiveAsync(worker.ItemId, now - _staleAfter, now.AddMinutes(5), cancellationToken);
        var (state, since) = StateOf(punches);

        return new TimeClockStatus
        {
            State = state,
            SinceUtc = since,
            LastPunch = punches.Count > 0 ? punches[^1] : null,
            NextPunches = NextPunches(state),
            Policy = policy,
            NeedsLocation = policy?.RequireGeofence == true,
            TimeZoneId = zone,
        };
    }

    public async Task<PlanningResult<TimePunch>> PunchAsync(TimePunchRequest request, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = new PlanningResult<TimePunch>();
        var worker = string.IsNullOrEmpty(request.WorkerId) ? null : await _workerStore.FindByIdAsync(request.WorkerId, cancellationToken);

        if (worker is null)
        {
            result.Fail(S["The worker wasn't found."]);

            return result;
        }

        if (worker.Status is WorkerStatus.Terminated or WorkerStatus.Suspended)
        {
            result.Fail(S["{0} can't clock in or out while they are {1}.", worker.DisplayName, worker.Status == WorkerStatus.Terminated ? S["no longer working here"] : S["suspended"]]);

            return result;
        }

        var now = _clock.UtcNow;
        var utc = request.Utc?.ToUniversalTime() ?? now;

        if (utc > now.AddMinutes(5))
        {
            result.Fail(S["A punch can't be in the future."]);

            return result;
        }

        var teamId = WorkerTimeZones.TeamOn(worker, DateOnly.FromDateTime(utc));
        var zone = await _timeZones.GetAsync(worker, teamId, cancellationToken);
        var date = DateOnly.FromDateTime(CalendarTime.ToLocal(utc, zone));
        var policy = await GetPolicyAsync(worker, date, cancellationToken);

        if (policy is null)
        {
            result.Fail(S["{0}'s team has no time clock policy, so their time isn't clocked.", worker.DisplayName]);

            return result;
        }

        if (!policy.Allows(request.Source))
        {
            result.Fail(S["Punches from {0} aren't allowed for {1}'s team.", SourceName(request.Source), worker.DisplayName]);

            return result;
        }

        var lockers = await _locks.LockAsync([TimeOffLocks.TimeClock(worker.ItemId)]);

        if (lockers is null)
        {
            result.Fail(S["Another punch for {0} is being recorded. Try again.", worker.DisplayName]);

            return result;
        }

        try
        {
            if (!string.IsNullOrEmpty(request.IdempotencyKey))
            {
                var existing = await _punchStore.FindByKeyAsync(worker.ItemId, request.IdempotencyKey, cancellationToken);

                if (existing is not null)
                {
                    result.Value = existing;

                    return result;
                }
            }

            var timesheet = await _timesheetStore.FindForDateAsync(worker.ItemId, date, cancellationToken);

            if (timesheet is not null && !timesheet.IsEditable)
            {
                result.Fail(S["The timesheet for {0} is {1}. A manager must reopen it first.", date.ToString("D", System.Globalization.CultureInfo.CurrentCulture), StatusName(timesheet.Status)]);

                return result;
            }

            if (request.Source != TimePunchSource.Manager)
            {
                var earlier = await _punchStore.ListEffectiveAsync(worker.ItemId, utc - _staleAfter, utc.AddTicks(1), cancellationToken);
                var (state, _) = StateOf(earlier);

                if (!NextPunches(state).Contains(request.Kind))
                {
                    result.Fail(state == TimeClockState.Out
                        ? S["{0} isn't clocked in.", worker.DisplayName]
                        : S["{0} can't do that now: they are {1}.", worker.DisplayName, StateName(state)]);

                    return result;
                }

                if (request.Kind == TimePunchKind.In && policy.BlockEarlyPunchInMinutes.HasValue)
                {
                    var opensAt = await EarliestPunchInAsync(worker, utc, policy.BlockEarlyPunchInMinutes.Value, cancellationToken);

                    if (opensAt.HasValue)
                    {
                        result.Fail(S["It's too early to clock in. You can clock in from {0}.", CalendarTime.ToLocal(opensAt.Value, zone).ToString("t", System.Globalization.CultureInfo.CurrentCulture)]);

                        return result;
                    }
                }
            }

            bool? withinGeofence = null;

            if (policy.RequireGeofence && request.Source is TimePunchSource.Web or TimePunchSource.Mobile)
            {
                if (!request.Latitude.HasValue || !request.Longitude.HasValue)
                {
                    result.Fail(S["Share your location to clock in or out: your team clocks in at the work site."]);

                    return result;
                }

                var site = await SiteAsync(teamId, cancellationToken);

                if (site is not null)
                {
                    withinGeofence = DistanceMeters(site.Latitude.Value, site.Longitude.Value, request.Latitude.Value, request.Longitude.Value) <= policy.GeofenceRadiusMeters;
                }
            }

            var punch = new TimePunch
            {
                ItemId = UniqueId.GenerateId(),
                WorkerId = worker.ItemId,
                TeamId = teamId,
                Kind = request.Kind,
                Utc = utc,
                Source = request.Source,
                DeviceId = Trim(request.DeviceId, 100),
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                WithinGeofence = withinGeofence,
                ActivityCodeId = request.ActivityCodeId,
                Job = Trim(request.Job, 200),
                IdempotencyKey = Trim(request.IdempotencyKey, 150),
                Reason = Trim(request.Reason, 500),
                Actor = actor,
                CreatedUtc = now,
            };

            await _punchStore.CreateAsync(punch, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            result.Value = punch;
        }
        finally
        {
            await TimeOffLocks.ReleaseAsync(lockers);
        }

        if (result.Value.Kind is TimePunchKind.In or TimePunchKind.Out)
        {
            await _events.RaiseAsync(result.Value.Kind == TimePunchKind.In ? WorkforceEvents.PunchedIn : WorkforceEvents.PunchedOut, WorkforceEventInputs.Punch(result.Value), result.Value.ItemId, cancellationToken);
        }

        return result;
    }

    public Task<PlanningResult<TimePunch>> CorrectAsync(string punchId, TimePunchKind kind, DateTime utc, string reason, string actor, CancellationToken cancellationToken = default)
    {
        return ReplaceAsync(punchId, reason, actor, original => new TimePunch
        {
            Kind = kind,
            Utc = utc.ToUniversalTime(),
            ActivityCodeId = original.ActivityCodeId,
            Job = original.Job,
        }, cancellationToken);
    }

    public Task<PlanningResult<TimePunch>> RemoveAsync(string punchId, string reason, string actor, CancellationToken cancellationToken = default)
    {
        return ReplaceAsync(punchId, reason, actor, original => new TimePunch
        {
            Kind = original.Kind,
            Utc = original.Utc,
            Removes = true,
        }, cancellationToken);
    }

    public async Task<TimePunch> FindAsync(string punchId, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(punchId) ? null : await _punchStore.FindByIdAsync(punchId, cancellationToken);
    }

    public Task<IReadOnlyList<TimePunch>> ListAsync(string workerId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        return _punchStore.ListEffectiveAsync(workerId, fromUtc, toUtc, cancellationToken);
    }

    public async Task<PlanningResult<WorkerProfile>> SetPinAsync(WorkerProfile worker, string pin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var result = new PlanningResult<WorkerProfile>();

        // The PIN is saved on the stored worker, so a copy from another request can't save over newer changes.
        worker = await _workerStore.FindByIdAsync(worker.ItemId, cancellationToken) ?? worker;
        result.Value = worker;

        if (string.IsNullOrWhiteSpace(pin))
        {
            worker.Put(new WorkerTimeClockSettings());
            await _workerStore.UpdateAsync(worker, cancellationToken);

            return result;
        }

        pin = pin.Trim();

        if (pin.Length is < 4 or > 8 || !pin.All(char.IsAsciiDigit))
        {
            result.Fail(S["A PIN is 4 to 8 digits."]);

            return result;
        }

        if (string.IsNullOrEmpty(worker.EmployeeNumber))
        {
            result.Fail(S["Give {0} an employee number first: it is their badge number on the kiosk.", worker.DisplayName]);

            return result;
        }

        var salt = RandomNumberGenerator.GetBytes(16);

        worker.Put(new WorkerTimeClockSettings
        {
            PinSalt = Convert.ToBase64String(salt),
            PinHash = HashPin(pin, salt),
        });
        await _workerStore.UpdateAsync(worker, cancellationToken);

        return result;
    }

    public async Task<PlanningResult<WorkerProfile>> IdentifyAsync(string badgeNumber, string pin, CancellationToken cancellationToken = default)
    {
        var result = new PlanningResult<WorkerProfile>();
        var worker = string.IsNullOrWhiteSpace(badgeNumber) ? null : await _workerStore.FindByEmployeeNumberAsync(badgeNumber.Trim(), cancellationToken);

        if (worker is null || !worker.TryGet<WorkerTimeClockSettings>(out var settings) || string.IsNullOrEmpty(settings.PinHash))
        {
            result.Fail(S["That badge number and PIN weren't recognized."]);

            return result;
        }

        var now = _clock.UtcNow;

        if (settings.LockedUntilUtc > now)
        {
            result.Fail(S["Too many wrong PINs. Try again in a few minutes, or ask a manager."]);

            return result;
        }

        var matches = !string.IsNullOrEmpty(pin)
            && CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(HashPin(pin.Trim(), Convert.FromBase64String(settings.PinSalt))), Convert.FromBase64String(settings.PinHash));

        if (!matches)
        {
            settings.FailedAttempts++;

            if (settings.FailedAttempts >= MaxFailedPins)
            {
                settings.FailedAttempts = 0;
                settings.LockedUntilUtc = now + _pinLockout;
            }

            worker.Put(settings);
            await _workerStore.UpdateAsync(worker, cancellationToken);
            result.Fail(S["That badge number and PIN weren't recognized."]);

            return result;
        }

        if (settings.FailedAttempts > 0 || settings.LockedUntilUtc.HasValue)
        {
            settings.FailedAttempts = 0;
            settings.LockedUntilUtc = null;
            worker.Put(settings);
            await _workerStore.UpdateAsync(worker, cancellationToken);
        }

        result.Value = worker;

        return result;
    }

    public async Task<TimeClockKiosk> FindKioskAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 20)
        {
            return null;
        }

        var kiosk = await _kioskStore.FindByTokenHashAsync(PortalSecrets.Hash(token), cancellationToken);

        return kiosk is { Enabled: true } ? kiosk : null;
    }

    public async Task<string> RenewKioskTokenAsync(TimeClockKiosk kiosk, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kiosk);

        var token = PortalSecrets.NewToken();
        var stored = await _kioskStore.FindByIdAsync(kiosk.ItemId, cancellationToken) ?? kiosk;

        kiosk.TokenHash = stored.TokenHash = PortalSecrets.Hash(token);
        await _kioskStore.UpdateAsync(stored, cancellationToken);

        return token;
    }

    /// <summary>
    /// Returns where a worker stands after their punches, and since when: an open punch that is too old leaves
    /// them off the clock.
    /// </summary>
    internal (TimeClockState State, DateTime? Since) StateOf(IReadOnlyList<TimePunch> punches)
    {
        var state = TimeClockState.Out;
        DateTime? since = null;

        foreach (var punch in punches)
        {
            switch (punch.Kind)
            {
                case TimePunchKind.In:
                case TimePunchKind.JobStart when state == TimeClockState.Out:
                    if (state == TimeClockState.Out)
                    {
                        since = punch.Utc;
                    }

                    state = TimeClockState.Working;
                    break;

                case TimePunchKind.Out:
                    state = TimeClockState.Out;
                    since = punch.Utc;
                    break;

                case TimePunchKind.MealStart:
                    state = TimeClockState.OnMeal;
                    break;

                case TimePunchKind.BreakStart:
                    state = TimeClockState.OnBreak;
                    break;

                case TimePunchKind.MealEnd:
                case TimePunchKind.BreakEnd:
                    state = TimeClockState.Working;
                    break;
            }
        }

        if (state != TimeClockState.Out && punches.Count > 0 && _clock.UtcNow - punches[^1].Utc > _staleAfter)
        {
            return (TimeClockState.Out, null);
        }

        return (state, state == TimeClockState.Out && punches.Count == 0 ? null : since);
    }

    private static TimePunchKind[] NextPunches(TimeClockState state)
    {
        return state switch
        {
            TimeClockState.Working => [TimePunchKind.MealStart, TimePunchKind.BreakStart, TimePunchKind.Out, TimePunchKind.ActivityChange, TimePunchKind.JobStart, TimePunchKind.JobEnd],
            TimeClockState.OnMeal => [TimePunchKind.MealEnd, TimePunchKind.Out],
            TimeClockState.OnBreak => [TimePunchKind.BreakEnd, TimePunchKind.Out],
            _ => [TimePunchKind.In, TimePunchKind.JobStart],
        };
    }

    private async Task<PlanningResult<TimePunch>> ReplaceAsync(string punchId, string reason, string actor, Func<TimePunch, TimePunch> replacement, CancellationToken cancellationToken)
    {
        var result = new PlanningResult<TimePunch>();
        var original = await FindAsync(punchId, cancellationToken);

        if (original is null || original.Removes)
        {
            result.Fail(S["The punch wasn't found."]);

            return result;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            result.Fail(S["Say why the punch changes."]);

            return result;
        }

        var lockers = await _locks.LockAsync([TimeOffLocks.TimeClock(original.WorkerId)]);

        if (lockers is null)
        {
            result.Fail(S["Another change to these punches is being saved. Try again."]);

            return result;
        }

        try
        {
            if (await _punchStore.IsReplacedAsync(original.ItemId, cancellationToken))
            {
                result.Fail(S["The punch was already changed. Reload to see its latest version."]);

                return result;
            }

            var punch = replacement(original);

            if (punch.Utc > _clock.UtcNow.AddMinutes(5))
            {
                result.Fail(S["A punch can't be in the future."]);

                return result;
            }

            var worker = await _workerStore.FindByIdAsync(original.WorkerId, cancellationToken);

            foreach (var day in new[] { original.Utc, punch.Utc }.Distinct())
            {
                var zone = worker is null ? null : await _timeZones.GetAsync(worker, original.TeamId, cancellationToken);
                var date = DateOnly.FromDateTime(CalendarTime.ToLocal(day, zone ?? "UTC"));
                var timesheet = await _timesheetStore.FindForDateAsync(original.WorkerId, date, cancellationToken);

                if (timesheet is not null && !timesheet.IsEditable)
                {
                    result.Fail(S["The timesheet for {0} is {1}. Reopen it first.", date.ToString("D", System.Globalization.CultureInfo.CurrentCulture), StatusName(timesheet.Status)]);

                    return result;
                }
            }

            punch.ItemId = UniqueId.GenerateId();
            punch.WorkerId = original.WorkerId;
            punch.TeamId = original.TeamId;
            punch.Source = TimePunchSource.Manager;
            punch.ReplacesPunchId = original.ItemId;
            punch.Reason = Trim(reason, 500);
            punch.Actor = actor;
            punch.CreatedUtc = _clock.UtcNow;

            await _punchStore.CreateAsync(punch, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            result.Value = punch;

            return result;
        }
        finally
        {
            await TimeOffLocks.ReleaseAsync(lockers);
        }
    }

    /// <summary>
    /// Returns when clocking in opens, when the worker's next shift is further away than the policy allows and no
    /// shift is under way.
    /// </summary>
    private async Task<DateTime?> EarliestPunchInAsync(WorkerProfile worker, DateTime utc, int blockMinutes, CancellationToken cancellationToken)
    {
        var shifts = await _shiftReader.ListAsync(worker, utc.AddHours(-16), utc.AddHours(16), cancellationToken);
        var window = TimeSpan.FromMinutes(blockMinutes);

        if (shifts.Any(shift => shift.StartUtc - window <= utc && shift.EndUtc > utc))
        {
            return null;
        }

        var next = shifts.Where(shift => shift.StartUtc > utc).OrderBy(shift => shift.StartUtc).FirstOrDefault();

        return next is null ? null : next.StartUtc - window;
    }

    private async Task<WorkforceLocation> SiteAsync(string teamId, CancellationToken cancellationToken)
    {
        foreach (var team in await LineageAsync(teamId, cancellationToken))
        {
            if (team.Location is { Latitude: not null, Longitude: not null })
            {
                return team.Location;
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<WorkforceTeam>> LineageAsync(string teamId, CancellationToken cancellationToken)
    {
        var team = string.IsNullOrEmpty(teamId) ? null : await _teamStore.FindByIdAsync(teamId, cancellationToken);

        return team is null ? [] : [team, .. await _teamManager.GetAncestorsAsync(team.ItemId, cancellationToken)];
    }

    private static string HashPin(string pin, byte[] salt)
    {
        return Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(pin, salt, PinIterations, HashAlgorithmName.SHA256, 32));
    }

    private static double DistanceMeters(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        const double radius = 6_371_000;
        var dLat = (latitude2 - latitude1) * Math.PI / 180;
        var dLon = (longitude2 - longitude1) * Math.PI / 180;
        var a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
            + (Math.Cos(latitude1 * Math.PI / 180) * Math.Cos(latitude2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));

        return radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static string Trim(string value, int length)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();

        return value.Length > length ? value[..length] : value;
    }

    private string SourceName(TimePunchSource source)
    {
        return source switch
        {
            TimePunchSource.Web => S["the browser"],
            TimePunchSource.Mobile => S["the My Work app"],
            TimePunchSource.Kiosk => S["a kiosk"],
            TimePunchSource.ContactCenter => S["the contact center"],
            TimePunchSource.Api => S["the API"],
            _ => S["a manager"],
        };
    }

    private string StateName(TimeClockState state)
    {
        return state switch
        {
            TimeClockState.Working => S["clocked in"],
            TimeClockState.OnMeal => S["on a meal"],
            TimeClockState.OnBreak => S["on a break"],
            _ => S["clocked out"],
        };
    }

    private string StatusName(TimesheetStatus status)
    {
        return status switch
        {
            TimesheetStatus.Submitted => S["submitted"],
            TimesheetStatus.Approved => S["approved"],
            TimesheetStatus.Locked => S["locked"],
            _ => S["open"],
        };
    }
}
