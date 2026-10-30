namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// A <see cref="ICalendarEventHandler"/> whose members do nothing, to override as needed.
/// </summary>
public abstract class CalendarEventHandlerBase : ICalendarEventHandler
{
    /// <inheritdoc />
    public virtual Task ValidatingAsync(CalendarEventValidatingContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task SavingAsync(CalendarEventContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task SavedAsync(CalendarEventContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task DeletingAsync(CalendarEventContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
