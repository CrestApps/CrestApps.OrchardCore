using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The kiosk page a shared device keeps open: workers enter their badge number and PIN, then clock in or out. The
/// address's secret is the kiosk's key, so the page needs no sign-in.
/// </summary>
[AllowAnonymous]
[Feature(WorkforceConstants.Features.TimeClock)]
public sealed class TimeClockKioskController : Controller
{
    private static readonly TimeSpan _ticketLifetime = TimeSpan.FromMinutes(2);

    private readonly ITimeClockService _timeClock;
    private readonly ITimeClockKioskManager _kioskManager;
    private readonly ITeamManager _teamManager;
    private readonly IDataProtectionProvider _dataProtection;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeClockKioskController"/> class.
    /// </summary>
    /// <param name="timeClock">The time clock.</param>
    /// <param name="kioskManager">The kiosk manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="dataProtection">The data protection provider.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeClockKioskController(
        ITimeClockService timeClock,
        ITimeClockKioskManager kioskManager,
        ITeamManager teamManager,
        IDataProtectionProvider dataProtection,
        IClock clock,
        IStringLocalizer<TimeClockKioskController> stringLocalizer)
    {
        _timeClock = timeClock;
        _kioskManager = kioskManager;
        _teamManager = teamManager;
        _dataProtection = dataProtection;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Shows the badge and PIN pad.
    /// </summary>
    /// <param name="token">The kiosk's secret.</param>
    /// <returns>The page.</returns>
    [HttpGet("workforce/kiosk/{token}")]
    public async Task<IActionResult> Index(string token)
    {
        var kiosk = await _timeClock.FindKioskAsync(token, HttpContext.RequestAborted);

        if (kiosk is null)
        {
            return NotFound();
        }

        return View(new KioskViewModel { Kiosk = kiosk, Token = token });
    }

    /// <summary>
    /// Checks the badge number and PIN and shows what the worker can punch.
    /// </summary>
    /// <param name="token">The kiosk's secret.</param>
    /// <param name="badge">The badge number.</param>
    /// <param name="pin">The PIN.</param>
    /// <returns>The worker's choices, or the pad with an error.</returns>
    [HttpPost("workforce/kiosk/{token}")]
    public async Task<IActionResult> Identify(string token, string badge, string pin)
    {
        var kiosk = await _timeClock.FindKioskAsync(token, HttpContext.RequestAborted);

        if (kiosk is null)
        {
            return NotFound();
        }

        var model = new KioskViewModel { Kiosk = kiosk, Token = token };
        var identified = await _timeClock.IdentifyAsync(badge, pin, HttpContext.RequestAborted);

        if (!identified.Succeeded)
        {
            model.Error = identified.Errors[0];

            return View(nameof(Index), model);
        }

        if (!await AllowedAsync(kiosk, identified.Value))
        {
            model.Error = S["This kiosk is for another team."];

            return View(nameof(Index), model);
        }

        model.Worker = identified.Value;
        model.Status = await _timeClock.GetStatusAsync(identified.Value, HttpContext.RequestAborted);
        model.Ticket = Protector().Protect(kiosk.ItemId + "|" + identified.Value.ItemId, _ticketLifetime);

        if (model.Status.Policy is null)
        {
            model.Error = S["{0}'s team doesn't clock time.", identified.Value.DisplayName];
            model.Worker = null;

            return View(nameof(Index), model);
        }

        return View(nameof(Index), model);
    }

    /// <summary>
    /// Records the worker's punch.
    /// </summary>
    /// <param name="token">The kiosk's secret.</param>
    /// <param name="ticket">The ticket the PIN check gave.</param>
    /// <param name="kind">The punch.</param>
    /// <param name="key">A key that makes a punch sent twice count once.</param>
    /// <returns>The result, which goes back to the pad by itself.</returns>
    [HttpPost("workforce/kiosk/{token}/punch")]
    public async Task<IActionResult> Punch(string token, string ticket, TimePunchKind kind, string key)
    {
        var kiosk = await _timeClock.FindKioskAsync(token, HttpContext.RequestAborted);

        if (kiosk is null)
        {
            return NotFound();
        }

        var model = new KioskViewModel { Kiosk = kiosk, Token = token };
        string[] parts;

        try
        {
            parts = Protector().Unprotect(ticket ?? string.Empty).Split('|');
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            parts = [];
        }

        if (parts.Length != 2 || parts[0] != kiosk.ItemId)
        {
            model.Error = S["That took too long. Enter your badge number and PIN again."];

            return View(nameof(Index), model);
        }

        var result = await _timeClock.PunchAsync(new TimePunchRequest
        {
            WorkerId = parts[1],
            Kind = kind,
            Source = TimePunchSource.Kiosk,
            DeviceId = kiosk.ItemId,
            IdempotencyKey = string.IsNullOrWhiteSpace(key) ? null : "kiosk:" + key,
        }, kiosk.Name, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            model.Error = result.Errors[0];

            return View(nameof(Index), model);
        }

        var stored = await _kioskManager.FindByIdAsync(kiosk.ItemId);

        if (stored is not null)
        {
            stored.LastUsedUtc = _clock.UtcNow;
            await _kioskManager.UpdateAsync(stored);
        }

        model.Done = result.Value;

        return View(nameof(Index), model);
    }

    private async Task<bool> AllowedAsync(TimeClockKiosk kiosk, WorkerProfile worker)
    {
        if (string.IsNullOrEmpty(kiosk.TeamId))
        {
            return true;
        }

        var teams = await _teamManager.GetSubtreeIdsAsync(kiosk.TeamId, HttpContext.RequestAborted);
        var today = DateOnly.FromDateTime(_clock.UtcNow);

        return worker.MembershipsOn(today).Any(membership => teams.Contains(membership.TeamId));
    }

    private ITimeLimitedDataProtector Protector()
    {
        return _dataProtection.CreateProtector("CrestApps.Workforce.Kiosk").ToTimeLimitedDataProtector();
    }
}
