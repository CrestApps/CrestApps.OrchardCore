using System.Security.Claims;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Core.Services;

/// <summary>
/// Tells whether an account has credentials of its own, and whether a request shows them.
/// </summary>
/// <remarks>
/// <para>
/// A round trip to the mailbox (a confirmation link or an e-mailed code) proves who reads the mail, not who set the
/// account's password or linked its external logins. Someone can register with another person's address and a password
/// of their own, so a proof that hands over what belongs to the address (guest orders, for one) is kept with the
/// account's credentials only when the same request also shows those credentials: the request is signed in to that
/// account with its password or one of its own external logins, or the password is typed with the proof.
/// </para>
/// <para>
/// ASP.NET Core Identity marks how a session began: <c>amr=pwd</c> after a password (or passkey) sign-in, <c>amr=mfa</c>
/// after a two-factor sign-in, and the provider's name in <see cref="ClaimTypes.AuthenticationMethod"/> after an external
/// login. An e-mailed code and Orchard Core's sign-in after registration add neither, so neither shows the credentials.
/// </para>
/// </remarks>
public static class UserCredentialsProof
{
    /// <summary>
    /// The claim ASP.NET Core Identity adds to say how a session began.
    /// </summary>
    public const string AuthenticationMethodReferenceClaim = "amr";

    /// <summary>
    /// Whether the account can be signed in to without its mailbox: it has a password or an external login.
    /// </summary>
    /// <param name="account">The account.</param>
    public static bool HasCredentials(User account)
        => !string.IsNullOrEmpty(account?.PasswordHash) || account?.LoginInfos is { Count: > 0 };

    /// <summary>
    /// Whether <paramref name="principal"/> is signed in to <paramref name="account"/> with the account's password or
    /// one of its own external logins.
    /// </summary>
    /// <param name="principal">The request's user.</param>
    /// <param name="account">The account.</param>
    public static bool ShowsCredentials(ClaimsPrincipal principal, User account)
    {
        if (principal?.Identity?.IsAuthenticated != true ||
            account is null ||
            string.IsNullOrEmpty(account.UserId) ||
            !string.Equals(principal.FindFirstValue(ClaimTypes.NameIdentifier), account.UserId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(account.PasswordHash) &&
            principal.HasClaim(claim => claim.Type == AuthenticationMethodReferenceClaim && (claim.Value == "pwd" || claim.Value == "mfa")))
        {
            return true;
        }

        var provider = principal.FindFirstValue(ClaimTypes.AuthenticationMethod);

        return !string.IsNullOrEmpty(provider) &&
            account.LoginInfos is { Count: > 0 } logins &&
            logins.Any(login => string.Equals(login.LoginProvider, provider, StringComparison.Ordinal));
    }
}
