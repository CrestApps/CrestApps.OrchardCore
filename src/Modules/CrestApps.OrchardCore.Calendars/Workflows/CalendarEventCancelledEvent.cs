using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// Starts a workflow when a calendar event, or one occurrence of a repeating event, is cancelled.
/// </summary>
public sealed class CalendarEventCancelledEvent : CalendarEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventCancelledEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarEventCancelledEvent(IStringLocalizer<CalendarEventCancelledEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CalendarEventCancelledEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Calendar Event Cancelled"];
}
