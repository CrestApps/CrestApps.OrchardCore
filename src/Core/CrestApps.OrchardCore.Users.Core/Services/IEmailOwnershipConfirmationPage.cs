using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users.Core.Services;

/// <summary>
/// The page where a signed-in user confirms, with a code e-mailed to their address, that the address is theirs, which
/// is what hands them what belongs to it (see <see cref="UserEmailOwnership"/>). The Users module's "Confirm email
/// ownership" feature provides it. A module that hands over what belongs to an address links to it when the address
/// isn't proved yet, so its users learn how to get what is waiting for them.
/// </summary>
public interface IEmailOwnershipConfirmationPage
{
    /// <summary>
    /// Returns the address of the page for <paramref name="user"/>, or <see langword="null"/> when they can't confirm
    /// there: their address is proved already, or a code can't confirm it (see the Users module's documentation).
    /// </summary>
    /// <param name="user">The signed-in user.</param>
    /// <param name="returnUrl">The local address to come back to once confirmed, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<string> GetUrlAsync(IUser user, string returnUrl = null, CancellationToken cancellationToken = default);
}
