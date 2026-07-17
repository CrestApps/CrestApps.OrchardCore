using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Asterisk.Models;
using Microsoft.Extensions.Caching.Distributed;
using OrchardCore;
using OrchardCore.Environment.Cache;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskPjsipCredentialIssuer : IAsteriskPjsipCredentialIssuer
{
    private const int PasswordByteLength = 32;
    private const int CleanupBatchSize = 200;
    private const string CachePrefix = "CrestApps:Asterisk:PjsipCredentials";
    private const string CacheTagPrefix = "CrestApps.Asterisk.PjsipCredentials";

    /// <summary>
    /// The maximum number of concurrent live browser credentials a single authenticated user may hold.
    /// When a new credential would exceed this cap, the oldest live credential is revoked first so the
    /// newest session wins. This bounds how many server-owned SIP endpoints one agent can materialize.
    /// The cap is enforced against the durable lease store, so it survives distributed-cache eviction.
    /// </summary>
    private const int MaxLiveCredentialsPerUser = 3;

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly IDistributedCache _cache;
    private readonly ITagCache _tagCache;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ShellSettings _shellSettings;
    private readonly IAsteriskPjsipRealtimeCredentialStore _realtimeStore;
    private readonly IAsteriskPjsipCredentialLeaseStore _leaseStore;
    private readonly IAsteriskPjsipDialogTerminator _dialogTerminator;

    public AsteriskPjsipCredentialIssuer(
        IDistributedCache cache,
        ITagCache tagCache,
        IDistributedLock distributedLock,
        IClock clock,
        ShellSettings shellSettings,
        IAsteriskPjsipRealtimeCredentialStore realtimeStore,
        IAsteriskPjsipCredentialLeaseStore leaseStore,
        IAsteriskPjsipDialogTerminator dialogTerminator)
    {
        _cache = cache;
        _tagCache = tagCache;
        _distributedLock = distributedLock;
        _clock = clock;
        _shellSettings = shellSettings;
        _realtimeStore = realtimeStore;
        _leaseStore = leaseStore;
        _dialogTerminator = dialogTerminator;
    }

    public async Task<AsteriskPjsipCredential> IssueAsync(
        AsteriskPjsipCredentialIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateRequest(request);

        await using var locker = await AcquireLockAsync();
        var credential = await IssueCoreAsync(request, cancellationToken);
        await SignalTenantAsync();

        return credential;
    }

    public async Task<AsteriskPjsipCredential> RotateAsync(
        AsteriskPjsipCredentialIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateRequest(request);

        await using var locker = await AcquireLockAsync();

        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            await RevokeSessionCoreAsync(request.SessionId.Trim(), "rotated", cancellationToken);
        }

        var credential = await IssueCoreAsync(request, cancellationToken);
        await SignalTenantAsync();

        return credential;
    }

    public async Task<bool> RevokeAsync(
        string authorizationUser,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authorizationUser))
        {
            return false;
        }

        var normalizedUser = authorizationUser.Trim();

        await using var locker = await AcquireLockAsync();
        var revoked = await RevokeCoreAsync(normalizedUser, reason, cancellationToken);

        if (revoked)
        {
            await SignalTenantAsync();
        }

        return revoked;
    }

    public async Task<int> RevokeUserAsync(
        string userId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return 0;
        }

        var normalizedUserId = userId.Trim();
        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "signed_out" : reason.Trim();

        await using var locker = await AcquireLockAsync();

        // The durable lease store is the authority for what a user owns, so revocation survives cache
        // eviction: even if the volatile record was evicted, the lease is still discoverable and torn down.
        var leases = await _leaseStore.ListByUserAsync(normalizedUserId, cancellationToken);
        var revoked = 0;

        foreach (var lease in leases)
        {
            if (await RevokeCoreAsync(lease.AuthorizationUser, normalizedReason, cancellationToken))
            {
                revoked++;
            }
        }

        if (revoked > 0)
        {
            await SignalTenantAsync();
        }

        return revoked;
    }

    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(CreateLockKey(), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return 0;
        }

        await using var acquiredLock = locker;
        var removed = await CleanupCoreAsync(cancellationToken);

        if (removed > 0)
        {
            await SignalTenantAsync();
        }

        return removed;
    }

    private async Task<AsteriskPjsipCredential> IssueCoreAsync(
        AsteriskPjsipCredentialIssueRequest request,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var expiresAtUtc = now.Add(request.CredentialLifetime);
        var tenantName = GetTenantName();
        var userId = request.UserId.Trim();
        var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? IdGenerator.GenerateId()
            : request.SessionId.Trim();
        var interactionId = string.IsNullOrWhiteSpace(request.InteractionId)
            ? null
            : request.InteractionId.Trim();
        var authorizationUser = CreateAuthorizationUser(tenantName);
        var password = CreatePassword();
        var endpointName = authorizationUser;
        var sipUri = $"sip:{authorizationUser}@{request.SipDomain.Trim()}";

        await EnforceUserCredentialCapAsync(userId, now, cancellationToken);

        var lease = new AsteriskPjsipCredentialLease
        {
            AuthorizationUser = authorizationUser,
            TenantName = tenantName,
            UserId = userId,
            SessionId = sessionId,
            InteractionId = interactionId,
            IssuedUtc = now,
            ExpiresUtc = expiresAtUtc,
        };

        // Lease-first ordering: persist the durable lease BEFORE provisioning the realtime SIP row and
        // before caching. A realtime row therefore can never exist without a durable lease that the
        // current tenant owns, so cleanup can always reconcile it by exact authorization user. If the
        // realtime write fails, the lease is marked revoked so cleanup tears down any partial row.
        await _leaseStore.CreateAsync(lease, cancellationToken);

        try
        {
            await _realtimeStore.UpsertAsync(new AsteriskPjsipRealtimeCredential
            {
                TenantName = tenantName,
                SessionId = sessionId,
                EndpointName = endpointName,
                AuthorizationUser = authorizationUser,
                Password = password,
                DisplayName = request.DisplayName,
                ExpiresAtUtc = expiresAtUtc,
                ContactExpiration = request.ContactExpiration,
                Codecs = request.Codecs,
            }, cancellationToken);
        }
        catch
        {
            // Provisioning failed after the lease was durably committed. Mark the lease revoked and commit
            // that durably in its own isolated transaction (it must survive even though we rethrow and the
            // ambient scope rolls back), then best-effort tear down any partial realtime row so nothing is
            // left live. Cleanup will reconcile the revoked lease regardless.
            lease.RevokedUtc = now;
            lease.RevocationReason = "provision_failed";
            await _leaseStore.UpdateAsync(lease, cancellationToken);

            await TryTeardownRealtimeAsync(authorizationUser, "provision_failed", cancellationToken);

            throw;
        }

        // The cache is a pure performance cache; the durable lease is the authority. A cache write failure
        // must never fail issuance or leave durable state inconsistent, so it is best-effort only.
        await TryWriteCacheAsync(ToRecord(lease), cancellationToken);

        return new AsteriskPjsipCredential
        {
            TenantName = tenantName,
            SessionId = sessionId,
            EndpointName = endpointName,
            AuthorizationUser = authorizationUser,
            Password = password,
            SipUri = sipUri,
            ExpiresAtUtc = expiresAtUtc,
        };
    }

    private async Task EnforceUserCredentialCapAsync(
        string userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // The cap is measured against the durable lease store rather than the cache so it cannot be
        // defeated by cache eviction.
        var live = await _leaseStore.ListLiveByUserAsync(userId, now, cancellationToken);

        if (live.Count < MaxLiveCredentialsPerUser)
        {
            return;
        }

        var overflow = live.Count - MaxLiveCredentialsPerUser + 1;

        foreach (var stale in live.OrderBy(lease => lease.IssuedUtc).Take(overflow))
        {
            await RevokeCoreAsync(stale.AuthorizationUser, "session_cap_exceeded", cancellationToken);
        }
    }

    private async Task<bool> RevokeCoreAsync(
        string authorizationUser,
        string reason,
        CancellationToken cancellationToken)
    {
        // Read-through the performance cache first to fail closed quickly for unknown or cross-tenant
        // authorization users before touching the durable store. The read is resilient: any cache outage
        // is treated as a miss and reconciled against the durable lease, so revocation never depends on
        // cache availability. A cache miss is never treated as expiry.
        var record = await ReadRecordAsync(authorizationUser, cancellationToken);

        if (record is not null && !string.Equals(record.TenantName, GetTenantName(), StringComparison.Ordinal))
        {
            return false;
        }

        var lease = await _leaseStore.GetByAuthorizationUserAsync(authorizationUser, cancellationToken);

        if (lease is null)
        {
            await TryRemoveCacheAsync(authorizationUser, cancellationToken);

            return false;
        }

        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "revoked" : reason.Trim();

        await _dialogTerminator.TerminateAsync(authorizationUser, lease.RevocationReason ?? normalizedReason, cancellationToken);
        await _realtimeStore.DeleteAsync(authorizationUser, cancellationToken);
        await _leaseStore.DeleteAsync(lease, cancellationToken);
        await TryRemoveCacheAsync(authorizationUser, cancellationToken);

        return true;
    }

    private async Task RevokeSessionCoreAsync(
        string sessionId,
        string reason,
        CancellationToken cancellationToken)
    {
        var leases = await _leaseStore.ListLiveBySessionAsync(sessionId, _clock.UtcNow, cancellationToken);

        foreach (var lease in leases)
        {
            await RevokeCoreAsync(lease.AuthorizationUser, reason, cancellationToken);
        }
    }

    private async Task<int> CleanupCoreAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        // Cleanup queries ONLY the current tenant's durable leases (never a LIKE prefix scan over the
        // shared PJSIP Realtime table) and deletes each corresponding realtime row by EXACT authorization
        // user. This guarantees a tenant can never delete a realtime row it does not own a lease for.
        var leases = await _leaseStore.ListExpiredOrRevokedAsync(now, CleanupBatchSize, cancellationToken);
        var removed = 0;

        foreach (var lease in leases)
        {
            await _dialogTerminator.TerminateAsync(lease.AuthorizationUser, lease.RevocationReason ?? "expired", cancellationToken);
            await _realtimeStore.DeleteAsync(lease.AuthorizationUser, cancellationToken);
            await _leaseStore.DeleteAsync(lease, cancellationToken);
            await TryRemoveCacheAsync(lease.AuthorizationUser, cancellationToken);
            removed++;
        }

        return removed;
    }

    private async Task<ILocker> AcquireLockAsync()
    {
        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(CreateLockKey(), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            throw new InvalidOperationException($"The Asterisk PJSIP credential store for tenant '{GetTenantName()}' is currently being updated.");
        }

        return locker;
    }

    private static void ValidateRequest(AsteriskPjsipCredentialIssueRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new ArgumentException("A user id is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SipDomain))
        {
            throw new ArgumentException("A SIP domain is required.", nameof(request));
        }

        if (request.CredentialLifetime <= TimeSpan.Zero)
        {
            throw new ArgumentException("A positive credential lifetime is required.", nameof(request));
        }

        if (request.ContactExpiration <= TimeSpan.Zero)
        {
            throw new ArgumentException("A positive contact expiration is required.", nameof(request));
        }
    }

    private async Task WriteCacheAsync(
        AsteriskPjsipCredentialRecord record,
        CancellationToken cancellationToken)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = new DateTimeOffset(record.ExpiresAtUtc.AddMinutes(5), TimeSpan.Zero),
        };

        await _cache.SetStringAsync(CreateRecordKey(record.AuthorizationUser), JsonSerializer.Serialize(record), options, cancellationToken);
    }

    private async Task TryWriteCacheAsync(
        AsteriskPjsipCredentialRecord record,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteCacheAsync(record, cancellationToken);
        }
        catch
        {
            // The cache is a pure performance cache; a miss is reconciled from the durable lease, so a
            // failed cache write is swallowed to keep the durable authority consistent.
        }
    }

    private async Task TryRemoveCacheAsync(
        string authorizationUser,
        CancellationToken cancellationToken)
    {
        try
        {
            await _cache.RemoveAsync(CreateRecordKey(authorizationUser), cancellationToken);
        }
        catch
        {
            // The cache is a pure performance cache; a failed removal only leaves a self-expiring stale
            // projection that the durable lease authority overrides, so the exception is swallowed to keep
            // revocation and cleanup independent of cache availability.
        }
    }

    private async Task TryTeardownRealtimeAsync(
        string authorizationUser,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dialogTerminator.TerminateAsync(authorizationUser, reason, cancellationToken);
        }
        catch
        {
            // Best-effort dialog teardown; a subsequent cleanup pass reconciles the revoked lease.
        }

        try
        {
            await _realtimeStore.DeleteAsync(authorizationUser, cancellationToken);
        }
        catch
        {
            // Best-effort realtime teardown; the revoked lease guarantees cleanup can reclaim any row later.
        }
    }

    private async Task<AsteriskPjsipCredentialRecord> ReadRecordAsync(
        string authorizationUser,
        CancellationToken cancellationToken)
    {
        string json = null;

        try
        {
            json = await _cache.GetStringAsync(CreateRecordKey(authorizationUser), cancellationToken);
        }
        catch
        {
            // The cache is a pure performance cache; a transport outage (for example Redis being
            // unavailable) is treated as a miss so the durable lease stays the sole authority and
            // security-critical paths such as revocation never fail because the cache is down.
        }

        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                return JsonSerializer.Deserialize<AsteriskPjsipCredentialRecord>(json);
            }
            catch
            {
                // A corrupt cache entry is treated as a miss and reconciled from the durable lease.
            }
        }

        // Cache miss: reconcile against the durable lease authority and re-materialize the cache. A miss is
        // never interpreted as expiry; expiry is only ever read from the durable lease's ExpiresUtc.
        var lease = await _leaseStore.GetByAuthorizationUserAsync(authorizationUser, cancellationToken);

        if (lease is null)
        {
            return null;
        }

        var record = ToRecord(lease);
        await TryWriteCacheAsync(record, cancellationToken);

        return record;
    }

    private static AsteriskPjsipCredentialRecord ToRecord(AsteriskPjsipCredentialLease lease)
        => new()
        {
            TenantName = lease.TenantName,
            UserId = lease.UserId,
            SessionId = lease.SessionId,
            InteractionId = lease.InteractionId,
            EndpointName = lease.AuthorizationUser,
            AuthorizationUser = lease.AuthorizationUser,
            IssuedAtUtc = lease.IssuedUtc,
            ExpiresAtUtc = lease.ExpiresUtc,
            RevokedAtUtc = lease.RevokedUtc,
            RevocationReason = lease.RevocationReason,
        };

    private Task SignalTenantAsync()
        => _tagCache.RemoveTagAsync($"{CacheTagPrefix}:{GetTenantName()}");

    private string GetTenantName()
        => string.IsNullOrWhiteSpace(_shellSettings.Name) ? "Default" : _shellSettings.Name.Trim();

    private string CreateLockKey()
        => $"{CachePrefix}:{GetTenantName()}:lock";

    private string CreateRecordKey(string authorizationUser)
        => $"{CachePrefix}:{GetTenantName()}:user:{authorizationUser}";

    private static string CreateAuthorizationUser(string tenantName)
        => $"cc-{SanitizeIdentifier(tenantName)}-{StableTenantHash(tenantName)}-{IdGenerator.GenerateId()}";

    private static string StableTenantHash(string tenantName)
    {
        // Incorporate a fixed-width stable hash of the RAW (unsanitized) tenant name so tenants whose
        // sanitized names collide (for example "acme", "acme-east", and "Acme") produce distinct
        // authorization-user namespaces even when they share one PJSIP Realtime database.
        var bytes = Encoding.UTF8.GetBytes(tenantName ?? string.Empty);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
    }

    private static string SanitizeIdentifier(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length == 0 || builder[builder.Length - 1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-');
    }

    private static string CreatePassword()
    {
        Span<byte> bytes = stackalloc byte[PasswordByteLength];
        RandomNumberGenerator.Fill(bytes);

        return Convert.ToBase64String(bytes);
    }
}
