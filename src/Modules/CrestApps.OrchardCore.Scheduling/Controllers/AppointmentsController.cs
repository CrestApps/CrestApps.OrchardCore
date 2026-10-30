using System.Globalization;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Scheduling.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Scheduling.Controllers;

/// <summary>
/// The list of appointments, booking by staff, and an appointment's page with its actions.
/// </summary>
public sealed class AppointmentsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IAppointmentService _appointmentService;
    private readonly AppointmentStore _appointmentStore;
    private readonly AppointmentTypeStore _typeStore;
    private readonly SchedulingPageService _pageService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppointmentsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="appointmentService">The appointment service.</param>
    /// <param name="appointmentStore">The appointment store.</param>
    /// <param name="typeStore">The appointment type store.</param>
    /// <param name="pageService">The page builder.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AppointmentsController(
        IAuthorizationService authorizationService,
        IAppointmentService appointmentService,
        AppointmentStore appointmentStore,
        AppointmentTypeStore typeStore,
        SchedulingPageService pageService,
        INotifier notifier,
        IHtmlLocalizer<AppointmentsController> htmlLocalizer,
        IStringLocalizer<AppointmentsController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _appointmentService = appointmentService;
        _appointmentStore = appointmentStore;
        _typeStore = typeStore;
        _pageService = pageService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists appointments.
    /// </summary>
    /// <param name="search">Text the customer must match.</param>
    /// <param name="status">The state to show.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("scheduling/appointments", "SchedulingAppointments")]
    public async Task<IActionResult> Index(string search, string status, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ViewAppointments))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        AppointmentStatus? filter = Enum.TryParse<AppointmentStatus>(status, out var parsed) ? parsed : null;
        var (count, appointments) = await _appointmentStore.PageAsync(filter, search, null, pager.GetStartIndex(), pager.PageSize);
        var model = new SchedulingListViewModel
        {
            Search = search,
            Status = status,
            List = new AdminListContext<Appointment>
            {
                Source = appointments,
                Pager = await shapeFactory.PagerAsync(pager, count, new RouteValueDictionary { { "search", search }, { "status", status } }),
            },
            StatusOptions =
            [
                new SelectListItem(S["Requested"], nameof(AppointmentStatus.Requested)),
                new SelectListItem(S["Confirmed"], nameof(AppointmentStatus.Confirmed)),
                new SelectListItem(S["Held"], nameof(AppointmentStatus.Held)),
                new SelectListItem(S["Completed"], nameof(AppointmentStatus.Completed)),
                new SelectListItem(S["No-show"], nameof(AppointmentStatus.NoShow)),
                new SelectListItem(S["Cancelled"], nameof(AppointmentStatus.Cancelled)),
                new SelectListItem(S["Expired"], nameof(AppointmentStatus.Expired)),
            ],
        };

        return View(model);
    }

    /// <summary>
    /// Shows the page that books an appointment.
    /// </summary>
    /// <param name="model">The appointment type and day.</param>
    /// <returns>The page.</returns>
    [Admin("scheduling/appointments/new", "SchedulingAppointmentsNew")]
    public async Task<IActionResult> New(NewAppointmentViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageAppointments))
        {
            return Forbid();
        }

        ModelState.Clear();
        await FillAsync(model);

        return View(model);
    }

    /// <summary>
    /// Books an appointment.
    /// </summary>
    /// <param name="model">The booking.</param>
    /// <returns>A redirect to the appointment, or the page with errors.</returns>
    [HttpPost]
    [ActionName(nameof(New))]
    [Admin("scheduling/appointments/new", "SchedulingAppointmentsNew")]
    public async Task<IActionResult> NewPost(NewAppointmentViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageAppointments))
        {
            return Forbid();
        }

        if (DateTimeOffset.TryParse(model.Start, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var start))
        {
            var result = await _appointmentService.BookAsync(new AppointmentRequest
            {
                AppointmentTypeId = model.AppointmentTypeId,
                StartUtc = start.UtcDateTime,
                CustomerName = model.CustomerName,
                CustomerEmail = model.CustomerEmail,
                CustomerPhone = model.CustomerPhone,
                Notes = model.Notes,
                Source = "Admin",
                Actor = User.Identity?.Name,
            });

            if (result.Succeeded)
            {
                await _notifier.SuccessAsync(H["The appointment has been booked."]);

                return RedirectToAction(nameof(Detail), new { id = result.Appointment.ItemId });
            }

            foreach (var error in result.Errors)
            {
                model.Errors.Add(error);
            }
        }
        else
        {
            model.Errors.Add(S["Choose a time."]);
        }

        await FillAsync(model);

        return View(model);
    }

    /// <summary>
    /// Shows an appointment.
    /// </summary>
    /// <param name="id">The appointment's id.</param>
    /// <param name="date">The day to offer new times on.</param>
    /// <returns>The page.</returns>
    [Admin("scheduling/appointments/{id}", "SchedulingAppointmentsDetail")]
    public async Task<IActionResult> Detail(string id, string date)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ViewAppointments))
        {
            return Forbid();
        }

        var appointment = await _appointmentService.FindByIdAsync(id);

        if (appointment is null)
        {
            return NotFound();
        }

        var model = await _pageService.BuildDetailAsync(appointment, date);

        model.ManageUrl = Url.RouteUrl("SchedulingManage", new { id = appointment.ItemId, token = appointment.ManageToken }, Request.Scheme);

        return View(model);
    }

    /// <summary>
    /// Confirms, completes, marks as missed, cancels or moves an appointment.
    /// </summary>
    /// <param name="id">The appointment's id.</param>
    /// <param name="operation">Confirm, Complete, NoShow, Cancel or Reschedule.</param>
    /// <param name="start">The new start, for a move.</param>
    /// <param name="reason">Why, for a cancellation.</param>
    /// <returns>A redirect to the appointment.</returns>
    [HttpPost]
    [Admin("scheduling/appointments/{id}/do", "SchedulingAppointmentsDo")]
    public async Task<IActionResult> Do(string id, string operation, string start, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SchedulingPermissions.ManageAppointments))
        {
            return Forbid();
        }

        var appointment = await _appointmentService.FindByIdAsync(id);

        if (appointment is null)
        {
            return NotFound();
        }

        var actor = User.Identity?.Name;
        var result = operation switch
        {
            "Confirm" => await _appointmentService.ConfirmAsync(appointment, actor),
            "Complete" => await _appointmentService.CompleteAsync(appointment, noShow: false, actor),
            "NoShow" => await _appointmentService.CompleteAsync(appointment, noShow: true, actor),
            "Cancel" => await _appointmentService.CancelAsync(appointment, reason, actor),
            "Reschedule" when DateTimeOffset.TryParse(start, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var newStart) => await _appointmentService.RescheduleAsync(appointment, newStart.UtcDateTime, actor: actor),
            _ => null,
        };

        if (result is null)
        {
            await _notifier.ErrorAsync(H["Choose what to do."]);
        }
        else if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["The appointment has been updated."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["The appointment could not be updated: {0}", string.Join(" ", result.Errors)]);
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    private async Task FillAsync(NewAppointmentViewModel model)
    {
        var types = (await _typeStore.ListAsync()).Where(type => type.Enabled).ToList();
        var type = types.FirstOrDefault(item => item.ItemId == model.AppointmentTypeId) ?? types.FirstOrDefault();

        model.TypeOptions = types.Select(item => new SelectListItem(item.Name, item.ItemId, item.ItemId == type?.ItemId)).ToList();

        if (type is not null)
        {
            await _pageService.FillSlotsAsync(model, type);
        }
    }
}
