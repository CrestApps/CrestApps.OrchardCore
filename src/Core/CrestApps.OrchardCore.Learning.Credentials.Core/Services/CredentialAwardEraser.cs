using CrestApps.OrchardCore.Learning.Credentials.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Services;

/// <summary>
/// Erases a person's personal data from their credential awards: each award is revoked in its status list, which keeps
/// the award's slot, and the recipient, the identity hash, the signed credential and the award reason are removed. The
/// award keeps the definition, the achievement, the dates and the slot, which identify no one.
/// </summary>
public sealed class CredentialAwardEraser
{
    /// <summary>
    /// The revocation reason an erasure records.
    /// </summary>
    public const string ErasureReason = "Personal data erased";

    private const string _lockKey = "LearningCredentials:Award";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(60);

    private readonly CredentialAwardStore _store;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialAwardEraser"/> class.
    /// </summary>
    /// <param name="store">The award store.</param>
    /// <param name="distributedLock">The distributed lock.</param>
    /// <param name="session">The session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="services">The services, for the observers.</param>
    /// <param name="logger">The logger.</param>
    public CredentialAwardEraser(
        CredentialAwardStore store,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock,
        IServiceProvider services,
        ILogger<CredentialAwardEraser> logger)
    {
        _store = store;
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// Erases a person's personal data from their awards.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of awards erased.</returns>
    public async Task<int> EraseAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return 0;
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(_lockKey, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            throw new TimeoutException("The credential awards are being changed by another request; try the erasure again.");
        }

        var revoked = new List<CredentialAward>();
        var count = 0;

        await using (locker)
        {
            var now = _clock.UtcNow;

            foreach (var award in await _store.ListForUserAsync(userId, cancellationToken))
            {
                if (!award.IsRevoked)
                {
                    award.RevokedUtc = now;
                    award.RevokedReason = ErasureReason;
                    revoked.Add(award);
                }

                award.RecipientUserId = null;
                award.Recipient = null;
                award.Credential = null;
                award.Jwt = null;
                award.ShowName = false;
                award.AwardReason = null;
                award.ErasedUtc = now;

                await _store.UpdateAsync(award, cancellationToken);
                count++;
            }

            await _session.SaveChangesAsync(cancellationToken);
        }

        foreach (var award in revoked)
        {
            foreach (var observer in _services.GetServices<Learning.Credentials.Services.ICredentialAwardObserver>())
            {
                try
                {
                    await observer.RevokedAsync(award, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.LogError(exception, "An observer of the credential award '{AwardId}' failed.", award.ItemId);
                }
            }
        }

        return count;
    }
}
