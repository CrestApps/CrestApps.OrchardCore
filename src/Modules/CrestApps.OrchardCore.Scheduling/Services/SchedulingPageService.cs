using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.ViewModels;
using System.Globalization;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Scheduling.Services;

/// <summary>
/// Builds the booking and appointment pages the admin screens and the public booking pages share.
/// </summary>
public sealed class SchedulingPageService
{
    private readonly AppointmentTypeStore _typeStore;
    private readonly SchedulingResourceStore _resourceStore;
    private readonly ISchedulingAvailabilityService _availabilityService;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchedulingPageService"/> class.
    /// </summary>
    /// <param name="typeStore">The appointment type store.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="availabilityService">The availability service.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="clock">The clock.</param>
    public SchedulingPageService(
        AppointmentTypeStore typeStore,
        SchedulingResourceStore resourceStore,
        ISchedulingAvailabilityService availabilityService,
        ISiteService siteService,
        IClock clock)
    {
        _typeStore = typeStore;
        _resourceStore = resourceStore;
        _availabilityService = availabilityService;
        _siteService = siteService;
        _clock = clock;
    }

    /// <summary>
    /// Gets the site's time zone.
    /// </summary>
    /// <returns>The IANA time zone id.</returns>
    public async Task<string> GetTimeZoneIdAsync()
    {
        return CalendarTime.NormalizeZoneId((await _siteService.GetSiteSettingsAsync()).TimeZoneId) ?? "UTC";
    }

    /// <summary>
    /// Fills the open times of a day into a booking page.
    /// </summary>
    /// <param name="model">The page.</param>
    /// <param name="type">The appointment type.</param>
    /// <returns>A task.</returns>
    public async Task FillSlotsAsync(NewAppointmentViewModel model, AppointmentType type)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(type);

        model.AppointmentType = type;
        model.AppointmentTypeId = type.ItemId;
        model.TimeZoneId = await GetTimeZoneIdAsync();

        var day = ParseDay(model.Date, model.TimeZoneId);

        model.Date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var slots = await _availabilityService.FindSlotsAsync(type, CalendarTime.ToUtc(day, model.TimeZoneId), CalendarTime.ToUtc(day.AddDays(1), model.TimeZoneId));

        model.Slots = SchedulingFormat.ToChoices(slots, model.TimeZoneId);
    }

    /// <summary>
    /// Builds an appointment's page.
    /// </summary>
    /// <param name="appointment">The appointment.</param>
    /// <param name="date">The day to offer new times on, as yyyy-MM-dd.</param>
    /// <returns>The page.</returns>
    public async Task<AppointmentDetailViewModel> BuildDetailAsync(Appointment appointment, string date)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        var timeZoneId = await GetTimeZoneIdAsync();
        var type = await _typeStore.FindByIdAsync(appointment.AppointmentTypeId);
        var resources = await _resourceStore.GetAsync(appointment.ResourceIds);
        var model = new AppointmentDetailViewModel
        {
            Appointment = appointment,
            AppointmentType = type,
            ResourceNames = string.Join(", ", resources.Select(resource => resource.Name)),
            TimeZoneId = timeZoneId,
            LocalStart = CalendarTime.ToLocal(appointment.StartUtc, timeZoneId),
            LocalEnd = CalendarTime.ToLocal(appointment.EndUtc, timeZoneId),
        };

        if (type is not null && appointment.Status is AppointmentStatus.Held or AppointmentStatus.Requested or AppointmentStatus.Confirmed)
        {
            var day = ParseDay(date ?? model.LocalStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), timeZoneId);

            model.Date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            model.Slots = SchedulingFormat.ToChoices(await _availabilityService.FindSlotsAsync(type, CalendarTime.ToUtc(day, timeZoneId), CalendarTime.ToUtc(day.AddDays(1), timeZoneId), excludeAppointmentId: appointment.ItemId), timeZoneId);
        }

        return model;
    }

    private DateTime ParseDay(string value, string timeZoneId)
    {
        return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : CalendarTime.ToLocal(_clock.UtcNow, timeZoneId).Date;
    }
}
