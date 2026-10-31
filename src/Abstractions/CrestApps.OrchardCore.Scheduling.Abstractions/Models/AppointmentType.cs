using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Scheduling.Models;

/// <summary>
/// A kind of appointment: how long it lasts, who can take it, and the rules for booking it.
/// </summary>
public sealed class AppointmentType : CatalogItem
{
    /// <summary>
    /// Gets or sets the name customers see.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description customers see.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the address of its public booking page.
    /// </summary>
    public string Slug { get; set; }

    /// <summary>
    /// Gets or sets how long an appointment lasts.
    /// </summary>
    public int DurationMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets how far apart the offered start times are.
    /// </summary>
    public int SlotIntervalMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets how much free time the resource needs before an appointment.
    /// </summary>
    public int BufferBeforeMinutes { get; set; }

    /// <summary>
    /// Gets or sets how much free time the resource needs after an appointment.
    /// </summary>
    public int BufferAfterMinutes { get; set; }

    /// <summary>
    /// Gets or sets how soon before the start an appointment can still be booked.
    /// </summary>
    public int MinimumNoticeMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets how far ahead appointments can be booked.
    /// </summary>
    public int HorizonDays { get; set; } = 60;

    /// <summary>
    /// Gets or sets the most appointments of this type one resource takes in a day, or 0 for no limit.
    /// </summary>
    public int MaxPerDay { get; set; }

    /// <summary>
    /// Gets or sets how many customers share one appointment, for a class or group session.
    /// </summary>
    public int Capacity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the resources that take the appointment.
    /// </summary>
    public IList<string> ResourceIds { get; set; } = [];

    /// <summary>
    /// Gets or sets how resources are picked.
    /// </summary>
    public AppointmentAssignment Assignment { get; set; }

    /// <summary>
    /// Gets or sets whether a booking waits for someone to confirm it.
    /// </summary>
    public bool RequiresConfirmation { get; set; }

    /// <summary>
    /// Gets or sets how long a held time stays held.
    /// </summary>
    public int HoldMinutes { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether customers can book it themselves on the public booking page and through the AI tools.
    /// </summary>
    public bool AllowSelfService { get; set; }

    /// <summary>
    /// Gets or sets where the appointment takes place.
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Gets or sets the color of its events.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets whether it can be booked.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
