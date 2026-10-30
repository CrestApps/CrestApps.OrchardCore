using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.Options;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Reads the calendar types from <see cref="CalendarOptions"/>.
/// </summary>
public sealed class DefaultCalendarTypeManager : ICalendarTypeManager
{
    private static readonly CalendarEventTypeDescriptor _defaultEventType = new(
        CalendarsConstants.EventTypes.Default,
        LocalizationSource.Create<DefaultCalendarTypeManager>("Event"));

    private readonly CalendarOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCalendarTypeManager"/> class.
    /// </summary>
    /// <param name="options">The calendar options.</param>
    public DefaultCalendarTypeManager(IOptions<CalendarOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public IReadOnlyList<CalendarTypeDescriptor> ListTypes()
    {
        return _options.Types
            .OrderBy(type => type.Position)
            .ThenBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public CalendarTypeDescriptor FindType(string name)
    {
        return _options.FindType(name);
    }

    /// <inheritdoc />
    public IReadOnlyList<CalendarEventTypeDescriptor> ListEventTypes(string calendarType)
    {
        var type = _options.FindType(calendarType);

        if (type is null || type.EventTypes.Count == 0)
        {
            return [_defaultEventType];
        }

        return type.EventTypes;
    }

    /// <inheritdoc />
    public CalendarEventTypeDescriptor FindEventType(string calendarType, string eventType)
    {
        var eventTypes = ListEventTypes(calendarType);

        if (string.IsNullOrEmpty(eventType))
        {
            return eventTypes[0];
        }

        return eventTypes.FirstOrDefault(descriptor => string.Equals(descriptor.Name, eventType, StringComparison.OrdinalIgnoreCase));
    }
}
