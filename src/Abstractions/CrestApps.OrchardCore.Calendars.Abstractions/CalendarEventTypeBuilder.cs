using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Configures a <see cref="CalendarEventTypeDescriptor"/> fluently.
/// </summary>
public sealed class CalendarEventTypeBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventTypeBuilder"/> class.
    /// </summary>
    /// <param name="descriptor">The descriptor to configure.</param>
    public CalendarEventTypeBuilder(CalendarEventTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        Descriptor = descriptor;
    }

    /// <summary>
    /// Gets the descriptor being configured.
    /// </summary>
    public CalendarEventTypeDescriptor Descriptor { get; }

    /// <summary>
    /// Sets the color events of the type are drawn in.
    /// </summary>
    /// <param name="color">The CSS color.</param>
    /// <returns>The builder.</returns>
    public CalendarEventTypeBuilder Color(string color)
    {
        Descriptor.Color = color;

        return this;
    }

    /// <summary>
    /// Sets the Font Awesome classes of the event type's icon.
    /// </summary>
    /// <param name="icon">The icon classes.</param>
    /// <returns>The builder.</returns>
    public CalendarEventTypeBuilder Icon(string icon)
    {
        Descriptor.Icon = icon;

        return this;
    }

    /// <summary>
    /// Sets how many minutes before the start new events of the type remind their people.
    /// </summary>
    /// <param name="minutes">The minutes, one of <see cref="CalendarReminders.Choices"/>; a negative number for no reminder.</param>
    /// <returns>The builder.</returns>
    public CalendarEventTypeBuilder RemindBefore(int minutes)
    {
        Descriptor.DefaultReminderMinutes = minutes;

        return this;
    }

    /// <summary>
    /// Makes new events of the type take time.
    /// </summary>
    /// <returns>The builder.</returns>
    public CalendarEventTypeBuilder Busy()
    {
        Descriptor.DefaultTransparency = CalendarTransparency.Busy;

        return this;
    }

    /// <summary>
    /// Makes new events of the type leave the time free, and keeps them out of the calendar's capacity.
    /// </summary>
    /// <returns>The builder.</returns>
    public CalendarEventTypeBuilder Free()
    {
        Descriptor.DefaultTransparency = CalendarTransparency.Free;
        Descriptor.CountsTowardCapacity = false;

        return this;
    }

    /// <summary>
    /// Makes new events of the type last whole days.
    /// </summary>
    /// <returns>The builder.</returns>
    public CalendarEventTypeBuilder AllDayByDefault()
    {
        Descriptor.AllDayByDefault = true;

        return this;
    }

    /// <summary>
    /// Sets how long new events of the type last.
    /// </summary>
    /// <param name="minutes">The length in minutes.</param>
    /// <returns>The builder.</returns>
    public CalendarEventTypeBuilder Lasts(int minutes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minutes, 1);

        Descriptor.DefaultDurationMinutes = minutes;

        return this;
    }

    /// <summary>
    /// Makes new events of the type wait for approval.
    /// </summary>
    /// <returns>The builder.</returns>
    public CalendarEventTypeBuilder NeedsApproval()
    {
        Descriptor.RequiresApproval = true;

        return this;
    }

    /// <summary>
    /// Makes events of the type name at least one attendee.
    /// </summary>
    /// <returns>The builder.</returns>
    public CalendarEventTypeBuilder RequiresAttendee()
    {
        Descriptor.RequiresAttendee = true;

        return this;
    }

    /// <summary>
    /// Keeps busy events of the type out of the calendar's capacity.
    /// </summary>
    /// <returns>The builder.</returns>
    public CalendarEventTypeBuilder IgnoresCapacity()
    {
        Descriptor.CountsTowardCapacity = false;

        return this;
    }
}
