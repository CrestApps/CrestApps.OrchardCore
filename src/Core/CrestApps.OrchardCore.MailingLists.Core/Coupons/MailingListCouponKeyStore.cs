using System.Security.Cryptography;
using OrchardCore.Documents;
using OrchardCore.Locking.Distributed;
using YesSql;

namespace CrestApps.OrchardCore.MailingLists.Core.Coupons;

/// <summary>
/// Keeps the tenant's welcome coupon key: a random 256-bit value made once (by the welcome coupon feature's migration,
/// or on first use under a distributed lock) and never a Data Protection key, which rotates.
/// </summary>
public sealed class MailingListCouponKeyStore
{
    private const string LockKey = "MailingLists:CouponKey";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly IDocumentManager<MailingListsKeysDocument> _documents;
    private readonly IDistributedLock _locks;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListCouponKeyStore"/> class.
    /// </summary>
    /// <param name="documents">The document manager of the keys document.</param>
    /// <param name="locks">The distributed lock.</param>
    /// <param name="session">The session, committed while the lock is held so a second key is never made.</param>
    public MailingListCouponKeyStore(
        IDocumentManager<MailingListsKeysDocument> documents,
        IDistributedLock locks,
        ISession session)
    {
        _documents = documents;
        _locks = locks;
        _session = session;
    }

    /// <summary>
    /// Returns the tenant's coupon key, making it when it does not exist yet.
    /// </summary>
    /// <returns>The key.</returns>
    public async Task<byte[]> GetKeyAsync()
    {
        var document = await _documents.GetOrCreateImmutableAsync();

        if (TryDecode(document.CouponKey, out var key))
        {
            return key;
        }

        return await EnsureKeyAsync(commit: true);
    }

    /// <summary>
    /// Makes the tenant's coupon key when it does not exist yet, and returns it.
    /// </summary>
    /// <param name="commit">
    /// Whether the unit of work is committed while the lock is held, so a concurrent request reads the new key; a
    /// migration passes <see langword="false"/>, since its own unit of work commits it.
    /// </param>
    /// <returns>The key.</returns>
    public async Task<byte[]> EnsureKeyAsync(bool commit)
    {
        var (locker, locked) = await _locks.TryAcquireLockAsync(LockKey, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            throw new TimeoutException("The mailing lists coupon key is being made by another request; try again.");
        }

        await using (locker)
        {
            var document = await _documents.GetOrCreateMutableAsync();

            if (TryDecode(document.CouponKey, out var key))
            {
                return key;
            }

            key = RandomNumberGenerator.GetBytes(32);
            document.CouponKey = Convert.ToBase64String(key);

            await _documents.UpdateAsync(document);

            if (commit)
            {
                await _session.SaveChangesAsync();
            }

            return key;
        }
    }

    private static bool TryDecode(string value, out byte[] key)
    {
        key = null;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        try
        {
            key = Convert.FromBase64String(value);

            return key.Length >= 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
