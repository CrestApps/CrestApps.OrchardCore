using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Portal;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The My Work portal for workers: home, schedule, time off, shifts to claim and trade, availability and settings,
/// with the portal's manifest, service worker and the worker's calendar feed.
/// </summary>
[Feature(WorkforceConstants.Features.Portal)]
public sealed class MyWorkController : Controller
{
    private const int ScheduleDays = 28;

    private readonly IAuthorizationService _authorizationService;
    private readonly MyWorkPortal _portal;
    private readonly IWorkerManager _workerManager;
    private readonly ITeamManager _teamManager;
    private readonly IActivityCodeManager _activityCodeManager;
    private readonly IShiftService _shiftService;
    private readonly IOpenShiftService _openShiftService;
    private readonly IShiftTradeService _tradeService;
    private readonly IWorkforceInbox _inbox;
    private readonly IWebPushService _pushService;
    private readonly IWorkerFeedService _feedService;
    private readonly IEnumerable<IWorkforceNotificationChannel> _channels;
    private readonly ISiteService _siteService;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MyWorkController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="portal">What the portal's pages share.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="activityCodeManager">The activity code manager.</param>
    /// <param name="shiftService">The published shift service.</param>
    /// <param name="openShiftService">The open shift service.</param>
    /// <param name="tradeService">The shift trade service.</param>
    /// <param name="inbox">The portal inbox.</param>
    /// <param name="pushService">The web push service.</param>
    /// <param name="feedService">The worker feed service.</param>
    /// <param name="channels">The notification channels.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MyWorkController(
        IAuthorizationService authorizationService,
        MyWorkPortal portal,
        IWorkerManager workerManager,
        ITeamManager teamManager,
        IActivityCodeManager activityCodeManager,
        IShiftService shiftService,
        IOpenShiftService openShiftService,
        IShiftTradeService tradeService,
        IWorkforceInbox inbox,
        IWebPushService pushService,
        IWorkerFeedService feedService,
        IEnumerable<IWorkforceNotificationChannel> channels,
        ISiteService siteService,
        ILocalClock localClock,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<MyWorkController> htmlLocalizer,
        IStringLocalizer<MyWorkController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _portal = portal;
        _workerManager = workerManager;
        _teamManager = teamManager;
        _activityCodeManager = activityCodeManager;
        _shiftService = shiftService;
        _openShiftService = openShiftService;
        _tradeService = tradeService;
        _inbox = inbox;
        _pushService = pushService;
        _feedService = feedService;
        _channels = channels;
        _siteService = siteService;
        _localClock = localClock;
        _clock = clock;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Shows the worker's next shift, today and their notifications.
    /// </summary>
    /// <returns>The home page.</returns>
    [HttpGet("my-work", Name = "MyWork")]
    public async Task<IActionResult> Index()
    {
        var (worker, denied) = await AuthorizeAsync();

        if (denied is not null)
        {
            return denied;
        }

        var model = new MyWorkHomeViewModel { Nav = await _portal.NavAsync(User, worker, "home", HttpContext.RequestAborted) };

        if (worker is not null)
        {
            var now = _clock.UtcNow;
            var shifts = await MyShiftsAsync(worker, now.AddHours(-12), now.AddDays(ScheduleDays));
            var today = (await PlanningOptions.TodayAsync(_localClock));

            model.NextShift = shifts.Where(shift => !shift.IsTimeOff && shift.StartUtc > now).OrderBy(shift => shift.StartUtc).FirstOrDefault();
            model.Today = shifts.Where(shift => shift.Date == today).ToList();
            model.Inbox = (await _inbox.PageAsync(worker.ItemId, 0, 10, HttpContext.RequestAborted)).Items.ToList();
        }

        return View(model);
    }

