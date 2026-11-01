using CrestApps.OrchardCore.Workforce.ContactCenter.Services.Intraday;
using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Workforce.Portal;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Controllers;

/// <summary>
/// The My Work page where workers take voluntary overtime and time off.
/// </summary>
[Feature(WorkforceContactCenterConstants.Features.Intraday)]
[RequireFeatures(WorkforceConstants.Features.Portal)]
public sealed class MyOffersController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IntradayService _intradayService;
    private readonly MyWorkPortal _portal;
    private readonly IWorkerTimeZoneService _timeZones;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyOffersController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="intradayService">The intraday service.</param>
    /// <param name="portal">The My Work portal.</param>
    /// <param name="timeZones">The time zones.</param>
    /// <param name="session">The session.</param>
    public MyOffersController(
        IAuthorizationService authorizationService,
        IntradayService intradayService,
        MyWorkPortal portal,
        IWorkerTimeZoneService timeZones,
        ISession session)
    {
        _authorizationService = authorizationService;
        _intradayService = intradayService;
        _portal = portal;
        _timeZones = timeZones;
        _session = session;
    }

    /// <summary>
    /// Lists the offers the worker can take.
    /// </summary>
    /// <param name="message">A message to show.</param>
    /// <param name="error">Whether the message is an error.</param>
    /// <returns>The page.</returns>
    [HttpGet("my-work/offers")]
    public async Task<IActionResult> Index(string message, bool error)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.UseWorkforcePortal))
        {
            return Forbid();
        }

        var worker = await _portal.GetWorkerAsync(User);

        if (worker is null)
        {
            return Redirect("~/my-work");
        }

        return View(new MyOffersViewModel
        {
            Nav = await _portal.NavAsync(User, worker, "offers", HttpContext.RequestAborted),
            Offers = await _intradayService.OpenForAsync(worker, HttpContext.RequestAborted),
            TimeZoneId = await _timeZones.GetAsync(worker, null, HttpContext.RequestAborted),
            Message = message,
            IsError = error,
        });
    }

    /// <summary>
    /// Takes an offer.
    /// </summary>
    /// <param name="id">The offer.</param>
    /// <returns>The page, with what happened.</returns>
    [HttpPost("my-work/offers/{id}/take")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Take(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.UseWorkforcePortal))
        {
            return Forbid();
        }

        var worker = await _portal.GetWorkerAsync(User);

        if (worker is null)
        {
            return Redirect("~/my-work");
        }

        var errors = await _intradayService.TakeAsync(id, worker.ItemId, User.Identity?.Name, HttpContext.RequestAborted);

        if (errors.Count > 0)
        {
            return RedirectToAction(nameof(Index), new { message = string.Join(" ", errors), error = true });
        }

        await _session.SaveChangesAsync(HttpContext.RequestAborted);

        return RedirectToAction(nameof(Index), new { message = "Done: your shift is changed. You'll see it on your schedule." });
    }
}
