namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// Records that the person at an email address no longer wants email: every contact and lead that holds the address is
/// marked <c>Do not email</c>, so nothing automated and no agent sends to it again until it is cleared.
/// </summary>
public interface IEmailOptOutService
{
    /// <summary>
    /// Opts an email address out of email.
    /// </summary>
    /// <param name="address">The email address.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many contact records were marked.</returns>
    Task<int> OptOutAsync(string address, CancellationToken cancellationToken = default);
}
