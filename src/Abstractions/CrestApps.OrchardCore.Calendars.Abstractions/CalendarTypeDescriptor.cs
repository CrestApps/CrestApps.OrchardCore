using CrestApps.OrchardCore.Calendars.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Describes a kind of calendar: who owns it, what its events can do and which event types it holds. Features register
/// calendar types with <see cref="CalendarServiceCollectionExtensions.AddCalendarType"/>; the calendar services read the
/// descriptor instead of switching on a type name.
/// </summary>
public sealed class CalendarTypeDescriptor
{
    private readonly List<CalendarEventTypeDescriptor> _eventTypes = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarTypeDescriptor"/> class.
    /// </summary>
    /// <param name="name">The technical name of the type, stored on each calendar.</param>
    public CalendarTypeDescriptor(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        DisplayName = new LocalizationSource(name);
    }

    /// <summary>
    /// Gets the technical name of the type.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the name shown for the type.
    /// </summary>
    public LocalizationSource DisplayName { get; set; }

    /// <summary>
    /// Gets or sets what calendars of the type are for.
    /// </summary>
    public LocalizationSource Description { get; set; }

    /// <summary>
    /// Gets or sets the Font Awesome classes of the type's icon.
    /// </summary>
    public string Icon { get; set; } = "fa-solid fa-calendar";

    /// <summary>
    /// Gets or sets the color new calendars of the type get.
    /// </summary>
    public string DefaultColor { get; set; } = "#1971c2";

    /// <summary>
    /// Gets or sets who calendars of the type belong to.
    /// </summary>
    public CalendarOwnerKind OwnerKind { get; set; }

    /// <summary>
    /// Gets or sets what calendars of the type support.
    /// </summary>
    public CalendarCapabilities Capabilities { get; set; } = CalendarCapabilities.All;

    /// <summary>
    /// Gets or sets the visibility new calendars of the type get.
    /// </summary>
    public CalendarVisibility DefaultVisibility { get; set; } = CalendarVisibility.Private;

    /// <summary>
    /// Gets or sets how new calendars of the type treat overlapping events.
    /// </summary>
    public CalendarConflictPolicy DefaultConflictPolicy { get; set; }

    /// <summary>
    /// Gets or sets how many busy events may overlap on new calendars of the type that limit them.
    /// </summary>
    public int DefaultCapacity { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether people may create calendars of the type in the admin. A module that creates its calendars itself
    /// turns this off.
    /// </summary>
    public bool Creatable { get; set; } = true;

    /// <summary>
    /// Gets or sets whether a user may own only one calendar of the type, such as a personal calendar.
    /// </summary>
    public bool OnePerOwner { get; set; }

    /// <summary>
    /// Gets or sets the order the type is listed in.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets the event types calendars of the type hold. A type without any gets a single default event type.
    /// </summary>
    public IReadOnlyList<CalendarEventTypeDescriptor> EventTypes => _eventTypes;

    /// <summary>
    /// Adds an event type, replacing one with the same name.
    /// </summary>
    /// <param name="eventType">The event type.</param>
    public void AddEventType(CalendarEventTypeDescriptor eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        _eventTypes.RemoveAll(existing => string.Equals(existing.Name, eventType.Name, StringComparison.OrdinalIgnoreCase));
        _eventTypes.Add(eventType);
    }

    /// <summary>
    /// Finds one of the type's event types by name.
    /// </summary>
    /// <param name="name">The event type's name.</param>
    /// <returns>The event type, or <see langword="null"/> when the type has none by that name.</returns>
    public CalendarEventTypeDescriptor FindEventType(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return _eventTypes.FirstOrDefault(eventType => string.Equals(eventType.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets whether the type supports a capability.
    /// </summary>
    /// <param name="capability">The capability.</param>
    /// <returns><see langword="true"/> when calendars of the type support it.</returns>
    public bool Supports(CalendarCapabilities capability)
    {
        return (Capabilities & capability) == capability;
    }
}
