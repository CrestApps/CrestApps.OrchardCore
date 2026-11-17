using System.Security.Claims;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Core.Services;

/// <summary>
/// Tells whether an account has credentials of its own, and which of them a request shows.
/// </summary>
/// <remarks>
/// <para>
/// A round trip to the mailbox (a confirmation link or an e-mailed code) proves who reads the mail, not who set the
/// account's password or linked its external logins. Someone can register with another person's address and a password
/// of their own, and a credential can be added with the mailbox alone or from a code session, so showing one credential
/// says nothing about the others. A proof that hands over what belongs to the address (guest orders, for one) keeps only
/// the credential the same request shows: the request is signed in to that account with its password or through one of
/// its own external logins, or the password is typed with the proof.
/// </para>
/// <para>
/// ASP.NET Core Identity marks how a session began: <c>amr=pwd</c> after a password sign-in, <c>amr=mfa</c> after a
/// two-factor sign-in, and the provider's name in <see cref="ClaimTypes.AuthenticationMethod"/> after an external login
/// (with <c>amr=mfa</c> too when a second factor followed it). An e-mailed code (<c>amr=otp</c>, see
/// <see cref="EmailedCodeMethod"/>) and Orchard Core's sign-in after registration show neither.
/// </para>
/// </remarks>
public static class UserCredentialsProof
{
    /// <summary>
    /// The claim ASP.NET Core Identity adds to say how a session began.
    /// </summary>
    public const string AuthenticationMethodReferenceClaim = "amr";

    /// <summary>
    /// The <see cref="AuthenticationMethodReferenceClaim"/> value of a session a code e-mailed to the account began, when
    /// the account had a password or an external login nobody proved was the code holder's (RFC 8176's one-time code).
    /// </summary>
    public const string EmailedCodeMethod = "otp";

    /// <summary>
    /// Whether the account can be signed in to without its mailbox: it has a password or an external login.
    /// </summary>
    /// <param name="account">The account.</param>
    public static bool HasCredentials(User account)
        => !string.IsNullOrEmpty(account?.PasswordHash) || account?.LoginInfos is { Count: > 0 };

    /// <summary>
    /// Whether <paramref name="principal"/> is signed in to <paramref name="account"/> with the account's password or
    /// through one of its own external logins.
    /// </summary>
    /// <param name="principal">The request's user.</param>
    /// <param name="account">The account.</param>
    public static bool ShowsCredentials(ClaimsPrincipal principal, User account)
        => ShowsPassword(principal, account) || ShownLoginProvider(principal, account) is not null;

    /// <summary>
    /// Whether <paramref name="principal"/> is signed in to <paramref name="account"/> with the account's password. A
    /// session an external login began is not, even with <c>amr=mfa</c>.
    /// </summary>
    /// <param name="principal">The request's user.</param>
    /// <param name="account">The account.</param>
    public static bool ShowsPassword(ClaimsPrincipal principal, User account)
        => IsSignedInTo(principal, account) &&
            !string.IsNullOrEmpty(account.PasswordHash) &&
            string.IsNullOrEmpty(principal.FindFirstValue(ClaimTypes.AuthenticationMethod)) &&
            principal.HasClaim(claim => claim.Type == AuthenticationMethodReferenceClaim && (claim.Value == "pwd" || claim.Value == "mfa"));

    /// <summary>
    /// The provider of the account's own external login that <paramref name="principal"/> is signed in through, or
    /// <see langword="null"/>.
    /// </summary>
    /// <param name="principal">The request's user.</param>
    /// <param name="account">The account.</param>
    public static string ShownLoginProvider(ClaimsPrincipal principal, User account)
    {
        if (!IsSignedInTo(principal, account))
        {
            return null;
        }

        var provider = principal.FindFirstValue(ClaimTypes.AuthenticationMethod);

        return !string.IsNullOrEmpty(provider) &&
            account.LoginInfos is { Count: > 0 } logins &&
            logins.Any(login => string.Equals(login.LoginProvider, provider, StringComparison.Ordinal))
                ? provider
                : null;
    }

    /// <summary>
    /// Whether <paramref name="principal"/>'s session began with a code e-mailed to an account whose credentials nobody
    /// proved were the code holder's (see <see cref="EmailedCodeMethod"/>).
    /// </summary>
    /// <param name="principal">The request's user.</param>
    public static bool SignedInWithEmailedCode(ClaimsPrincipal principal)
        => principal?.Identity?.IsAuthenticated == true &&
            principal.HasClaim(claim => claim.Type == AuthenticationMethodReferenceClaim && claim.Value == EmailedCodeMethod);

    private static bool IsSignedInTo(ClaimsPrincipal principal, User account)
        => principal?.Identity?.IsAuthenticated == true &&
            account is not null &&
            !string.IsNullOrEmpty(account.UserId) &&
            string.Equals(principal.FindFirstValue(ClaimTypes.NameIdentifier), account.UserId, StringComparison.Ordinal);
}
