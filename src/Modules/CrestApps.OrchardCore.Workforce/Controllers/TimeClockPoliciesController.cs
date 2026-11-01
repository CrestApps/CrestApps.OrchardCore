using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The time clock policies: the list and the editor.
/// </summary>
[Feature(WorkforceConstants.Features.TimeClock)]
public sealed class TimeClockPoliciesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ITimeClockPolicyManager _manager;
    private readonly INotifier _notifier;
    private readonly IPayPeriodCalendarManager _calendarManager;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeClockPoliciesController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="manager">The manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="calendarManager">The pay period manager.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeClockPoliciesController(
        IAuthorizationService authorizationService,
        ITimeClockPolicyManager manager,
        INotifier notifier,
        IPayPeriodCalendarManager calendarManager,
        IHtmlLocalizer<TimeClockPoliciesController> htmlLocalizer,
        IStringLocalizer<TimeClockPoliciesController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _manager = manager;
        _notifier = notifier;
        _calendarManager = calendarManager;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists them.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/time-clock-policies", "WorkforceTimeClockPolicies")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeClockPolicies))
        {
            return Forbid();
        }

        var items = (await _manager.GetAllAsync(HttpContext.RequestAborted))
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new WorkforceListViewModel
        {
            List = new AdminListContext<TimeClockPolicy>
            {
                Source = items.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, items.Count),
            },
        });
    }

    /// <summary>
    /// Shows the editor.
    /// </summary>
    /// <param name="id">The id, or empty for new.</param>
    /// <returns>The editor.</returns>
    [Admin("workforce/time-clock-policies/edit/{id?}", "WorkforceTimeClockPoliciesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeClockPolicies))
        {
            return Forbid();
        }

        var item = string.IsNullOrEmpty(id) ? await _manager.NewAsync() : await _manager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        var model = new TimeClockPolicyEditViewModel
        {
            Id = id,
            Name = item.Name,
            Description = item.Description,
            PayPeriodCalendarId = item.PayPeriodCalendarId,
            RoundingMinutes = item.RoundingMinutes,
            RoundingDirection = item.RoundingDirection.ToString(),
            EarlyGraceMinutes = item.EarlyGraceMinutes,
            LateGraceMinutes = item.LateGraceMinutes,
            BlockEarlyPunchInMinutes = item.BlockEarlyPunchInMinutes,
            AutoMealAfterHours = item.AutoMealAfterHours,
            AutoMealMinutes = item.AutoMealMinutes,
            LongMealMinutes = item.LongMealMinutes,
            AllowedSources = item.AllowedSources.Select(source => source.ToString()).ToList(),
            RequireGeofence = item.RequireGeofence,
            GeofenceRadiusMeters = item.GeofenceRadiusMeters,
            MissedPunchHandling = item.MissedPunchHandling.ToString(),
            DailyOvertimeHours = item.DailyOvertimeHours,
            DailyDoubleTimeHours = item.DailyDoubleTimeHours,
            WeeklyOvertimeHours = item.WeeklyOvertimeHours,
            Points = Enum.GetValues<AttendanceExceptionKind>().ToDictionary(kind => kind.ToString(), kind => item.AttendancePoints.FirstOrDefault(rule => rule.Kind == kind)?.Points),
            PointsWindowDays = item.PointsWindowDays,
            PointsThreshold = item.PointsThreshold,
            Enabled = item.Enabled,
        };

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves it.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("workforce/time-clock-policies/edit/{id?}", "WorkforceTimeClockPoliciesEdit")]
    public async Task<IActionResult> EditPost(TimeClockPolicyEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeClockPolicies))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(model.Id);
        var item = isNew ? await _manager.NewAsync() : await _manager.FindByIdAsync(model.Id);

        if (item is null)
        {
            return NotFound();
        }

        item.Name = model.Name;
        item.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        item.PayPeriodCalendarId = model.PayPeriodCalendarId;
        item.RoundingMinutes = model.RoundingMinutes;
        item.RoundingDirection = Enum.TryParse<TimeRoundingDirection>(model.RoundingDirection, out var direction) ? direction : TimeRoundingDirection.Nearest;
        item.EarlyGraceMinutes = model.EarlyGraceMinutes;
        item.LateGraceMinutes = model.LateGraceMinutes;
        item.BlockEarlyPunchInMinutes = model.BlockEarlyPunchInMinutes;
        item.AutoMealAfterHours = model.AutoMealAfterHours;
        item.AutoMealMinutes = model.AutoMealMinutes;
        item.LongMealMinutes = model.LongMealMinutes;
        item.AllowedSources = (model.AllowedSources ?? []).Select(source => Enum.TryParse<TimePunchSource>(source, out var parsed) ? parsed : (TimePunchSource?)null).Where(source => source.HasValue && source != TimePunchSource.Manager).Select(source => source.Value).Distinct().ToList();
        item.RequireGeofence = model.RequireGeofence;
        item.GeofenceRadiusMeters = model.GeofenceRadiusMeters;
        item.MissedPunchHandling = Enum.TryParse<MissedPunchHandling>(model.MissedPunchHandling, out var missed) ? missed : MissedPunchHandling.Flag;
        item.DailyOvertimeHours = model.DailyOvertimeHours;
        item.DailyDoubleTimeHours = model.DailyDoubleTimeHours;
        item.WeeklyOvertimeHours = model.WeeklyOvertimeHours;
        item.AttendancePoints = (model.Points ?? new Dictionary<string, decimal?>())
            .Where(pair => pair.Value is > 0 && Enum.TryParse<AttendanceExceptionKind>(pair.Key, out _))
            .Select(pair => new AttendancePointRule { Kind = Enum.Parse<AttendanceExceptionKind>(pair.Key), Points = pair.Value.Value })
            .ToList();
        item.PointsWindowDays = model.PointsWindowDays;
        item.PointsThreshold = model.PointsThreshold;
        item.Enabled = model.Enabled;

        if (ModelState.IsValid)
        {
            var validation = await _manager.ValidateAsync(item);

            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync(model);

            return View(model);
        }

        if (isNew)
        {
            await _manager.CreateAsync(item);
        }
        else
        {
            await _manager.UpdateAsync(item);
        }

        await _notifier.SuccessAsync(H["The time clock policy has been saved."]);

        return AfterSave(item, isNew);
    }

    /// <summary>
    /// Deletes it.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/time-clock-policies/delete/{id}", "WorkforceTimeClockPoliciesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeClockPolicies))
        {
            return Forbid();
        }

        var item = await _manager.FindByIdAsync(id);

        if (item is not null)
        {
            await _manager.DeleteAsync(item);
            await _notifier.SuccessAsync(H["The time clock policy has been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private RedirectToActionResult AfterSave(TimeClockPolicy item, bool isNew)
    {
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync(TimeClockPolicyEditViewModel model)
    {
        model.CalendarOptions = (await _calendarManager.ListEnabledAsync(HttpContext.RequestAborted))
            .Select(calendar => new SelectListItem(calendar.Name, calendar.ItemId, calendar.ItemId == model.PayPeriodCalendarId))
            .ToList();
    }
}
