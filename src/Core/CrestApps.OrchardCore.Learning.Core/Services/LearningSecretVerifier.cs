using CrestApps.OrchardCore.Core.Security;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// The answer to a secret a person typed.
/// </summary>
public enum SecretCheck
{
    /// <summary>The secret is right, or the item has none.</summary>
    Accepted,

    /// <summary>The secret is wrong.</summary>
    Rejected,

    /// <summary>Too many wrong tries; the person waits before trying again.</summary>
    LockedOut,
}

/// <summary>
/// Checks a secret a person typed (an enrollment key, a quiz access code) with the limit on wrong tries, so every
/// module that asks for a secret gets the same protection.
/// </summary>
public sealed class LearningSecretVerifier
{
    private readonly LearningSecretStore _store;
    private readonly SecretAttemptLimiter _attempts;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningSecretVerifier"/> class.
    /// </summary>
    /// <param name="store">The secrets.</param>
    /// <param name="attempts">The limit on wrong tries.</param>
    public LearningSecretVerifier(LearningSecretStore store, SecretAttemptLimiter attempts)
    {
        _store = store;
        _attempts = attempts;
    }

    /// <summary>
    /// Gets whether a content item has a secret of a kind.
    /// </summary>
    /// <param name="kind">The kind of secret.</param>
    /// <param name="ownerId">The content item.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when it has one.</returns>
    public Task<bool> HasSecretAsync(string kind, string ownerId, CancellationToken cancellationToken = default)
        => _store.HasAsync(kind, ownerId, cancellationToken);

    /// <summary>
    /// Checks a secret. An item without a secret of the kind accepts anything; a locked-out person is refused before
    /// the secret is checked.
    /// </summary>
    /// <param name="kind">The kind of secret.</param>
    /// <param name="ownerId">The content item.</param>
    /// <param name="userId">The person.</param>
    /// <param name="secret">What the person typed.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The answer.</returns>
    public async Task<SecretCheck> CheckAsync(string kind, string ownerId, string userId, string secret, CancellationToken cancellationToken = default)
    {
        var stored = await _store.FindAsync(kind, ownerId, cancellationToken);

        if (string.IsNullOrEmpty(stored?.Hash))
        {
            return SecretCheck.Accepted;
        }

        if (await _attempts.IsLockedOutAsync(AttemptScope(kind), ownerId, userId, cancellationToken))
        {
            return SecretCheck.LockedOut;
        }

        if (!EnrollmentKeys.Verify(secret?.Trim(), stored.Hash))
        {
            await _attempts.RecordFailureAsync(AttemptScope(kind), ownerId, userId, cancellationToken);

            return SecretCheck.Rejected;
        }

        await _attempts.ResetAsync(AttemptScope(kind), ownerId, userId, cancellationToken);

        return SecretCheck.Accepted;
    }

    private static string AttemptScope(string kind)
        => "Learning:" + kind;
}
