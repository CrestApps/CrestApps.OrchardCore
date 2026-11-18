using System.Text.Json;
using CrestApps.OrchardCore.Core.Services;
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
/// the account before the first proof of its address, a password, an external login, a passkey or a session, was set
/// before anyone proved the address, and may be somebody else's: whoever registered with it, or changed their email to
/// it. So the first proof keeps only the credential the same request showed (the password typed with the code, or the
/// external login signed in through again, see <see cref="UserCredentialsProof"/>; never a passkey), removes the rest,
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
    /// <summary>
    /// The step a right code establishes on the code page for an account without a password that signs in only through
    /// external logins or passkeys, whose address isn't proved: the owner still chooses to remove them (K4).
    /// </summary>
    public const string TakeBackStep = "take-back";

    /// <summary>
    /// The step a fresh sign-in through one of the account's external logins establishes on the confirmation page: that
    /// login is kept (K5).
    /// </summary>
    public const string KeptLoginStep = "kept-login";

    private const string StepPurpose = "CrestApps.Users.EmailOwnershipProof.Step";

    private static readonly TimeSpan _stepLifetime = TimeSpan.FromMinutes(15);

    private readonly UserManager<IUser> _userManager;
    private readonly ISession _session;
    private readonly UserCredentialsResets _credentialsResets;
    private readonly IEnumerable<IUserSignInRevocation> _revocations;
    private readonly IOptionsMonitor<RegistrationOptions> _registrationOptions;
    private readonly IClock _clock;
    private readonly SignedTokenService _signedTokens;
    private readonly ILogger _logger;

    public EmailOwnershipProof(
        UserManager<IUser> userManager,
        ISession session,
        UserCredentialsResets credentialsResets,
        IEnumerable<IUserSignInRevocation> revocations,
        IOptionsMonitor<RegistrationOptions> registrationOptions,
        IClock clock,
        SignedTokenService signedTokens,
        ILogger<EmailOwnershipProof> logger)
    {
        _userManager = userManager;
        _session = session;
        _credentialsResets = credentialsResets;
        _revocations = revocations;
        _registrationOptions = registrationOptions;
        _clock = clock;
        _signedTokens = signedTokens;
        _logger = logger;
    }

    /// <summary>
    /// Protects what one step of a proof established for the next, posted back in a form: that a right code reached an
    /// account (<see cref="TakeBackStep"/>), or that the user just signed in again through one of the account's external
    /// logins (<see cref="KeptLoginStep"/>). It names the account, its security stamp and, for a login, its provider and
    /// key, and works for 15 minutes; a change of the account's credentials, which rotates the stamp, ends it sooner.
    /// </summary>
    /// <param name="step">What the step established: <see cref="TakeBackStep"/> or <see cref="KeptLoginStep"/>.</param>
    /// <param name="account">The account.</param>
    /// <param name="login">The login signed in through again, for <see cref="KeptLoginStep"/>.</param>
    public string ProtectStep(string step, User account, UserLoginInfo login = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(step);
        ArgumentNullException.ThrowIfNull(account);

        var payload = JsonSerializer.Serialize(new ProofStep(step, account.UserId, account.SecurityStamp, (_clock.UtcNow + _stepLifetime).Ticks, login?.LoginProvider, login?.ProviderKey));

        return _signedTokens.Protect(StepPurpose, payload, _stepLifetime);
    }

    /// <summary>
    /// The id of the account a protected step is for, when it is a <paramref name="step"/> that is still within its
    /// lifetime; otherwise <see langword="null"/>. Check it against the account with <see cref="IsStepFor"/>.
    /// </summary>
    /// <param name="step">The step expected.</param>
    /// <param name="protectedStep">The protected step, as posted.</param>
    public string ReadStepUserId(string step, string protectedStep)
        => Read(step, protectedStep)?.UserId;

    /// <summary>
    /// Whether <paramref name="protectedStep"/> is a <paramref name="step"/> for <paramref name="account"/> as it is now:
    /// within its lifetime, with the account's current security stamp.
    /// </summary>
    /// <param name="step">The step expected.</param>
    /// <param name="protectedStep">The protected step, as posted.</param>
    /// <param name="account">The account.</param>
    public bool IsStepFor(string step, string protectedStep, User account)
        => Read(step, protectedStep) is { } read && Matches(read, account);

    /// <summary>
    /// The external login a <see cref="KeptLoginStep"/> names, when it is for <paramref name="account"/> as it is now and
    /// the account still has that login; otherwise <see langword="null"/>.
    /// </summary>
    /// <param name="protectedStep">The protected step, as posted.</param>
    /// <param name="account">The account.</param>
    public UserLoginInfo ReadKeptLogin(string protectedStep, User account)
        => Read(KeptLoginStep, protectedStep) is { } read && Matches(read, account)
            ? account.LoginInfos?.FirstOrDefault(login =>
                string.Equals(login.LoginProvider, read.LoginProvider, StringComparison.Ordinal) &&
                string.Equals(login.ProviderKey, read.ProviderKey, StringComparison.Ordinal))
            : null;

    private ProofStep Read(string step, string protectedStep)
    {
        if (string.IsNullOrEmpty(protectedStep))
        {
            return null;
        }

        try
        {
            if (_signedTokens.Unprotect(StepPurpose, protectedStep) is not { } payload)
            {
                return null;
            }

            var read = JsonSerializer.Deserialize<ProofStep>(payload);

            return read is not null && string.Equals(read.Step, step, StringComparison.Ordinal) && read.ExpiresTicks > _clock.UtcNow.Ticks
                ? read
                : null;
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            return null;
        }
    }

    private static bool Matches(ProofStep read, User account)
        => account is not null &&
            !string.IsNullOrEmpty(account.SecurityStamp) &&
            string.Equals(read.UserId, account.UserId, StringComparison.Ordinal) &&
            string.Equals(read.SecurityStamp, account.SecurityStamp, StringComparison.Ordinal);

    /// <summary>
    /// Whether the account has a password, an external login or a passkey, which the first proof of its address removes
    /// unless the request shows it.
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
        => RecordAsync(account, passwordShown: false, shownLogin: null, cancellationToken);

    /// <summary>
    /// Confirms the account's address and records it as proved, keeping only the credential the request showed.
    /// </summary>
    /// <param name="account">The account whose address a code just proved.</param>
    /// <param name="passwordShown">Whether the same request showed the account's password, so it is kept.</param>
    /// <param name="shownLogin">The external login the same flow signed in through again, freshly, at its provider (see
    /// <see cref="ProtectStep"/>), which is kept when the account still has it; or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<EmailProofResult> RecordAsync(User account, bool passwordShown, UserLoginInfo shownLogin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        cancellationToken.ThrowIfCancellationRequested();

        var firstProof = !UserEmailOwnership.IsVerified(account.Properties, account.Email);
        var now = _clock.UtcNow;
        var removed = false;

        if (firstProof)
        {
            removed = RemoveWhatWasNotShown(account, passwordShown, shownLogin);
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

    // Keeps the password only when it was shown, and an external login only when the flow signed in through it again at
    // its provider, matched by provider and key: a session's cookie names the provider, not which of its accounts was
    // used, and can be older than the account's logins (G1, K5). The tokens a removed login stored go with it.
    // Passkeys always go: a session never says which passkey it used, nothing on the confirmation page asks for one, and
    // one somebody else registered before the proof would otherwise sign them in for good. The owner adds theirs again.
    private static bool RemoveWhatWasNotShown(User account, bool passwordShown, UserLoginInfo shownLogin)
    {
        var removed = false;

        if (!passwordShown && !string.IsNullOrEmpty(account.PasswordHash))
        {
            account.PasswordHash = null;
            removed = true;
        }

        if (account.Passkeys is { Count: > 0 } passkeys)
        {
            passkeys.Clear();
            removed = true;
        }

        if (account.LoginInfos is not { Count: > 0 } logins)
        {
            return removed;
        }

        var kept = shownLogin is null
            ? null
            : logins.FirstOrDefault(login =>
                string.Equals(login.LoginProvider, shownLogin.LoginProvider, StringComparison.Ordinal) &&
                string.Equals(login.ProviderKey, shownLogin.ProviderKey, StringComparison.Ordinal));
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

    private sealed record ProofStep(string Step, string UserId, string SecurityStamp, long ExpiresTicks, string LoginProvider, string ProviderKey);
}
