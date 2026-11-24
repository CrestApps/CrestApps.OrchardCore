using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Lets a worker whose time the time clock keeps punch from Time Tracker's own clock tools (such as the AI assistant's
/// <c>clockMyTime</c>), with the same checks as the My Work punch buttons.
/// </summary>
[Feature(WorkforceConstants.Features.TimeClock)]
public sealed class TimeClockSelfPunchStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IExternalTimeClockPunches, TimeClockSelfPunch>());
    }
}

/// <summary>
/// Punches on the time clock for the signed-in worker, as <c>MyTimeController.Punch</c> does: the person needs
/// <see cref="WorkforcePermissions.UseWorkforcePortal"/> and <see cref="WorkforcePermissions.PunchTime"/> and must be a
/// worker; the time clock then applies its policy (punch order, location, geofence).
/// </summary>
internal sealed class TimeClockSelfPunch : IExternalTimeClockPunches
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IStaffMemberManager _workers;
    private readonly ITimeClockService _timeClock;

    internal readonly IStringLocalizer S;

    public TimeClockSelfPunch(IAuthorizationService authorizationService, IStaffMemberManager workers, ITimeClockService timeClock, IStringLocalizer<TimeClockSelfPunch> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _workers = workers;
        _timeClock = timeClock;
        S = stringLocalizer;
    }

    public string ClockId => TimeClockTimeTracker.ClockId;

    public async Task<ExternalTimeClockPunchState> GetStateAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var (worker, refusal) = await AuthorizeAsync(user, cancellationToken);

        if (worker is null)
        {
            return new ExternalTimeClockPunchState { Refusal = refusal };
        }

        var status = await _timeClock.GetStatusAsync(worker, cancellationToken);

        return new ExternalTimeClockPunchState
        {
            State = status.State.ToString(),
            SinceUtc = status.SinceUtc,
            NextPunches = [.. status.NextPunches.Select(kind => kind.ToString())],
            NeedsLocation = status.NeedsLocation,
            TimeZoneId = status.TimeZoneId,
        };
    }

    public async Task<ExternalTimeClockPunchResult> PunchAsync(ClaimsPrincipal user, string punch, CancellationToken cancellationToken = default)
    {
        // Names only: a number never picks a punch by position.
        if (string.IsNullOrWhiteSpace(punch)
            || int.TryParse(punch, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            || !Enum.TryParse<TimePunchKind>(punch.Trim(), ignoreCase: true, out var kind)
            || !Enum.IsDefined(kind))
        {
            return ExternalTimeClockPunchResult.Fail(S["Choose a punch: {0}.", string.Join(", ", Enum.GetNames<TimePunchKind>())]);
        }

        var (worker, refusal) = await AuthorizeAsync(user, cancellationToken);

        if (worker is null)
        {
            return ExternalTimeClockPunchResult.Fail(refusal);
        }

        var result = await _timeClock.PunchAsync(new TimePunchRequest
        {
            WorkerId = worker.ItemId,
            Kind = kind,
            Source = TimePunchSource.Web,
            DeviceId = "AI assistant",
        }, user.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            return new ExternalTimeClockPunchResult { Errors = [.. result.Errors] };
        }

        var status = await _timeClock.GetStatusAsync(worker, cancellationToken);

        return new ExternalTimeClockPunchResult
        {
            Punch = result.Value.Kind.ToString(),
            Utc = result.Value.Utc,
            TimeZoneId = status.TimeZoneId,
            WithinGeofence = result.Value.WithinGeofence,
        };
    }

    private async Task<(StaffMember Worker, string Refusal)> AuthorizeAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return (null, S["Sign in to punch."].Value);
        }

        if (!await _authorizationService.AuthorizeAsync(user, WorkforcePermissions.UseWorkforcePortal) || !await _authorizationService.AuthorizeAsync(user, WorkforcePermissions.PunchTime))
        {
            return (null, S["You aren't allowed to clock in and out here: it needs the 'UseWorkforcePortal' and 'PunchTime' permissions."].Value);
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var worker = string.IsNullOrEmpty(userId) ? null : await _workers.FindByUserIdAsync(userId, cancellationToken);

        if (worker is null)
        {
            return (null, S["You aren't a worker, so you have no time clock to punch."].Value);
        }

        return (worker, null);
    }
}
