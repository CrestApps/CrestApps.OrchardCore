using System.Globalization;
using System.Net;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Scheduling.ViewModels;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Email;
using OrchardCore.Modules;
using OrchardCore.RateLimits;
using OrchardCore.ReCaptcha.Configuration;
using OrchardCore.ReCaptcha.Services;

namespace CrestApps.OrchardCore.Scheduling.Controllers;

/// <summary>
/// The public booking pages: pick a time and book, and the customer's manage link to move or cancel. Booking is
/// throttled per network (with or without Orchard Core's Rate Limits), shows reCAPTCHA when that feature is set up, and,
/// for a type that requires it, only holds the time until the customer confirms from the link e-mailed to them.
/// </summary>
[Feature(SchedulingConstants.Features.SelfService)]
[AllowAnonymous]
public sealed class BookingPagesController : Controller
{
    private readonly AppointmentTypeStore _typeStore;
    private readonly IAppointmentService _appointmentService;
    private readonly SchedulingPageService _pageService;
    private readonly IAttemptLimiter _limiter;
    private readonly SignedTokenService _tokens;
    private readonly IClock _clock;

    private const string ConfirmationPurpose = "CrestApps.Scheduling.ConfirmBooking";

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingPagesController"/> class.
    /// </summary>
    /// <param name="typeStore">The appointment type store.</param>
    /// <param name="appointmentService">The appointment service.</param>
    /// <param name="pageService">The page builder.</param>
    /// <param name="limiter">The attempt limiter.</param>
    /// <param name="tokens">The signed token service, for e-mail confirmation links.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BookingPagesController(
        AppointmentTypeStore typeStore,
        IAppointmentService appointmentService,
        SchedulingPageService pageService,
        IAttemptLimiter limiter,
        SignedTokenService tokens,
        IClock clock,
        IStringLocalizer<BookingPagesController> stringLocalizer)
    {
        _typeStore = typeStore;
        _appointmentService = appointmentService;
        _pageService = pageService;
        _limiter = limiter;
        _tokens = tokens;
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

        var model = new NewAppointmentViewModel { Date = date, ShowReCaptcha = ReCaptcha() is not null };

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
    [RateLimitGroup(SchedulingConstants.RateLimitGroups.Booking)]
    public async Task<IActionResult> BookPost(string slug, NewAppointmentViewModel model)
    {
        var type = await FindTypeAsync(slug);

        if (type is null)
        {
            return NotFound();
        }

        var reCaptcha = ReCaptcha();
        var emailService = type.RequireEmailConfirmation ? HttpContext.RequestServices.GetService<IEmailService>() : null;

        model.ShowReCaptcha = reCaptcha is not null;

        if (reCaptcha is not null && !await reCaptcha.ValidateCaptchaAsync((_, _) => { }))
        {
            model.Errors.Add(S["Confirm you're not a robot."]);
        }
        else if (string.IsNullOrWhiteSpace(model.CustomerEmail))
        {
            model.Errors.Add(S["Your email address is required."]);
        }
        else if (type.RequireEmailConfirmation && emailService is null)
        {
            // Fails closed: a booking that can't be confirmed is never taken.
            model.Errors.Add(S["Bookings can't be confirmed by email right now. Try again later."]);
        }
        else if (DateTimeOffset.TryParse(model.Start, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var start))
        {
            // Each network gets a few bookings an hour, whether or not Orchard Core's Rate Limits feature is on: every
            // booking takes locks, a slot and the booking workflows. Counted once the form and the reCAPTCHA pass, so posts
            // that fail them don't use up the network's bookings.
            var attempt = await _limiter.AcquireAsync(SchedulingConstants.AttemptPolicies.BookingNetwork, ClientNetworkKey.NetworkOnly(HttpContext), HttpContext.RequestAborted);

            if (!attempt.Allowed)
            {
                await ThrottlingServiceCollectionExtensions.WriteTooManyRequestsAsync(HttpContext, attempt.RetryAfter, HttpContext.RequestAborted);

                return new EmptyResult();
            }

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

                // A type that needs e-mail confirmation only holds the time until the customer confirms.
                Hold = type.RequireEmailConfirmation,
            });

            if (result.Succeeded && result.Appointment.Status == AppointmentStatus.Held && type.RequireEmailConfirmation)
            {
                // The manage link is only sent by e-mail: the time stays held until the customer opens it. A message that
                // can't be sent releases the time at once, rather than hold it for nobody until the hold expires.
                if (await SendConfirmationAsync(emailService, type, result.Appointment))
                {
                    return View("CheckEmail", new BookingConfirmationViewModel { AppointmentType = type, Appointment = result.Appointment });
                }

                await _appointmentService.CancelAsync(result.Appointment, "The confirmation e-mail could not be sent.", model.CustomerEmail, cancellationToken: HttpContext.RequestAborted);
                model.Errors.Add(S["We couldn't send the confirmation email, so the time wasn't booked. Try again later."]);
            }
            else if (result.Succeeded)
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

    /// <summary>
    /// Shows a held booking's confirmation, with a button that confirms it. Opening the link changes nothing: mail
    /// scanners open every link in a message.
    /// </summary>
    /// <param name="id">The appointment's id.</param>
    /// <param name="token">The signed token in the e-mailed link.</param>
    /// <returns>The page.</returns>
    [HttpGet("book/confirm/{id}/{token}", Name = "SchedulingConfirmBooking")]
    public async Task<IActionResult> Confirm(string id, string token)
    {
        var (appointment, type) = await FindHeldAsync(id, token);

        if (appointment is null)
        {
            return NotFound();
        }

        return View(new BookingConfirmationViewModel { AppointmentType = type, Appointment = appointment, Token = token });
    }

    /// <summary>
    /// Confirms a held booking from the e-mailed link.
    /// </summary>
    /// <param name="id">The appointment's id.</param>
    /// <param name="token">The signed token in the e-mailed link.</param>
    /// <returns>A redirect to the manage page.</returns>
    [HttpPost("book/confirm/{id}/{token}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPost(string id, string token)
    {
        var (appointment, _) = await FindHeldAsync(id, token);

        if (appointment is null)
        {
            return NotFound();
        }

        var result = await _appointmentService.ConfirmContactAsync(appointment, appointment.CustomerEmail, HttpContext.RequestAborted);

        TempData["SchedulingMessage"] = result.Succeeded ? S["Your booking is confirmed."].Value : string.Join(" ", result.Errors);

        return RedirectToRoute("SchedulingManage", new { id, token = appointment.ManageToken });
    }

    private async Task<(Appointment Appointment, AppointmentType Type)> FindHeldAsync(string id, string token)
    {
        if (string.IsNullOrEmpty(id) || _tokens.Unprotect(ConfirmationPurpose, token) != id)
        {
            return (null, null);
        }

        var appointment = await _appointmentService.FindByIdAsync(id);

        if (appointment is not { Status: AppointmentStatus.Held })
        {
            return (null, null);
        }

        var type = await _typeStore.FindByIdAsync(appointment.AppointmentTypeId, HttpContext.RequestAborted);

        return type is null ? (null, null) : (appointment, type);
    }

    private async Task<bool> SendConfirmationAsync(IEmailService emailService, AppointmentType type, Appointment appointment)
    {
        var lifetime = TimeSpan.FromMinutes(Math.Max(1, type.HoldMinutes));
        var link = Url.RouteUrl("SchedulingConfirmBooking", new { id = appointment.ItemId, token = _tokens.Protect(ConfirmationPurpose, appointment.ItemId, lifetime) }, Request.Scheme);
        var name = WebUtility.HtmlEncode(type.Name);

        var sent = await emailService.SendAsync(new MailMessage
        {
            To = appointment.CustomerEmail,
            Subject = S["Confirm your booking: {0}", type.Name],
            HtmlBody = $"<p>{WebUtility.HtmlEncode(S["Confirm your booking of {0} within {1} minutes, or the time is released.", type.Name, Math.Max(1, type.HoldMinutes)])}</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">{name}</a></p>",
        }, cancellationToken: HttpContext.RequestAborted);

        return sent?.Succeeded == true;
    }

    // The reCAPTCHA feature's service, when the feature is on and set up.
    private ReCaptchaService ReCaptcha()
    {
        var service = HttpContext.RequestServices.GetService<ReCaptchaService>();
        var settings = HttpContext.RequestServices.GetService<IOptionsMonitor<ReCaptchaSettings>>()?.CurrentValue;

        return service is not null && settings?.ConfigurationExists() == true ? service : null;
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
