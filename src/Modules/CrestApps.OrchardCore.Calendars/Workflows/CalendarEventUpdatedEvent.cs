using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// Starts a workflow when a calendar event, or one occurrence of a repeating event, changes.
/// </summary>
public sealed class CalendarEventUpdatedEvent : CalendarEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventUpdatedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarEventUpdatedEvent(IStringLocalizer<CalendarEventUpdatedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CalendarEventUpdatedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Calendar Event Updated"];
}