    /// <summary>
    /// Marks the worker's notifications read.
    /// </summary>
    /// <returns>A redirect to the home page.</returns>
    [HttpPost("my-work/inbox/read")]
    public async Task<IActionResult> MarkRead()
    {
        var (worker, denied) = await AuthorizeWorkerAsync();

        if (denied is not null)
        {
            return denied;
        }

        await _inbox.MarkAllReadAsync(worker.ItemId, HttpContext.RequestAborted);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Lists the worker's shifts and time off for the next four weeks.
    /// </summary>
    /// <returns>The schedule.</returns>
    [HttpGet("my-work/schedule", Name = "MyWorkSchedule")]
    public async Task<IActionResult> Schedule()
    {
        var (worker, denied) = await AuthorizeWorkerAsync();

        if (denied is not null)
        {
            return denied;
        }

        var now = _clock.UtcNow;
        var items = await MyShiftsAsync(worker, now.AddHours(-12), now.AddDays(ScheduleDays));

        return View(new MyScheduleViewModel
        {
            Nav = await _portal.NavAsync(User, worker, "schedule", HttpContext.RequestAborted),
            Days = items.GroupBy(item => item.Date).OrderBy(group => group.Key).Select(group => (group.Key, (IList<MyShiftViewModel>)group.OrderBy(item => item.StartUtc).ToList())).ToList(),
        });
    }

    /// <summary>
    /// Shows the worker's balances, requests and the request form.
    /// </summary>
    /// <returns>The time off page.</returns>
    [HttpGet("my-work/time-off", Name = "MyWorkTimeOff")]
    public async Task<IActionResult> TimeOff()
    {
        var (worker, denied) = await AuthorizeTimeOffAsync();

        if (denied is not null)
        {
            return denied;
        }

        var tomorrow = (await PlanningOptions.TodayAsync(_localClock)).AddDays(1);
        var model = new MyTimeOffViewModel { StartDate = tomorrow, EndDate = tomorrow, StartTime = "13:00", EndTime = "17:00" };

        await PopulateTimeOffAsync(model, worker);

        return View(model);
    }

    /// <summary>
    /// Checks or submits the worker's request.
    /// </summary>
    /// <param name="model">The request.</param>
    /// <param name="check">Whether only to check it.</param>
    /// <returns>The time off page.</returns>
    [HttpPost("my-work/time-off")]
    public async Task<IActionResult> TimeOffPost(MyTimeOffViewModel model, bool check = false)
    {
        var (worker, denied) = await AuthorizeTimeOffAsync();

        if (denied is not null)
        {
            return denied;
        }

        var requests = HttpContext.RequestServices.GetRequiredService<ITimeOffRequestService>();
        var types = HttpContext.RequestServices.GetRequiredService<ITimeOffTypeManager>();
        var type = string.IsNullOrEmpty(model.TypeId) ? null : await types.FindByIdAsync(model.TypeId);

        if (type is null || !type.Enabled || !type.VisibleToWorkers)
        {
            ModelState.AddModelError(nameof(model.TypeId), S["Choose the kind of time off."]);
        }

        if (model.PartialDay)
        {
            model.EndDate = model.StartDate;
        }

        if (!model.StartDate.HasValue || !model.EndDate.HasValue)
        {
            ModelState.AddModelError(nameof(model.StartDate), S["Enter the first and the last day."]);
        }

        TimeOnly? startTime = null;
        TimeOnly? endTime = null;

        if (model.PartialDay)
        {
            if (PlanningInput.TryParseTime(model.StartTime, out var start) && PlanningInput.TryParseTime(model.EndTime, out var end))
            {
                startTime = start;
                endTime = end;
            }
            else
            {
                ModelState.AddModelError(nameof(model.StartTime), S["Enter when the time off starts and ends, such as 13:00 and 17:00."]);
            }
        }

        if (ModelState.IsValid)
        {
            var input = new TimeOffRequestInput
            {
                WorkerId = worker.ItemId,
                TypeId = type.ItemId,
                StartDate = model.StartDate.Value,
                EndDate = model.EndDate.Value,
                StartTime = startTime,
                EndTime = endTime,
                Reason = model.Reason,
            };

            if (check)
            {
                model.Preview = await requests.PreviewAsync(input, HttpContext.RequestAborted);
            }
            else
            {
                var result = await requests.SubmitAsync(input, User.Identity?.Name, HttpContext.RequestAborted);

                if (result.Succeeded)
                {
                    await _notifier.SuccessAsync(result.Value.Status switch
                    {
                        TimeOffRequestStatus.Approved => H["Your time off is approved."],
                        TimeOffRequestStatus.Waitlisted => H["As many people as may be off already are, so your request is on the waitlist. You'll hear if a place frees up."],
                        _ => H["Your request has been sent. You'll hear when it is decided."],
                    });

                    return RedirectToAction(nameof(TimeOff));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
            }
        }

        await PopulateTimeOffAsync(model, worker);

        return View(nameof(TimeOff), model);
    }

    /// <summary>
    /// Withdraws or cancels one of the worker's requests.
    /// </summary>
    /// <param name="id">The request.</param>
    /// <returns>A redirect to the time off page.</returns>
    [HttpPost("my-work/time-off/cancel/{id}")]
    public async Task<IActionResult> CancelTimeOff(string id)
    {
        var (worker, denied) = await AuthorizeTimeOffAsync();

        if (denied is not null)
        {
            return denied;
        }

        var requests = HttpContext.RequestServices.GetRequiredService<ITimeOffRequestService>();
        var request = await requests.FindAsync(id, HttpContext.RequestAborted);

        if (request is null || request.WorkerId != worker.ItemId)
        {
            return NotFound();
        }

        var result = await requests.CancelAsync(id, User.Identity?.Name, S["Canceled by {0}", worker.DisplayName], HttpContext.RequestAborted);

        await NotifyAsync(result.Errors, H["Your time off has been canceled."]);

        return RedirectToAction(nameof(TimeOff));
    }

    /// <summary>
    /// Shows the open shifts the worker can claim, their shifts to trade and the trades offered to them.
    /// </summary>
    /// <returns>The shifts page.</returns>
    [HttpGet("my-work/shifts", Name = "MyWorkShifts")]
    public async Task<IActionResult> Shifts()
    {
        var (worker, denied) = await AuthorizeWorkerAsync();

        if (denied is not null)
        {
            return denied;
        }

        var today = await PlanningOptions.TodayAsync(_localClock);
        var now = _clock.UtcNow;
        var teamIds = worker.MembershipsOn(today).Select(membership => membership.TeamId).Distinct(StringComparer.Ordinal).ToArray();
        var model = new MyShiftsViewModel
        {
            Nav = await _portal.NavAsync(User, worker, "shifts", HttpContext.RequestAborted),
            MyShifts = (await MyShiftsAsync(worker, now, now.AddDays(ScheduleDays))).Where(shift => !shift.IsTimeOff).ToList(),
        };

        if (teamIds.Length > 0)
        {
            var (_, open) = await _openShiftService.PageAsync(new OpenShiftQuery { TeamIds = teamIds, From = today }, 0, 50, HttpContext.RequestAborted);

            foreach (var shift in open)
            {
                var claim = shift.Claims.FirstOrDefault(item => item.WorkerId == worker.ItemId && item.Status == OpenShiftClaimStatus.Pending);

                model.OpenShifts.Add(new MyOpenShiftViewModel
                {
                    Shift = shift,
                    TeamName = (await _teamManager.FindByIdAsync(shift.TeamId))?.Name,
                    MyClaim = claim,
                    Problems = claim is null ? await _openShiftService.CheckAsync(shift.ItemId, worker.ItemId, HttpContext.RequestAborted) : [],
                });
            }

            var (_, trades) = await _tradeService.PageAsync(new ShiftTradeQuery { TeamIds = teamIds }, 0, 100, HttpContext.RequestAborted);

            foreach (var trade in trades.Where(trade => trade.Status is ShiftTradeStatus.Offered or ShiftTradeStatus.Accepted))
            {
                var row = await TradeRowAsync(trade);

                if (trade.OfferedByWorkerId == worker.ItemId)
                {
                    model.MyOffers.Add(row);
                }
                else if (trade.Status == ShiftTradeStatus.Offered && (trade.TargetWorkerId == worker.ItemId || trade.TargetWorkerId is null))
                {
                    model.OfferedToMe.Add(row);
                }
            }

            var colleagues = new Dictionary<string, WorkerProfile>(StringComparer.Ordinal);

            foreach (var teamId in teamIds)
            {
                foreach (var colleague in await _workerManager.ListByTeamAsync(teamId, today, HttpContext.RequestAborted))
                {
                    if (colleague.ItemId != worker.ItemId && colleague.Status == WorkerStatus.Active)
                    {
                        colleagues.TryAdd(colleague.ItemId, colleague);
                    }
                }
            }

            model.Colleagues = colleagues.Values.OrderBy(colleague => colleague.DisplayName, StringComparer.CurrentCultureIgnoreCase).Select(colleague => new SelectListItem(colleague.DisplayName, colleague.ItemId)).ToList();

            foreach (var colleague in colleagues.Values.OrderBy(colleague => colleague.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                foreach (var shift in (await MyShiftsAsync(colleague, now, now.AddDays(ScheduleDays))).Where(shift => !shift.IsTimeOff))
                {
                    model.ColleagueShifts.Add(new SelectListItem($"{colleague.DisplayName}: {shift.Start:ddd d MMM HH:mm}-{shift.End:HH:mm}", $"{colleague.ItemId}|{shift.EventId}"));
                }
            }
        }

        return View(model);
    }

    /// <summary>
    /// Claims an open shift.
    /// </summary>
    /// <param name="id">The open shift.</param>
    /// <returns>A redirect to the shifts page.</returns>
    [HttpPost("my-work/shifts/claim/{id}")]
    public async Task<IActionResult> Claim(string id)
    {
        var (worker, denied) = await AuthorizeWorkerAsync();

        if (denied is not null)
        {
            return denied;
        }

        var result = await _openShiftService.ClaimAsync(id, worker.ItemId, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result.Errors, result.Value?.FilledWorkerId == worker.ItemId ? H["The shift is yours; it is on your schedule."] : H["Your claim has been sent. You'll hear when it is decided."]);

        return RedirectToAction(nameof(Shifts));
    }

    /// <summary>
    /// Withdraws the worker's claim on an open shift.
    /// </summary>
    /// <param name="id">The open shift.</param>
    /// <returns>A redirect to the shifts page.</returns>
    [HttpPost("my-work/shifts/withdraw/{id}")]
    public async Task<IActionResult> Withdraw(string id)
    {
        var (worker, denied) = await AuthorizeWorkerAsync();

        if (denied is not null)
        {
            return denied;
        }

        var result = await _openShiftService.WithdrawClaimAsync(id, worker.ItemId, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result.Errors, H["Your claim has been withdrawn."]);

        return RedirectToAction(nameof(Shifts));
    }

    /// <summary>
    /// Offers one of the worker's shifts to a colleague, to anyone, or in a swap.
    /// </summary>
    /// <param name="shiftEventId">The worker's shift.</param>
    /// <param name="targetWorkerId">The colleague, or empty for anyone.</param>
    /// <param name="swapFor">For a swap, the colleague and their shift, as worker|event.</param>
    /// <param name="note">A note to the colleague.</param>
    /// <returns>A redirect to the shifts page.</returns>
    [HttpPost("my-work/trades/offer")]
    public async Task<IActionResult> Offer(string shiftEventId, string targetWorkerId, string swapFor, string note)
    {
        var (worker, denied) = await AuthorizeWorkerAsync();

        if (denied is not null)
        {
            return denied;
        }

        var offer = new ShiftTradeOffer { Kind = ShiftTradeKind.GiveAway, ShiftEventId = shiftEventId, TargetWorkerId = string.IsNullOrEmpty(targetWorkerId) ? null : targetWorkerId, Note = note };

        if (!string.IsNullOrEmpty(swapFor) && swapFor.Split('|') is [var colleagueId, var colleagueShiftId])
        {
            offer.Kind = ShiftTradeKind.Swap;
            offer.TargetWorkerId = colleagueId;
            offer.TargetShiftEventId = colleagueShiftId;
        }

        var shift = (await _shiftService.ListWorkerShiftsAsync(worker.ItemId, _clock.UtcNow, _clock.UtcNow.AddDays(ScheduleDays), HttpContext.RequestAborted)).FirstOrDefault(item => item.ItemId == shiftEventId);

        if (shift is null)
        {
            await _notifier.ErrorAsync(H["Choose one of your upcoming shifts."]);

            return RedirectToAction(nameof(Shifts));
        }

        var result = await _tradeService.OfferAsync(offer, User.Identity?.Name, HttpContext.RequestAborted);

        await NotifyAsync(result.Errors, offer.Kind == ShiftTradeKind.Swap ? H["Your swap has been offered."] : H["Your shift has been offered."]);

        return RedirectToAction(nameof(Shifts));
    }

    /// <summary>
    /// Accepts, turns down or takes back a trade.
    /// </summary>
    /// <param name="id">The trade.</param>
    /// <param name="decision">accept, reject or cancel.</param>
    /// <returns>A redirect to the shifts page.</returns>
    [HttpPost("my-work/trades/{id}/{decision}")]
    public async Task<IActionResult> Trade(string id, string decision)
    {
        var (worker, denied) = await AuthorizeWorkerAsync();

        if (denied is not null)
        {
            return denied;
        }

        var trade = await _tradeService.FindAsync(id, HttpContext.RequestAborted);

        if (trade is null)
        {
            return NotFound();
        }

        PlanningResult<ShiftTrade> result;
        LocalizedHtmlString success;

        switch (decision)
        {
            case "accept":
                result = await _tradeService.AcceptAsync(id, worker.ItemId, User.Identity?.Name, HttpContext.RequestAborted);
                success = result.Value?.Status == ShiftTradeStatus.Accepted ? H["You accepted the trade. It waits for a manager."] : H["The trade went through; your schedule has changed."];
                break;
            case "reject":
                result = await _tradeService.RejectAsync(id, worker.ItemId, User.Identity?.Name, HttpContext.RequestAborted);
                success = H["You turned the trade down."];
                break;
            case "cancel" when trade.OfferedByWorkerId == worker.ItemId:
                result = await _tradeService.CancelAsync(id, User.Identity?.Name, HttpContext.RequestAborted);
                success = H["Your offer has been taken back."];
                break;
            default:
                return NotFound();
        }

        await NotifyAsync(result.Errors, success);

        return RedirectToAction(nameof(Shifts));
    }

    /// <summary>
    /// Shows the worker's availability.
    /// </summary>
    /// <returns>The availability page.</returns>
    [HttpGet("my-work/availability", Name = "MyWorkAvailability")]
    public async Task<IActionResult> Availability()
    {
        var (worker, denied) = await AuthorizeWorkerAsync();

        if (denied is not null)
        {
            return denied;
        }

        return View(new MyAvailabilityViewModel
        {
            Nav = await _portal.NavAsync(User, worker, "availability", HttpContext.RequestAborted),
            Availability = AvailabilityFormat.Write(worker.Availability),
        });
    }

    /// <summary>
    /// Saves the worker's availability.
    /// </summary>
    /// <param name="model">The availability.</param>
    /// <returns>A redirect, or the page with errors.</returns>
    [HttpPost("my-work/availability")]
    public async Task<IActionResult> AvailabilityPost(MyAvailabilityViewModel model)
    {
        var (worker, denied) = await AuthorizeWorkerAsync();

        if (denied is not null)
        {
            return denied;
        }

        var errors = new List<string>();
        var windows = AvailabilityFormat.Read(model.Availability, errors);

        foreach (var error in errors)
        {
            ModelState.AddModelError(nameof(model.Availability), S["This line isn't understood: {0}", error]);
        }

        if (!ModelState.IsValid)
        {
            model.Nav = await _portal.NavAsync(User, worker, "availability", HttpContext.RequestAborted);

            return View(nameof(Availability), model);
        }

        worker.Availability = windows;
        await _workerManager.UpdateAsync(worker);
        await _notifier.SuccessAsync(H["Your availability has been saved. New schedules will follow it."]);

        return RedirectToAction(nameof(Availability));
    }

    /// <summary>
    /// Shows the worker's notification settings, devices and calendar feed.
    /// </summary>
    /// <returns>The settings page.</returns>
    [HttpGet("my-work/settings", Name = "MyWorkSettings")]
    public async Task<IActionResult> Settings()
    {
        var (worker, denied) = await AuthorizeWorkerAsync();

        if (denied is not null)
        {
            return denied;
        }

        var settings = worker.TryGet<WorkerPortalSettings>(out var stored) ? stored : new WorkerPortalSettings();
        var token = await _feedService.GetTokenAsync(worker, false, HttpContext.RequestAborted);

        return View(new MySettingsViewModel
        {
            Nav = await _portal.NavAsync(User, worker, "settings", HttpContext.RequestAborted),
            Channels = _channels.Where(channel => channel.CanBeTurnedOff).Select(channel => new MyNotificationOption { Value = channel.Name, Label = channel.DisplayName, Enabled = !settings.DisabledChannels.Contains(channel.Name) }).ToList(),
            Kinds = KindLabels().Select(kind => new MyNotificationOption { Value = kind.Kind.ToString(), Label = kind.Label, Enabled = !settings.DisabledKinds.Contains(kind.Kind) }).ToList(),
            Devices = (await _pushService.ListAsync(worker.ItemId, HttpContext.RequestAborted)).ToList(),
            FeedUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/my-work/feed/{token}.ics",
            PushPublicKey = await _pushService.GetPublicKeyAsync(HttpContext.RequestAborted),
        });
    }

    /// <summary>
    /// Saves which channels and kinds of notification the worker hears about.
    /// </summary>
    /// <param name="channels">The channels left on.</param>
    /// <param name="kinds">The kinds left on.</param>
    /// <returns>A redirect to the settings page.</returns>
    [HttpPost("my-work/settings")]
    public async Task<IActionResult> SettingsPost(string[] channels, string[] kinds)
    {
        var (worker, denied) = await AuthorizeWorkerAsync();

        if (denied is not null)
        {
            return denied;
        }

        var on = new HashSet<string>(channels ?? [], StringComparer.Ordinal);
        var kindsOn = new HashSet<string>(kinds ?? [], StringComparer.Ordinal);
        var settings = worker.TryGet<WorkerPortalSettings>(out var stored) ? stored : new WorkerPortalSettings();

        settings.DisabledChannels = _channels.Where(channel => channel.CanBeTurnedOff && !on.Contains(channel.Name)).Select(channel => channel.Name).ToList();
        settings.DisabledKinds = Enum.GetValues<WorkforceNotificationKind>().Where(kind => !kindsOn.Contains(kind.ToString())).ToList();
        worker.Put(settings);
        await _workerManager.UpdateAsync(worker);
        await _notifier.SuccessAsync(H["Your notification settings have been saved."]);

        return RedirectToAction(nameof(Settings));
    }

    /// <summary>
    /// Replaces the worker's calendar feed address, so the old one stops working.
    /// </summary>
    /// <returns>A redirect to the settings page.</returns>
    [HttpPost("my-work/feed/renew")]
    public async Task<IActionResult> RenewFeed()
    {
        var (worker, denied) = await AuthorizeWorkerAsync();

        if (denied is not null)
        {
            return denied;
        }

        await _feedService.GetTokenAsync(worker, true, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["Your calendar address has been replaced. Subscribe again with the new one."]);

        return RedirectToAction(nameof(Settings));
    }

    /// <summary>
    /// Turns push notifications on for this device.
    /// </summary>
    /// <param name="input">The browser's push subscription.</param>
    /// <returns>No content.</returns>
    [HttpPost("my-work/push/subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionInput input)
    {
        var (worker, denied) = await AuthorizeWorkerAsync();

        if (denied is not null)
        {
            return denied;
        }

        if (input is null || !Uri.TryCreate(input.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(input.Keys?.P256dh) || string.IsNullOrEmpty(input.Keys?.Auth))
        {
            return BadRequest();
        }

        await _pushService.SubscribeAsync(worker.ItemId, input.Endpoint, input.Keys.P256dh, input.Keys.Auth, Request.Headers.UserAgent.ToString(), HttpContext.RequestAborted);

        return NoContent();
    }

    /// <summary>
    /// Turns push notifications off for a device.
    /// </summary>
    /// <param name="endpoint">The device's push endpoint.</param>
    /// <returns>A redirect to the settings page.</returns>
    [HttpPost("my-work/push/unsubscribe")]
    public async Task<IActionResult> Unsubscribe(string endpoint)
    {
        var (worker, denied) = await AuthorizeWorkerAsync();

        if (denied is not null)
        {
            return denied;
        }

        await _pushService.UnsubscribeAsync(worker.ItemId, endpoint, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["Push notifications are off for that device."]);

        return RedirectToAction(nameof(Settings));
    }

    /// <summary>
    /// The worker's calendar feed, found by its secret.
    /// </summary>
    /// <param name="token">The secret.</param>
    /// <returns>The iCalendar feed.</returns>
    [AllowAnonymous]
    [HttpGet("my-work/feed/{token}.ics")]
    public async Task<IActionResult> Feed(string token)
    {
        var text = await _feedService.WriteAsync(token, HttpContext.RequestAborted);

        return text is null ? NotFound() : Content(text, "text/calendar; charset=utf-8");
    }

    /// <summary>
    /// The portal's web app manifest, so it can be installed.
    /// </summary>
    /// <returns>The manifest.</returns>
    [AllowAnonymous]
    [HttpGet("my-work/manifest.webmanifest")]
    public async Task<IActionResult> Manifest()
    {
        var site = await _siteService.GetSiteSettingsAsync();
        var icons = Url.Content("~/CrestApps.OrchardCore.Workforce/portal/");
        var manifest = new
        {
            name = S["My Work: {0}", site.SiteName].Value,
            short_name = S["My Work"].Value,
            description = S["Your shifts, time off and notifications."].Value,
            start_url = Url.Content("~/my-work"),
            scope = Url.Content("~/my-work"),
            display = "standalone",
            background_color = "#ffffff",
            theme_color = "#2b8a3e",
            lang = CultureInfo.CurrentUICulture.Name,
            icons = new object[]
            {
                new { src = icons + "icon-192.png", sizes = "192x192", type = "image/png", purpose = "any" },
                new { src = icons + "icon-512.png", sizes = "512x512", type = "image/png", purpose = "any" },
                new { src = icons + "icon-maskable-512.png", sizes = "512x512", type = "image/png", purpose = "maskable" },
            },
        };

        return new JsonResult(manifest) { ContentType = "application/manifest+json" };
    }

    /// <summary>
    /// The portal's service worker: it shows push notifications and opens their page, and shows a page of its own
    /// when the portal is opened offline.
    /// </summary>
    /// <returns>The script.</returns>
    [AllowAnonymous]
    [HttpGet("my-work/sw.js")]
    public IActionResult ServiceWorker()
    {
        var home = Url.Content("~/my-work");
        var icon = Url.Content("~/CrestApps.OrchardCore.Workforce/portal/icon-192.png");
        var offline = System.Text.Json.JsonSerializer.Serialize(S["You're offline. My Work needs a connection; it will load again once you're back online."].Value);
        var script = $$"""
            const home = '{{home}}';
            const icon = '{{icon}}';
            self.addEventListener('install', () => self.skipWaiting());
            self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
            self.addEventListener('fetch', event => {
                if (event.request.mode !== 'navigate') {
                    return;
                }
                event.respondWith(fetch(event.request).catch(() => new Response(
                    '<!doctype html><meta name="viewport" content="width=device-width, initial-scale=1"><title>My Work</title><p style="font-family:system-ui;padding:2rem">' + {{offline}} + '</p>',
                    { headers: { 'Content-Type': 'text/html; charset=utf-8' } })));
            });
            self.addEventListener('push', event => {
                let data = {};
                try { data = event.data ? event.data.json() : {}; } catch { data = { body: event.data ? event.data.text() : '' }; }
                event.waitUntil(self.registration.showNotification(data.title || 'My Work', { body: data.body || '', icon, badge: icon, data: { url: data.url || home } }));
            });
            self.addEventListener('notificationclick', event => {
                event.notification.close();
                const url = (event.notification.data && event.notification.data.url) || home;
                event.waitUntil(self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(windows => {
                    for (const client of windows) {
                        if (client.url.includes(home) && 'focus' in client) {
                            client.navigate(url);
                            return client.focus();
                        }
                    }
                    return self.clients.openWindow(url);
                }));
            });
            """;

        Response.Headers["Service-Worker-Allowed"] = Url.Content("~/my-work");
        Response.Headers.CacheControl = "no-cache";

        return Content(script, "text/javascript; charset=utf-8");
    }

    private async Task<(WorkerProfile Worker, IActionResult Denied)> AuthorizeAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return (null, Challenge());
        }

        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.UseWorkforcePortal))
        {
            return (null, Forbid());
        }

        return (await _portal.GetWorkerAsync(User), null);
    }

    private async Task<(WorkerProfile Worker, IActionResult Denied)> AuthorizeWorkerAsync()
    {
        var (worker, denied) = await AuthorizeAsync();

        if (denied is not null)
        {
            return (null, denied);
        }

        return worker is null ? (null, RedirectToAction(nameof(Index))) : (worker, null);
    }

    private async Task<(WorkerProfile Worker, IActionResult Denied)> AuthorizeTimeOffAsync()
    {
        var (worker, denied) = await AuthorizeWorkerAsync();

        if (denied is not null)
        {
            return (null, denied);
        }

        if (HttpContext.RequestServices.GetService<ITimeOffRequestService>() is null || !await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.RequestTimeOff))
        {
            return (null, NotFound());
        }

        return (worker, null);
    }

    private async Task PopulateTimeOffAsync(MyTimeOffViewModel model, WorkerProfile worker)
    {
        var services = HttpContext.RequestServices;
        var today = await PlanningOptions.TodayAsync(_localClock);
        var types = (await services.GetRequiredService<ITimeOffTypeManager>().ListEnabledAsync(HttpContext.RequestAborted)).Where(type => type.VisibleToWorkers).ToList();
        var typeById = (await services.GetRequiredService<ITimeOffTypeManager>().GetAllAsync(HttpContext.RequestAborted)).ToDictionary(type => type.ItemId, StringComparer.Ordinal);
        var requests = services.GetRequiredService<ITimeOffRequestService>();
        var mine = new List<TimeOffRequest>();

        foreach (var status in new TimeOffRequestStatus?[] { null, TimeOffRequestStatus.Approved, TimeOffRequestStatus.Declined })
        {
            mine.AddRange((await requests.PageAsync(new TimeOffRequestQuery { WorkerId = worker.ItemId, Status = status, From = today.AddDays(-30) }, 0, 20, HttpContext.RequestAborted)).Items);
        }

        model.Nav = await _portal.NavAsync(User, worker, "time-off", HttpContext.RequestAborted);
        model.Balances = (await services.GetRequiredService<ITimeOffBalanceService>().GetBalancesAsync(worker.ItemId, today, HttpContext.RequestAborted)).Where(balance => balance.Type?.VisibleToWorkers != false).ToList();
        model.Requests = mine.OrderByDescending(request => request.StartDate).Select(request => (request, typeById.TryGetValue(request.TypeId ?? string.Empty, out var type) ? type : null)).ToList();
        model.TypeOptions = types.Select(type => new SelectListItem(type.Name, type.ItemId, type.ItemId == model.TypeId)).ToList();
    }

    private async Task<List<MyShiftViewModel>> MyShiftsAsync(WorkerProfile worker, DateTime fromUtc, DateTime toUtc)
    {
        var codes = (await _activityCodeManager.GetAllAsync(HttpContext.RequestAborted)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);
        var teams = new Dictionary<string, WorkforceTeam>(StringComparer.Ordinal);
        var items = new List<MyShiftViewModel>();

        foreach (var shift in await _shiftService.ListWorkerShiftsAsync(worker.ItemId, fromUtc, toUtc, HttpContext.RequestAborted))
        {
            if (!shift.TryGet<ShiftData>(out var data))
            {
                continue;
            }

            if (!teams.TryGetValue(data.TeamId ?? string.Empty, out var team))
            {
                team = await _teamManager.FindByIdAsync(data.TeamId);
                teams[data.TeamId ?? string.Empty] = team;
            }

            var zone = await PlanningOptions.TimeZoneOfAsync(team, _siteService);
            var start = CalendarTime.ToLocal(shift.StartUtc, zone);

            items.Add(new MyShiftViewModel
            {
                EventId = shift.ItemId,
                Date = DateOnly.FromDateTime(start),
                Start = start,
                End = CalendarTime.ToLocal(shift.EndUtc, zone),
                StartUtc = shift.StartUtc,
                TeamName = team?.Name,
                Segments = string.Join(", ", data.Segments.Select(segment => $"{(segment.ActivityCodeId is not null && codes.TryGetValue(segment.ActivityCodeId, out var code) ? code.Name : segment.Kind.ToString())} {CalendarTime.ToLocal(segment.StartUtc, zone):HH:mm}-{CalendarTime.ToLocal(segment.EndUtc, zone):HH:mm}")),
            });
        }

        if (HttpContext.RequestServices.GetService<ITimeOffRequestService>() is { } requests)
        {
            var types = (await HttpContext.RequestServices.GetRequiredService<ITimeOffTypeManager>().GetAllAsync(HttpContext.RequestAborted)).ToDictionary(type => type.ItemId, StringComparer.Ordinal);
            var from = DateOnly.FromDateTime(fromUtc).AddDays(-1);
            var to = DateOnly.FromDateTime(toUtc).AddDays(1);
            var (_, approved) = await requests.PageAsync(new TimeOffRequestQuery { WorkerId = worker.ItemId, Status = TimeOffRequestStatus.Approved, From = from, To = to }, 0, 50, HttpContext.RequestAborted);

            foreach (var request in approved)
            {
                for (var day = request.StartDate; day <= request.EndDate; day = day.AddDays(1))
                {
                    if (request.Days.FirstOrDefault(item => item.Date == day) is { Amount: 0 })
                    {
                        continue;
                    }

                    var start = request.IsPartialDay ? day.ToDateTime(request.StartTime.Value) : day.ToDateTime(TimeOnly.MinValue);

                    items.Add(new MyShiftViewModel
                    {
                        EventId = request.ItemId,
                        Date = day,
                        Start = start,
                        End = request.IsPartialDay ? day.ToDateTime(request.EndTime.Value) : day.AddDays(1).ToDateTime(TimeOnly.MinValue),
                        StartUtc = CalendarTime.ToUtc(start, request.TimeZoneId),
                        IsTimeOff = true,
                        AllDay = !request.IsPartialDay,
                        Title = types.TryGetValue(request.TypeId ?? string.Empty, out var type) ? type.Name : S["Time off"].Value,
                    });
                }
            }
        }

        return items.OrderBy(item => item.StartUtc).ToList();
    }

    private async Task<MyTradeViewModel> TradeRowAsync(ShiftTrade trade)
    {
        var target = string.IsNullOrEmpty(trade.TargetWorkerId) ? null : await _workerManager.FindByIdAsync(trade.TargetWorkerId);
        var team = await _teamManager.FindByIdAsync(trade.TeamId);
        var zone = await PlanningOptions.TimeZoneOfAsync(team, _siteService);

        return new MyTradeViewModel
        {
            Trade = trade,
            FromName = (await _workerManager.FindByIdAsync(trade.OfferedByWorkerId))?.DisplayName,
            ToName = target?.DisplayName,
            ShiftText = CalendarTime.ToLocal(trade.ShiftStartUtc, zone).ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture),
            TargetShiftText = trade.TargetShiftStartUtc.HasValue ? CalendarTime.ToLocal(trade.TargetShiftStartUtc.Value, zone).ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture) : null,
        };
    }

    private IEnumerable<(WorkforceNotificationKind Kind, string Label)> KindLabels()
    {
        yield return (WorkforceNotificationKind.SchedulePublished, S["My schedule is published or changes"]);
        yield return (WorkforceNotificationKind.ShiftChanged, S["One of my shifts changes"]);
        yield return (WorkforceNotificationKind.TimeOff, S["My time off is decided"]);
        yield return (WorkforceNotificationKind.OpenShift, S["I get an open shift"]);
        yield return (WorkforceNotificationKind.ShiftTrade, S["A trade is offered to me, or mine is decided"]);
    }

    private async Task NotifyAsync(IReadOnlyList<string> errors, LocalizedHtmlString success)
    {
        if (errors.Count == 0)
        {
            await _notifier.SuccessAsync(success);

            return;
        }

        foreach (var error in errors)
        {
            await _notifier.ErrorAsync(H["{0}", error]);
        }
    }
}

/// <summary>
/// A browser's push subscription, as the Push API gives it.
/// </summary>
public sealed class PushSubscriptionInput
{
    /// <summary>
    /// Gets or sets the push endpoint.
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the device's keys.
    /// </summary>
    public PushSubscriptionKeys Keys { get; set; }
}

/// <summary>
/// The keys of a browser's push subscription.
/// </summary>
public sealed class PushSubscriptionKeys
{
    /// <summary>
    /// Gets or sets the device's public key.
    /// </summary>
    public string P256dh { get; set; }

    /// <summary>
    /// Gets or sets the device's secret.
    /// </summary>
    public string Auth { get; set; }
}
