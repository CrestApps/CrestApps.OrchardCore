using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Core.Services;

/// <summary>
/// Tells whether an account has credentials of its own (a password, external logins, passkeys), and which of them a
/// request shows.
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
/// <see cref="EmailedCodeMethod"/>) and Orchard Core's sign-in after registration show neither. Orchard Core signs a
/// passkey in with <c>amr=pop</c>, or <c>amr=mfa</c> when the site requires user verification, and no provider name.
/// A session that says <c>amr=mfa</c> may be a passkey's, not a password's, so only <c>amr=pwd</c> shows the
/// password; and no session shows a passkey, since none says which passkey it used: the first proof removes them all.
/// </para>
/// <para>
/// Those marks say how the session began, not that the credential is still the one it began with: the cookie can be
/// older than the account's current password or logins (somebody removed the login it signed in through and linked
/// their own account of the same provider). Identity rotates the security stamp when a password is set, changed or
/// reset and when a login is removed, so a session shows a credential only while its cookie carries the account's
/// current stamp (<see cref="SecurityStampClaimType"/>). A fresh sign-in, Identity's refresh at its stamp check and
/// <c>RefreshSignInAsync</c> all write the current stamp.
/// </para>
/// </remarks>
public static class UserCredentialsProof
{
    /// <summary>
    /// The claim ASP.NET Core Identity adds to say how a session began.
    /// </summary>
    public const string AuthenticationMethodReferenceClaim = "amr";

    /// <summary>
    /// The claim ASP.NET Core Identity puts the account's security stamp in when it signs a session in
    /// (<see cref="Microsoft.AspNetCore.Identity.ClaimsIdentityOptions.SecurityStampClaimType"/>'s default, which Orchard
    /// Core keeps).
    /// </summary>
    public const string SecurityStampClaimType = "AspNet.Identity.SecurityStamp";

    /// <summary>
    /// The claim a sign-in carries when the account's address isn't proved, it has a password or an external login, and
    /// one of its credentials changed in a session an e-mailed code began (see <see cref="HasCredentialsInDoubt"/> and
    /// <see cref="ChangedInCodeSessionPropertyName"/>). The Confirm email ownership feature adds it when it builds the
    /// session's claims, so only such a session has its account read on every page.
    /// </summary>
    public const string CredentialsInDoubtClaim = "crestapps_credentials_in_doubt";

    /// <summary>
    /// The claim a session an e-mailed code began carries with <see cref="CredentialsFingerprint"/> of the account at that
    /// sign-in, so a later change of its password or external logins from that session is noticed.
    /// </summary>
    public const string CodeSessionCredentialsClaim = "crestapps_code_session_credentials";

    /// <summary>
    /// The key, in the account's properties, of when a password or an external login of the account was added, changed
    /// or removed from a session an e-mailed code began, while its address wasn't proved: the owner, who holds the
    /// mailbox, linked their own login or removed one next to credentials somebody else may hold. Every session of such an
    /// account is told on every page until the address is proved. The first proof clears it.
    /// </summary>
    public const string ChangedInCodeSessionPropertyName = "EmailOwnershipCredentialsChangedInCodeSessionUtc";

    /// <summary>
    /// The property, on the account, that says when the owner of its address signed in with an e-mailed code while its
    /// password or logins weren't proved theirs. Until the owner confirms the address, those credentials sign nobody in:
    /// they may be whoever registered with the address, who could otherwise sign straight back in.
    /// </summary>
    public const string UnprovedCredentialsHeldPropertyName = "EmailOwnershipUnprovedCredentialsHeldUtc";

    /// <summary>
    /// The <see cref="AuthenticationMethodReferenceClaim"/> value of a session a code e-mailed to the account began, when
    /// the account had a password or an external login nobody proved was the code holder's (RFC 8176's one-time code).
    /// </summary>
    public const string EmailedCodeMethod = "otp";

    /// <summary>
    /// The <see cref="AuthenticationMethodReferenceClaim"/> value ASP.NET Core Identity writes after a password sign-in.
    /// </summary>
    public const string PasswordMethod = "pwd";

    /// <summary>
    /// Whether the account can be signed in to without its mailbox: it has a password, an external login or a passkey.
    /// </summary>
    /// <param name="account">The account.</param>
    public static bool HasCredentials(User account)
        => !string.IsNullOrEmpty(account?.PasswordHash) || account?.LoginInfos is { Count: > 0 } || HasPasskeys(account);

    /// <summary>
    /// Whether the account has a passkey, which signs in like a password and which no session shows (see
    /// <see cref="HasCredentialsNotShown"/>).
    /// </summary>
    /// <param name="account">The account.</param>
    public static bool HasPasskeys(User account)
        => account?.Passkeys is { Count: > 0 };

