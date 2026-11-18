using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Records that the holder of a code e-mailed to an account's address proved they control it. Whatever could sign in to
/// the account before the first proof of its address, a password, an external login or a session, was set before
/// anyone proved the address, and may be somebody else's: whoever registered with it, or changed their email to it. So
/// the first proof keeps only the credential the same request showed (signed in with the password or through one of the
/// external logins, or the password typed with the code, see <see cref="UserCredentialsProof"/>), removes the rest,
/// rotates the security stamp and ends every earlier sign-in, in the same update that records the address as proved,
/// which is what hands over what belongs to it (the guest orders placed with it).
/// </summary>
/// <remarks>
/// Showing one credential says nothing about the others: a password can be set again with the mailbox alone ("Forgot
/// password"), and a login can be linked from a code session, while a credential somebody else added stays. So the
/// proof never keeps more than what was shown.
/// </remarks>
public sealed class EmailOwnershipProof
{
    private readonly UserManager<IUser> _userManager;
    private readonly ISession _session;
    private readonly UserCredentialsResets _credentialsResets;
    private readonly IEnumerable<IUserSignInRevocation> _revocations;
    private readonly IOptionsMonitor<RegistrationOptions> _registrationOptions;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public EmailOwnershipProof(
        UserManager<IUser> userManager,
        ISession session,
        UserCredentialsResets credentialsResets,
        IEnumerable<IUserSignInRevocation> revocations,
        IOptionsMonitor<RegistrationOptions> registrationOptions,
        IClock clock,
        ILogger<EmailOwnershipProof> logger)
    {
        _userManager = userManager;
        _session = session;
        _credentialsResets = credentialsResets;
        _revocations = revocations;
        _registrationOptions = registrationOptions;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Whether the account has a password or an external login, which the first proof of its address removes unless the
    /// request shows it.
    /// </summary>
    /// <param name="account">The account.</param>
    public static bool HasCredentials(User account)
        => UserCredentialsProof.HasCredentials(account);

    /// <summary>
    /// Whether the account's password and external logins never signed anyone in: its address is not confirmed, on a
    /// site that requires confirmed addresses, where Orchard Core refuses every sign-in to it.
    /// </summary>
    /// <param name="account">The account.</param>
    public bool CredentialsNeverSignedIn(User account)
        => account is { EmailConfirmed: false } && _registrationOptions.CurrentValue.UsersMustValidateEmail;

    /// <summary>
    /// Confirms the account's address and records it as proved, removing every credential the account had. Everything
    /// is saved in one update, and when that fails the request's session is reset, so nothing of it is kept: the account
    /// never loses its password without the proof being recorded and the user told.
    /// </summary>
    /// <param name="account">The account whose address a code just proved.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task<EmailProofResult> RecordAsync(User account, CancellationToken cancellationToken = default)
        => RecordAsync(account, passwordShown: false, shownLoginProvider: null, cancellationToken);

    /// <summary>
    /// Confirms the account's address and records it as proved, keeping only the credential the request showed.
    /// </summary>
    /// <param name="account">The account whose address a code just proved.</param>
    /// <param name="passwordShown">Whether the same request showed the account's password, so it is kept.</param>
    /// <param name="shownLoginProvider">The provider of the external login the same request is signed in through, whose
    /// login is kept, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<EmailProofResult> RecordAsync(User account, bool passwordShown, string shownLoginProvider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        cancellationToken.ThrowIfCancellationRequested();

        var firstProof = !UserEmailOwnership.IsVerified(account.Properties, account.Email);
        var now = _clock.UtcNow;
        var removed = false;

        if (firstProof)
        {
            removed = RemoveWhatWasNotShown(account, passwordShown, shownLoginProvider);
            UserCredentialsProof.ClearChangedInCodeSession(account);
            UserCredentialsResets.Record(account.Properties, now);
            UserEmailOwnership.MarkVerified(account.Properties, account.Email, now);
        }

        account.EmailConfirmed = true;

        // The new security stamp goes in the same update.
        var saved = firstProof
            ? await _userManager.UpdateSecurityStampAsync(account)
            : await _userManager.UpdateAsync(account);

        if (!saved.Succeeded)
        {
            await _session.ResetAsync();
            _logger.LogWarning("The proof of a user's email address could not be saved, so nothing of it was kept.");

            return EmailProofResult.Failed;
        }

        if (!firstProof)
        {
            return EmailProofResult.Recorded;
        }

        await EndEarlierSignInsAsync(account, now, cancellationToken);

        return removed ? EmailProofResult.CredentialsRemoved : EmailProofResult.SignInsEnded;
    }

    // Keeps the password only when it was shown, and an external login only when the request is signed in through its
    // provider and it is the account's only login with that provider: the sign-in names the provider, not which of its
    // accounts was used. The tokens a removed login stored go with it.
    private static bool RemoveWhatWasNotShown(User account, bool passwordShown, string shownLoginProvider)
    {
        var removed = false;

        if (!passwordShown && !string.IsNullOrEmpty(account.PasswordHash))
        {
            account.PasswordHash = null;
            removed = true;
        }

        if (account.LoginInfos is not { Count: > 0 } logins)
        {
            return removed;
        }

        var shown = shownLoginProvider is null
            ? []
            : logins.Where(login => string.Equals(login.LoginProvider, shownLoginProvider, StringComparison.Ordinal)).ToArray();
        var kept = shown.Length == 1 ? shown[0] : null;
        var removedProviders = new HashSet<string>(StringComparer.Ordinal);

        for (var i = logins.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(logins[i], kept))
            {
                removedProviders.Add(logins[i].LoginProvider);
                logins.RemoveAt(i);
                removed = true;
            }
        }

        if (kept is not null)
        {
            removedProviders.Remove(kept.LoginProvider);
        }

        if (account.UserTokens is { Count: > 0 } tokens)
        {
            for (var i = tokens.Count - 1; i >= 0; i--)
            {
                if (removedProviders.Contains(tokens[i].LoginProvider))
                {
                    tokens.RemoveAt(i);
                }
            }
        }

        return removed;
    }

    // The new stamp ends earlier sessions only at Identity's next stamp check, up to 30 minutes later by default: the
    // cookie validation refuses them from now on, and tokens an OpenID server issued are revoked. This runs after the
    // update is saved; a revocation commits the request's session as it goes. A failure here is logged and does not stop
    // the rest: the stamp check still ends the sessions at its next run.
    private async Task EndEarlierSignInsAsync(User account, DateTime resetUtc, CancellationToken cancellationToken)
    {
        try
        {
            await _credentialsResets.PublishAsync(account.UserId, resetUtc, account.SecurityStamp, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "The reset of a user's credentials could not be published, so their earlier sessions end at the next security stamp check.");
        }

        foreach (var revocation in _revocations)
        {
            try
            {
                await revocation.RevokeAsync(account.UserId, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "What was issued to a user could not be revoked after the proof of their email address reset their credentials.");
            }
        }
    }
}
