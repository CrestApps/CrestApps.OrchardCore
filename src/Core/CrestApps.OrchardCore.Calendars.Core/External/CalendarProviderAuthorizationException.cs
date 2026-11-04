namespace CrestApps.OrchardCore.Calendars.Core.External;

/// <summary>
/// Thrown by a provider when the external account refuses access (consent revoked, a refresh token that expired, a calendar that
/// is no longer shared). Retrying won't help, so the synchronization service pauses the subscription until someone reconnects it
/// instead of retrying.
/// </summary>
public sealed class CalendarProviderAuthorizationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarProviderAuthorizationException"/> class.
    /// </summary>
    public CalendarProviderAuthorizationException()
        : this("The external calendar refused access. Reconnect the account.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarProviderAuthorizationException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public CalendarProviderAuthorizationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarProviderAuthorizationException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The error that caused it.</param>
    public CalendarProviderAuthorizationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
