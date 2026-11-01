namespace CrestApps.OrchardCore.Calendars.Core.External;

/// <summary>
/// Thrown by an <see cref="IWritableCalendarProvider"/> when the external copy changed since it was last synchronized (an
/// <c>If-Match</c> precondition failed). The synchronization service then applies the subscription's
/// <see cref="CalendarSyncConflictPolicy"/>.
/// </summary>
public sealed class CalendarProviderConflictException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarProviderConflictException"/> class.
    /// </summary>
    public CalendarProviderConflictException()
        : this("The event changed in the external calendar since it was last synchronized.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarProviderConflictException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public CalendarProviderConflictException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarProviderConflictException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The error that caused it.</param>
    public CalendarProviderConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
