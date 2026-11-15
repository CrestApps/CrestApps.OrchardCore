using CrestApps.OrchardCore.Learning.Core.Indexes;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Keeps the hashes of the secrets learners type (enrollment keys, quiz access codes) out of the content items they
/// belong to. Only the salted hash is stored; a secret is checked with <see cref="LearningSecretVerifier"/>, which also
/// limits wrong tries.
/// </summary>
public sealed class LearningSecretStore : DocumentCatalog<LearningSecret, LearningSecretIndex>
{
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningSecretStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="clock">The clock.</param>
    public LearningSecretStore(ISession session, IClock clock)
        : base(session)
    {
        CollectionName = LearningConstants.CollectionName;
        _clock = clock;
    }

    /// <summary>
    /// Finds the secret of a kind for a content item.
    /// </summary>
    /// <param name="kind">The kind of secret.</param>
    /// <param name="ownerId">The content item.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The secret, or <see langword="null"/>.</returns>
    public async Task<LearningSecret> FindAsync(string kind, string ownerId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(ownerId)
            ? null
            : await Session.Query<LearningSecret, LearningSecretIndex>(index => index.Kind == kind && index.OwnerId == ownerId, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Gets whether a content item has a secret of a kind.
    /// </summary>
    /// <param name="kind">The kind of secret.</param>
    /// <param name="ownerId">The content item.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when it has one.</returns>
    public async Task<bool> HasAsync(string kind, string ownerId, CancellationToken cancellationToken = default)
        => !string.IsNullOrEmpty((await FindAsync(kind, ownerId, cancellationToken))?.Hash);

    /// <summary>
    /// Sets a secret, keeping only its hash.
    /// </summary>
    /// <param name="kind">The kind of secret.</param>
    /// <param name="ownerId">The content item.</param>
    /// <param name="secret">The secret.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public Task SetAsync(string kind, string ownerId, string secret, CancellationToken cancellationToken = default)
        => SetHashAsync(kind, ownerId, EnrollmentKeys.Hash(secret.Trim()), cancellationToken);

    /// <summary>
    /// Sets the hash of a secret, such as one read from a recipe, or removes the secret when the hash is empty.
    /// </summary>
    /// <param name="kind">The kind of secret.</param>
    /// <param name="ownerId">The content item.</param>
    /// <param name="hash">The hash.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public async Task SetHashAsync(string kind, string ownerId, string hash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(kind);
        ArgumentException.ThrowIfNullOrEmpty(ownerId);

        var secret = await FindAsync(kind, ownerId, cancellationToken);

        if (string.IsNullOrEmpty(hash))
        {
            if (secret is not null)
            {
                await DeleteAsync(secret, cancellationToken);
            }

            return;
        }

        if (secret is null)
        {
            await CreateAsync(new LearningSecret { Kind = kind, OwnerId = ownerId, Hash = hash, UpdatedUtc = _clock.UtcNow }, cancellationToken);

            return;
        }

        secret.Hash = hash;
        secret.UpdatedUtc = _clock.UtcNow;
        await UpdateAsync(secret, cancellationToken);
    }

    /// <summary>
    /// Checks a secret against the stored hash, without limiting tries; people's input goes through
    /// <see cref="LearningSecretVerifier"/>.
    /// </summary>
    /// <param name="kind">The kind of secret.</param>
    /// <param name="ownerId">The content item.</param>
    /// <param name="secret">The secret.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    public async Task<bool> VerifyAsync(string kind, string ownerId, string secret, CancellationToken cancellationToken = default)
        => EnrollmentKeys.Verify(secret?.Trim(), (await FindAsync(kind, ownerId, cancellationToken))?.Hash);

    /// <summary>
    /// Lists every secret of a kind, for an export.
    /// </summary>
    /// <param name="kind">The kind of secret.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The secrets.</returns>
    public async Task<IReadOnlyList<LearningSecret>> ListAsync(string kind, CancellationToken cancellationToken = default)
        => (await Session.Query<LearningSecret, LearningSecretIndex>(index => index.Kind == kind, collection: CollectionName).ListAsync(cancellationToken)).ToArray();
}
