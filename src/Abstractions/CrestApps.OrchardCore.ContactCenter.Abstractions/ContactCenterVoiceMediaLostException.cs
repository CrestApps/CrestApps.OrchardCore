namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Thrown by <see cref="IContactCenterVoiceMediaSession.ReadIncomingAsync"/> when the provider reported the call's
/// media stream failed and it could not be brought back, while the call itself is still up.
/// </summary>
/// <remarks>
/// A media stream that simply ends means the caller has gone, and the session's reader just stops. This is the other
/// case: the provider said the stream broke and the caller is still on the line, unable to hear or be heard. Whoever
/// is holding the call has to decide what happens to them -- a person, or an apology and a hangup -- rather than
/// treating it as a caller who hung up.
/// </remarks>
public sealed class ContactCenterVoiceMediaLostException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterVoiceMediaLostException"/> class.
    /// </summary>
    public ContactCenterVoiceMediaLostException()
        : base("The call's media stream failed and could not be restored.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterVoiceMediaLostException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ContactCenterVoiceMediaLostException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterVoiceMediaLostException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public ContactCenterVoiceMediaLostException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