    /// <summary>
    /// Whether <paramref name="principal"/> is signed in to <paramref name="account"/> with the account's password or
    /// through one of its own external logins.
    /// </summary>
    /// <param name="principal">The request's user.</param>
    /// <param name="account">The account.</param>
    public static bool ShowsCredentials(ClaimsPrincipal principal, User account)
        => ShowsPassword(principal, account) || ShownLoginProvider(principal, account) is not null;

    /// <summary>
    /// Whether <paramref name="principal"/> is signed in to <paramref name="account"/> with the account's password
    /// (<c>amr=pwd</c>). A session an external login or a passkey began is not, and neither is one that says only
    /// <c>amr=mfa</c>: Orchard Core writes that for a passkey when the site requires user verification, and a password
    /// with a second factor is never one a code serves (two-factor accounts are excluded).
    /// </summary>
    /// <param name="principal">The request's user.</param>
    /// <param name="account">The account.</param>
    public static bool ShowsPassword(ClaimsPrincipal principal, User account)
        => IsSignedInTo(principal, account) &&
            !string.IsNullOrEmpty(account.PasswordHash) &&
            HasCurrentSecurityStamp(principal, account) &&
            string.IsNullOrEmpty(principal.FindFirstValue(ClaimTypes.AuthenticationMethod)) &&
            principal.HasClaim(claim => claim.Type == AuthenticationMethodReferenceClaim && claim.Value == PasswordMethod);

