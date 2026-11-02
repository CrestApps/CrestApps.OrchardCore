using System.Globalization;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Scheduling.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Scheduling.Controllers;

/// <summary>
/// The public booking pages: pick a time and book, and the customer's manage link to move or cancel.
/// </summary>
[Feature(SchedulingConstants.Features.SelfService)]
[AllowAnonymous]
public sealed class BookingPagesController : Controller
{
    private readonly AppointmentTypeStore _typeStore;
    private readonly IAppointmentService _appointmentService;
    private readonly SchedulingPageService _pageService;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingPagesController"/> class.
    /// </summary>
    /// <param name="typeStore">The appointment type store.</param>
    /// <param name="appointmentService">The appointment service.</param>
    /// <param name="pageService">The page builder.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BookingPagesController(
        AppointmentTypeStore typeStore,
        IAppointmentService appointmentService,
        SchedulingPageService pageService,
        IClock clock,
        IStringLocalizer<BookingPagesController> stringLocalizer)
    {
        _typeStore = typeStore;
        _appointmentService = appointmentService;
        _pageService = pageService;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Shows an appointment type's booking page.
    /// </summary>
    /// <param name="slug">The page's address.</param>
    /// <param name="date">The day, as yyyy-MM-dd.</param>
    /// <returns>The page.</returns>
    [HttpGet("book/{slug}", Name = "SchedulingBook")]
    public async Task<IActionResult> Book(string slug, string date)
    {
        var type = await FindTypeAsync(slug);

        if (type is null)
        {
            return NotFound();
        }

        var model = new NewAppointmentViewModel { Date = date };

        await _pageService.FillSlotsAsync(model, type);

        return View(model);
    }

    /// <summary>
    /// Books from the public page.
    /// </summary>
    /// <param name="slug">The page's address.</param>
    /// <param name="model">The booking.</param>
    /// <returns>The confirmation, or the page with errors.</returns>
    [HttpPost("book/{slug}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BookPost(string slug, NewAppointmentViewModel model)
    {
        var type = await FindTypeAsync(slug);

        if (type is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(model.CustomerEmail))
        {
            model.Errors.Add(S["Your email address is required."]);
        }
        else if (DateTimeOffset.TryParse(model.Start, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var start))
        {
            var result = await _appointmentService.BookAsync(new AppointmentRequest
            {
                AppointmentTypeId = type.ItemId,
                StartUtc = start.UtcDateTime,
                CustomerName = model.CustomerName,
                CustomerEmail = model.CustomerEmail,
                CustomerPhone = model.CustomerPhone,
                Notes = model.Notes,
                Source = "SelfService",
                Actor = model.CustomerEmail,
                SelfService = true,
            });

            if (result.Succeeded)
            {
                return RedirectToRoute("SchedulingManage", new { id = result.Appointment.ItemId, token = result.Appointment.ManageToken });
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

        await _pageService.FillSlotsAsync(model, type);

        return View(nameof(Book), model);
    }

    /// <summary>
    /// Shows the customer's appointment from their manage link.
    /// </summary>
    /// <param name="id">The appointment's id.</param>
    /// <param name="token">The secret in the link.</param>
    /// <param name="date">The day to offer new times on.</param>
    /// <returns>The page.</returns>
    [HttpGet("book/manage/{id}/{token}", Name = "SchedulingManage")]
    public async Task<IActionResult> Manage(string id, string token, string date)
    {
        var appointment = await FindAppointmentAsync(id, token);

        if (appointment is null)
        {
            return NotFound();
        }

        var model = await _pageService.BuildDetailAsync(appointment, date);

        model.Token = token;
        model.CustomerCanCancel = model.AppointmentType.CustomerCanCancel(appointment, _clock.UtcNow);
        model.CustomerCanReschedule = model.AppointmentType.CustomerCanReschedule(appointment, _clock.UtcNow);

        return View(model);
    }

    /// <summary>
    /// Moves or cancels the customer's appointment.
    /// </summary>
    /// <param name="id">The appointment's id.</param>
    /// <param name="token">The secret in the link.</param>
    /// <param name="operation">Cancel or Reschedule.</param>
    /// <param name="start">The new start, for a move.</param>
    /// <returns>A redirect to the manage page.</returns>
    [HttpPost("book/manage/{id}/{token}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManagePost(string id, string token, string operation, string start)
    {
        var appointment = await FindAppointmentAsync(id, token);

        if (appointment is null)
        {
            return NotFound();
        }

        var result = operation switch
        {
            "Cancel" => await _appointmentService.CancelAsync(appointment, S["Cancelled by the customer."], appointment.CustomerEmail, selfService: true),
            "Reschedule" when DateTimeOffset.TryParse(start, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var newStart) => await _appointmentService.RescheduleAsync(appointment, newStart.UtcDateTime, actor: appointment.CustomerEmail, selfService: true),
            _ => null,
        };

        TempData["SchedulingMessage"] = result is null ? S["Choose what to do."].Value : result.Succeeded ? S["Your appointment has been updated."].Value : string.Join(" ", result.Errors);

        return RedirectToRoute("SchedulingManage", new { id, token });
    }

    private async Task<AppointmentType> FindTypeAsync(string slug)
    {
        var type = await _typeStore.FindBySlugAsync(slug?.Trim().ToLowerInvariant());

        return type is { Enabled: true, AllowSelfService: true } ? type : null;
    }

    private async Task<Appointment> FindAppointmentAsync(string id, string token)
    {
        var appointment = await _appointmentService.FindByIdAsync(id);

        return AppointmentManageToken.Matches(appointment, token) ? appointment : null;
    }
}
