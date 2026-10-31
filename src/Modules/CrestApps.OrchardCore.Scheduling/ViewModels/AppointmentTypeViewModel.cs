using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Scheduling.ViewModels;

/// <summary>
/// The editor of an appointment type.
/// </summary>
public class AppointmentTypeViewModel
{
    /// <summary>Gets or sets the id.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; }

    /// <summary>Gets or sets the booking page address.</summary>
    public string Slug { get; set; }

    /// <summary>Gets or sets the length in minutes.</summary>
    public int DurationMinutes { get; set; } = 30;

    /// <summary>Gets or sets the gap between offered times in minutes.</summary>
    public int SlotIntervalMinutes { get; set; } = 30;

    /// <summary>Gets or sets the free time needed before, in minutes.</summary>
    public int BufferBeforeMinutes { get; set; }

    /// <summary>Gets or sets the free time needed after, in minutes.</summary>
    public int BufferAfterMinutes { get; set; }

    /// <summary>Gets or sets the minimum notice in minutes.</summary>
    public int MinimumNoticeMinutes { get; set; } = 60;

    /// <summary>Gets or sets how many days ahead can be booked.</summary>
    public int HorizonDays { get; set; } = 60;

    /// <summary>Gets or sets the most per resource per day.</summary>
    public int MaxPerDay { get; set; }

    /// <summary>Gets or sets the most per resource per week.</summary>
    public int MaxPerWeek { get; set; }

    /// <summary>Gets or sets the most upcoming appointments one customer may book themselves.</summary>
    public int MaxUpcomingPerCustomer { get; set; }

    /// <summary>Gets or sets how soon before the start a customer can still cancel, in minutes.</summary>
    public int CancelNoticeMinutes { get; set; }

    /// <summary>Gets or sets how soon before the start a customer can still move it, in minutes.</summary>
    public int RescheduleNoticeMinutes { get; set; }

    /// <summary>Gets or sets how many customers share one appointment.</summary>
    public int Capacity { get; set; } = 1;

    /// <summary>Gets or sets the resources.</summary>
    public string[] ResourceIds { get; set; } = [];

    /// <summary>Gets or sets how resources are picked.</summary>
    public string Assignment { get; set; }

    /// <summary>Gets or sets whether bookings wait for confirmation.</summary>
    public bool RequiresConfirmation { get; set; }

    /// <summary>Gets or sets how long a hold lasts, in minutes.</summary>
    public int HoldMinutes { get; set; } = 10;

    /// <summary>Gets or sets whether customers can book it themselves.</summary>
    public bool AllowSelfService { get; set; }

    /// <summary>Gets or sets the location.</summary>
    public string Location { get; set; }

    /// <summary>Gets or sets the color.</summary>
    public string Color { get; set; } = "#0b7285";

    /// <summary>Gets or sets whether it can be booked.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the resources to choose from.</summary>
    [BindNever]
    public IList<SelectListItem> ResourceOptions { get; set; } = [];

    /// <summary>Gets or sets the assignment options.</summary>
    [BindNever]
    public IList<SelectListItem> AssignmentOptions { get; set; } = [];
}