    /// <summary>
    /// The provider of the account's own external login that <paramref name="principal"/> is signed in through, or
    /// <see langword="null"/>.
    /// </summary>
    /// <param name="principal">The request's user.</param>
    /// <param name="account">The account.</param>
    public static string ShownLoginProvider(ClaimsPrincipal principal, User account)
    {
        if (!IsSignedInTo(principal, account) || !HasCurrentSecurityStamp(principal, account))
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
    /// Whether the account's address isn't proved and it has a password, an external login or a passkey: credentials
    /// somebody else may hold, which the first proof of the address removes unless the request shows them.
    /// </summary>
    /// <param name="account">The account.</param>
    public static bool HasCredentialsInDoubt(User account)
        => HasCredentials(account) && !UserEmailOwnership.IsVerified(account.Properties, account.Email);

    /// <summary>
    /// How many credentials the account has: its password, each external login and each passkey.
    /// </summary>
    /// <param name="account">The account.</param>
    public static int CountCredentials(User account)
        => (string.IsNullOrEmpty(account?.PasswordHash) ? 0 : 1) + (account?.LoginInfos?.Count ?? 0) + (account?.Passkeys?.Count ?? 0);

    /// <summary>
    /// Whether a credential of the account changed in a session an e-mailed code began while its address wasn't proved
    /// (see <see cref="ChangedInCodeSessionPropertyName"/>).
    /// </summary>
    /// <param name="account">The account.</param>
    public static bool ChangedInCodeSession(User account)
        => account?.Properties?[ChangedInCodeSessionPropertyName] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrEmpty(text);

    /// <summary>
    /// Records that a credential of the account changed in a session an e-mailed code began.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="changedUtc">When.</param>
    public static void MarkChangedInCodeSession(User account, DateTime changedUtc)
    {
        ArgumentNullException.ThrowIfNull(account);

        account.Properties[ChangedInCodeSessionPropertyName] = changedUtc.ToString("O", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Whether the account's password, external logins and passkeys are held: the owner of its address signed in with a
    /// code while they weren't proved, and hasn't confirmed the address since (see
    /// <see cref="UnprovedCredentialsHeldPropertyName"/>).
    /// </summary>
    /// <param name="account">The account.</param>
    public static bool AreUnprovedCredentialsHeld(User account)
        => account?.Properties?[UnprovedCredentialsHeldPropertyName] is JsonValue value &&
            value.TryGetValue<string>(out var text) &&
            !string.IsNullOrEmpty(text) &&
            !UserEmailOwnership.IsVerified(account.Properties, account.Email);

    /// <summary>
    /// Holds the account's unproved credentials until the address is confirmed (see
    /// <see cref="UnprovedCredentialsHeldPropertyName"/>).
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="heldUtc">When.</param>
    public static void HoldUnprovedCredentials(User account, DateTime heldUtc)
    {
        ArgumentNullException.ThrowIfNull(account);

        account.Properties[UnprovedCredentialsHeldPropertyName] = heldUtc.ToString("O", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Clears <see cref="UnprovedCredentialsHeldPropertyName"/>, once the address is proved.
    /// </summary>
    /// <param name="account">The account.</param>
    public static void ReleaseUnprovedCredentials(User account)
        => account?.Properties?.Remove(UnprovedCredentialsHeldPropertyName);

    /// <summary>
    /// Clears <see cref="ChangedInCodeSessionPropertyName"/>, once the address is proved.
    /// </summary>
    /// <param name="account">The account.</param>
    public static void ClearChangedInCodeSession(User account)
        => account?.Properties?.Remove(ChangedInCodeSessionPropertyName);

    /// <summary>
    /// A short digest of what can sign in to the account without its mailbox: its security stamp, which Identity rotates
    /// when a password is set, changed or reset and when a login is removed, its external logins, since adding one keeps
    /// the stamp, and its passkeys' credential ids, since Identity keeps the stamp when it adds or removes one. A passkey's
    /// use (its signature counter, its name) changes nothing, and an account without passkeys has the digest it had
    /// before they were counted. Nothing of the password itself goes in.
    /// </summary>
    /// <param name="account">The account.</param>
    public static string CredentialsFingerprint(User account)
    {
        ArgumentNullException.ThrowIfNull(account);

        var logins = (account.LoginInfos ?? [])
            .Select(login => login.LoginProvider + "\n" + login.ProviderKey)
            .Order(StringComparer.Ordinal);
        var passkeys = (account.Passkeys ?? [])
            .Select(passkey => "passkey\n" + Convert.ToBase64String(passkey.CredentialId ?? []))
            .Order(StringComparer.Ordinal);
        var text = string.Join("\n\n", [account.SecurityStamp ?? string.Empty, .. logins, .. passkeys]);

        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0, 16);
    }

    /// <summary>
    /// Whether <paramref name="principal"/>'s sign-in was to an account with credentials in doubt that changed in a code
    /// session (it carries <see cref="CredentialsInDoubtClaim"/>) or began with an e-mailed code. Any other session is
    /// told at sign-in only, so nothing else needs the account read.
    /// </summary>
    /// <param name="principal">The request's user.</param>
    public static bool MayHaveCredentialsInDoubt(ClaimsPrincipal principal)
        => principal?.Identity?.IsAuthenticated == true &&
            (principal.HasClaim(claim => claim.Type == CredentialsInDoubtClaim) || SignedInWithEmailedCode(principal));

    /// <summary>
    /// Whether <paramref name="account"/> has a password, an external login or a passkey that <paramref name="principal"/>
    /// does not show, which the first proof of its address would remove: anything but the password the session shows, or
    /// the account's only login of the provider it shows. A passkey is never shown (see <see cref="UserCredentialsProof"/>).
    /// </summary>
    /// <param name="principal">The request's user.</param>
    /// <param name="account">The account.</param>
    public static bool HasCredentialsNotShown(ClaimsPrincipal principal, User account)
    {
        if (!HasCredentials(account))
        {
            return false;
        }

        if (HasPasskeys(account))
        {
            return true;
        }

        var passwordShown = ShowsPassword(principal, account);

        if (!passwordShown && !string.IsNullOrEmpty(account.PasswordHash))
        {
            return true;
        }

        var logins = account.LoginInfos ?? [];
        var provider = passwordShown ? null : ShownLoginProvider(principal, account);
        var shown = provider is not null && logins.Count(login => string.Equals(login.LoginProvider, provider, StringComparison.Ordinal)) == 1 ? 1 : 0;

        return logins.Count > shown;
    }

    /// <summary>
    /// Whether <paramref name="principal"/>'s session began with a code e-mailed to an account whose credentials nobody
    /// proved were the code holder's (see <see cref="EmailedCodeMethod"/>).
    /// </summary>
    /// <param name="principal">The request's user.</param>
    public static bool SignedInWithEmailedCode(ClaimsPrincipal principal)
        => principal?.Identity?.IsAuthenticated == true &&
            principal.HasClaim(claim => claim.Type == AuthenticationMethodReferenceClaim && claim.Value == EmailedCodeMethod);

    // The cookie was issued, or refreshed, since the account's credentials last changed.
    private static bool HasCurrentSecurityStamp(ClaimsPrincipal principal, User account)
        => !string.IsNullOrEmpty(account.SecurityStamp) &&
            string.Equals(principal.FindFirstValue(SecurityStampClaimType), account.SecurityStamp, StringComparison.Ordinal);

    private static bool IsSignedInTo(ClaimsPrincipal principal, User account)
        => principal?.Identity?.IsAuthenticated == true &&
            account is not null &&
            !string.IsNullOrEmpty(account.UserId) &&
            string.Equals(principal.FindFirstValue(ClaimTypes.NameIdentifier), account.UserId, StringComparison.Ordinal);
}
