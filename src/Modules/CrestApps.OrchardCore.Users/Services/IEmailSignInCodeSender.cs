namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Sends a sign-in code to the address it was issued for.
/// </summary>
public interface IEmailSignInCodeSender
{
    /// <summary>
    /// Sends the code once the current request has ended, so how long the mail server takes never shows in the response,
    /// and the page answers in the same time whether or not an account has the address.
    /// </summary>
    /// <param name="userName">The account's user name, for the log when the mail can't be sent.</param>
    /// <param name="email">The address the code was issued for.</param>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">The cancellation token of the request; the mail is sent after it, whatever it says.</param>
    Task QueueAsync(string userName, string email, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the code now.
    /// </summary>
    /// <param name="userName">The account's user name, for the log when the mail can't be sent.</param>
    /// <param name="email">The address the code was issued for.</param>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SendAsync(string userName, string email, string code, CancellationToken cancellationToken = default);
}
