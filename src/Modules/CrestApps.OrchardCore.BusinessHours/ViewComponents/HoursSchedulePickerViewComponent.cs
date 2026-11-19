using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.BusinessHours.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.OrchardCore.BusinessHours.ViewComponents;

/// <summary>
/// Picks a business hours schedule. Other modules invoke it by name (<c>HoursSchedulePicker</c>) from their views; it
/// lists the schedules every source offers (the Business Hours module's own and, for example, Contact Center
/// calendars), and the value is the schedule id.
/// </summary>
public sealed class HoursSchedulePickerViewComponent : ViewComponent
{
    private readonly IOpenHoursService _openHoursService;

    /// <summary>
    /// Initializes a new instance of the <see cref="HoursSchedulePickerViewComponent"/> class.
    /// </summary>
    /// <param name="openHoursService">The open-hours service.</param>
    public HoursSchedulePickerViewComponent(IOpenHoursService openHoursService)
    {
        _openHoursService = openHoursService;
    }

    /// <summary>
    /// Renders the picker.
    /// </summary>
    /// <param name="name">The input name.</param>
    /// <param name="selectedValue">The schedule id chosen now.</param>
    /// <param name="required">Whether a choice is required.</param>
    /// <param name="emptyText">The text of the empty choice, such as "Around the clock".</param>
    /// <returns>The view.</returns>
    public async Task<IViewComponentResult> InvokeAsync(
        string name,
        string selectedValue = null,
        bool required = false,
        string emptyText = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var schedules = await _openHoursService.ListSchedulesAsync(HttpContext.RequestAborted);
        var options = schedules
            .Select(schedule => new HoursSchedulePickerOption
            {
                Value = schedule.Id,
                Text = schedule.Name,
                Selected = string.Equals(schedule.Id, selectedValue, StringComparison.Ordinal),
            })
            .ToList();

        // A chosen schedule stays listed after it is disabled or deleted, so saving the form doesn't drop it silently.
        if (!string.IsNullOrEmpty(selectedValue) && !options.Any(option => option.Selected))
        {
            options.Insert(0, new HoursSchedulePickerOption
            {
                Value = selectedValue,
                Text = selectedValue,
                Selected = true,
                Missing = true,
            });
        }

        return View(new HoursSchedulePickerViewModel
        {
            Id = $"hours-schedule-picker-{name.Replace('.', '-').Replace('[', '-').Replace(']', '-')}",
            InputName = name,
            Required = required,
            EmptyText = emptyText,
            Options = options,
        });
    }
}
