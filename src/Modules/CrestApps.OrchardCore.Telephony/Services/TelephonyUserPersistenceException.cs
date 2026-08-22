namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Thrown when telephony token changes cannot be persisted to the current user's account. The message
/// carries only redacted identity error codes; the full identity errors are written to the log.
/// </summary>
internal sealed class TelephonyUserPersistenceException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyUserPersistenceException"/> class.
    /// </summary>
    /// <param name="message">A redacted message describing why persistence failed.</param>
    public TelephonyUserPersistenceException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyUserPersistenceException"/> class.
    /// </summary>
    /// <param name="message">A redacted message describing why persistence failed.</param>
    /// <param name="innerException">The underlying persistence failure.</param>
    public TelephonyUserPersistenceException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
