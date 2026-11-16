using System.Security.Claims;

namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Asks the signed-in person for their password again before a page shows something sensitive, such as their pay, so
/// someone at a computer left signed in can't see it. A confirmation lasts <see cref="Window"/> in that browser.
/// Guard a controller action with <c>[ConfirmPassword]</c> (CrestApps.OrchardCore.Users.Core), or check
/// <see cref="IsConfirmedAsync"/> in a display driver and show a link to <see cref="GetConfirmationUrl"/>.
/// </summary>
public interface IPasswordConfirmation
{
    /// <summary>
    /// Gets how long a confirmation lasts.
    /// </summary>
    TimeSpan Window { get; }

    /// <summary>
    /// Returns whether a person confirmed their password in this browser within <see cref="Window"/>.
    /// </summary>
    /// <param name="user">The signed-in person.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when they did.</returns>
    Task<bool> IsConfirmedAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a person just confirmed their password, for <see cref="Window"/>.
    /// </summary>
    /// <param name="user">The signed-in person.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ConfirmAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the page that asks for the password and then returns to a local page.
    /// </summary>
    /// <param name="returnUrl">The local page to return to.</param>
    /// <returns>The page's path.</returns>
    string GetConfirmationUrl(string returnUrl);
}
