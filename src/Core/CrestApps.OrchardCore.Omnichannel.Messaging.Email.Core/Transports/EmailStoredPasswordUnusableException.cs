namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;

/// <summary>
/// Thrown when the stored password of a mail server was saved for other settings (another host, port, security or user
/// name, or before passwords were sealed to their account), so it was not sent and has to be typed again. The server
/// was not contacted.
/// </summary>
public sealed class EmailStoredPasswordUnusableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmailStoredPasswordUnusableException"/> class.
    /// </summary>
    /// <param name="host">The mail server's host.</param>
    public EmailStoredPasswordUnusableException(string host)
        : base($"The stored password for the mail server at '{host}' was saved for other settings, so it was not sent. Type the password again on the address.")
    {
        Host = host;
    }

    /// <summary>
    /// Gets the mail server's host.
    /// </summary>
    public string Host { get; }
}
