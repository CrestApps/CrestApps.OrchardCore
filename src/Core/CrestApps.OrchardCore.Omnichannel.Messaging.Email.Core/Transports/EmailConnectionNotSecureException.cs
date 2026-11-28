namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Transports;

/// <summary>
/// Thrown when a mail server that wants a sign-in did not offer an encrypted connection, so the password was not sent.
/// Its message is safe to show to whoever set up the server.
/// </summary>
public sealed class EmailConnectionNotSecureException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmailConnectionNotSecureException"/> class.
    /// </summary>
    /// <param name="host">The mail server's host.</param>
    public EmailConnectionNotSecureException(string host)
        : base($"The mail server at '{host}' did not offer an encrypted connection, so the password was not sent. Choose STARTTLS or SSL/TLS, or None to send it unencrypted.")
    {
    }
}
