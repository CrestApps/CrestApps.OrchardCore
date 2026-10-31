using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// Starts a workflow when a calendar event is deleted.
/// </summary>
public sealed class CalendarEventDeletedEvent : CalendarEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventDeletedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarEventDeletedEvent(IStringLocalizer<CalendarEventDeletedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CalendarEventDeletedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Calendar Event Deleted"];
}
