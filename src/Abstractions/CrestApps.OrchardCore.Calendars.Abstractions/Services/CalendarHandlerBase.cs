namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// A <see cref="ICalendarHandler"/> whose members do nothing, to override as needed.
/// </summary>
public abstract class CalendarHandlerBase : ICalendarHandler
{
    /// <inheritdoc />
    public virtual Task ValidatingAsync(CalendarValidatingContext context)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task CreatedAsync(CalendarContext context)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task UpdatedAsync(CalendarContext context)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task DeletingAsync(CalendarContext context)
    {
        return Task.CompletedTask;
    }
}
