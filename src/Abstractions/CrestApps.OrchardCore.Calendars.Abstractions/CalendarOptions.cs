namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// The calendar types registered by the enabled features, and limits that keep calendar queries bounded.
/// </summary>
public sealed class CalendarOptions
{
    private readonly Dictionary<string, CalendarTypeDescriptor> _types = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the registered calendar types.
    /// </summary>
    public IReadOnlyCollection<CalendarTypeDescriptor> Types => _types.Values;

    /// <summary>
    /// Gets or sets the most occurrences one query expands. Longer ranges are cut off at this many occurrences.
    /// </summary>
    public int MaxOccurrencesPerQuery { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the most events one query loads from the database.
    /// </summary>
    public int MaxEventsPerQuery { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the longest range, in days, one query may ask for.
    /// </summary>
    public int MaxQueryDays { get; set; } = 400;

    /// <summary>
    /// Gets or sets how many minutes before the start new events that take time remind their people, unless their event type
    /// says otherwise; a negative number for no reminder. All-day events have none.
    /// </summary>
    public int DefaultReminderMinutes { get; set; } = 10;

    /// <summary>
    /// Gets the calendar type with the given name, adding it when it isn't registered yet, so a feature can extend a type
    /// another feature registered.
    /// </summary>
    /// <param name="name">The technical name of the type.</param>
    /// <returns>The type's descriptor.</returns>
    public CalendarTypeDescriptor GetOrAddType(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!_types.TryGetValue(name, out var descriptor))
        {
            descriptor = new CalendarTypeDescriptor(name);
            _types[name] = descriptor;
        }

        return descriptor;
    }

    /// <summary>
    /// Finds a registered calendar type.
    /// </summary>
    /// <param name="name">The technical name of the type.</param>
    /// <returns>The type's descriptor, or <see langword="null"/> when no enabled feature registers it.</returns>
    public CalendarTypeDescriptor FindType(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return _types.GetValueOrDefault(name);
    }
}
