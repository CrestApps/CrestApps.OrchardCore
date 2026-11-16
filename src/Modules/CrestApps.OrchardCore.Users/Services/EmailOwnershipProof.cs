using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Records that the holder of a code e-mailed to an account's address proved they control it. Whatever could sign in to
/// the account before the first proof of its address, a password, an external login or a session, was set before
/// anyone proved the address, and may be somebody else's: whoever registered with it, or changed their email to it. It
/// is removed, and the security stamp rotated so other sessions end, in the same update that records the address as
/// proved, which is what hands over what belongs to it (the guest orders placed with it).
/// </summary>
public sealed class EmailOwnershipProof
{
    private readonly UserManager<IUser> _userManager;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public EmailOwnershipProof(
        UserManager<IUser> userManager,
        ISession session,
        IClock clock,
        ILogger<EmailOwnershipProof> logger)
    {
        _userManager = userManager;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Whether the account has a password or an external login, which the first proof of its address removes.
    /// </summary>
    /// <param name="account">The account.</param>
    public static bool HasCredentials(User account)
        => !string.IsNullOrEmpty(account?.PasswordHash) || account?.LoginInfos is { Count: > 0 };

    /// <summary>
    /// Confirms the account's address and records it as proved. Everything is saved in one update, and when that fails
    /// the request's session is reset, so nothing of it is kept: the account never loses its password without the proof
    /// being recorded and the user told.
    /// </summary>
    /// <param name="account">The account whose address a code just proved.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<EmailProofResult> RecordAsync(User account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        cancellationToken.ThrowIfCancellationRequested();

        var firstProof = !UserEmailOwnership.IsVerified(account.Properties, account.Email);
        var removed = firstProof && HasCredentials(account);

        if (firstProof)
        {
            account.PasswordHash = null;
            account.LoginInfos?.Clear();
            UserEmailOwnership.MarkVerified(account.Properties, account.Email, _clock.UtcNow);
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

        return removed ? EmailProofResult.CredentialsRemoved : EmailProofResult.Recorded;
    }
}
