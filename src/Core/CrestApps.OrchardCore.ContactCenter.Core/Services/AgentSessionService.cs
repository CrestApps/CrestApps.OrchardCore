using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IAgentSessionService"/>. Session writes are
/// serialized per user with a distributed lock so concurrent connects, disconnects, and the stale
/// cleanup pass cannot corrupt the connection list.
/// </summary>
public sealed class AgentSessionService : IAgentSessionService
{
    /// <summary>
    /// The number of seconds without a heartbeat after which a session is considered abandoned.
    /// </summary>
    public const int StaleThresholdSeconds = 90;

    /// <summary>
    /// The number of times a disconnect re-reads and re-applies the connection removal after losing the
    /// session's version check to a concurrent writer before giving up without throwing.
    /// </summary>
    private const int MaxSessionWriteAttempts = 3;

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly IAgentSessionManager _sessionManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IAgentPresenceManager _presenceManager;
    private readonly IDistributedLock _distributedLock;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IClock _clock;
    private readonly IEnumerable<ISoftPhoneCredentialRevoker> _credentialRevokers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentSessionService"/> class.
    /// </summary>
    /// <param name="sessionManager">The agent session manager.</param>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="presenceManager">The agent presence manager used to sign out abandoned sessions.</param>
    /// <param name="distributedLock">The distributed lock used to serialize per-user session writes.</param>
    /// <param name="scopeExecutor">The scope executor used to commit heartbeat stamps in their own unit of work.</param>
    /// <param name="clock">The clock used to stamp session activity.</param>
    /// <param name="credentialRevokers">The soft-phone credential revokers invoked when an abandoned session is cleaned up.</param>
    /// <param name="logger">The logger used to record stale-session cleanup diagnostics.</param>
    public AgentSessionService(
        IAgentSessionManager sessionManager,
        IAgentProfileManager agentManager,
        IAgentPresenceManager presenceManager,
        IDistributedLock distributedLock,
        IContactCenterScopeExecutor scopeExecutor,
        IClock clock,
        IEnumerable<ISoftPhoneCredentialRevoker> credentialRevokers,
        ILogger<AgentSessionService> logger)
    {
        _sessionManager = sessionManager;
        _agentManager = agentManager;
        _presenceManager = presenceManager;
        _distributedLock = distributedLock;
        _scopeExecutor = scopeExecutor;
        _clock = clock;
        _credentialRevokers = credentialRevokers;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AgentSession> ConnectAsync(string userId, string connectionId, string userName, string displayName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(connectionId);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(GetLockKey(userId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            throw new InvalidOperationException($"The Contact Center agent session for user '{userId}' is currently being updated.");
        }

        await using var acquiredLock = locker;

        var now = _clock.UtcNow;
        var session = await _sessionManager.FindByUserIdAsync(userId, cancellationToken);
        var isNew = session is null;

        if (isNew)
        {
            session = await _sessionManager.NewAsync(cancellationToken: cancellationToken);
            session.UserId = userId;
            session.CreatedUtc = now;
            session.ConnectedUtc = now;
        }

        if (!session.ConnectionIds.Contains(connectionId))
        {
            session.ConnectionIds.Add(connectionId);
        }

        session.ConnectedUtc ??= now;
        session.IsOnline = session.ConnectionIds.Count > 0;
        session.LastHeartbeatUtc = now;
        session.ModifiedUtc = now;

        if (!string.IsNullOrEmpty(userName))
        {
            session.UserName = userName;
        }

        if (!string.IsNullOrEmpty(displayName))
        {
            session.DisplayName = displayName;
        }

        var profile = await _agentManager.FindByUserIdAsync(userId, cancellationToken);

        if (profile is not null)
        {
            session.QueueIds = AgentEntitlementUtilities.FilterEntitled(profile.QueueIds, profile.AllowedQueueIds);
            session.CampaignIds = AgentEntitlementUtilities.FilterEntitled(profile.CampaignIds, profile.AllowedCampaignIds);

            if (string.IsNullOrEmpty(session.DisplayName))
            {
                session.DisplayName = profile.DisplayName;
            }
        }

        if (isNew)
        {
            await _sessionManager.CreateAsync(session, cancellationToken: cancellationToken);
        }
        else
        {
            await _sessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
        }

        return session;
    }

    /// <inheritdoc/>
    public async Task<AgentSession> DisconnectAsync(string userId, string connectionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(connectionId);

        // Removing a connection id races the heartbeat, connect, membership sync, and stale-cleanup writers
        // that all rewrite the same session document. A distributed lock cannot make that safe here: the
        // store's version check runs when the shell scope commits, which is after any lock this method could
        // release, so the removal is applied in its own unit of work that commits before returning. That keeps
        // a lost version check contained here instead of surfacing it at the hub's OnDisconnectedAsync commit,
        // where it crashed the disconnect and left the connection stranded in the list -- keeping an agent who
        // had gone away looking online. Unlike a heartbeat, a disconnect must not be dropped on a conflict, so
        // it retries: each attempt re-reads the current session and re-applies the removal against the newest
        // committed version.
        AgentSession result = null;

        for (var attempt = 1; attempt <= MaxSessionWriteAttempts; attempt++)
        {
            try
            {
                await _scopeExecutor.ExecuteAsync<IAgentSessionManager>(async manager =>
                {
                    var session = await manager.FindByUserIdAsync(userId, cancellationToken);

                    if (session is null)
                    {
                        return;
                    }

                    session.ConnectionIds.Remove(connectionId);
                    session.IsOnline = session.ConnectionIds.Count > 0;
                    session.ModifiedUtc = _clock.UtcNow;

                    if (!session.IsOnline)
                    {
                        session.LastDisconnectedUtc = _clock.UtcNow;
                    }

                    await manager.UpdateAsync(session, cancellationToken: cancellationToken);

                    result = session;
                });

                return result;
            }
            catch (ConcurrencyException)
            {
                // Another writer committed a newer session between the read and this commit. Re-read and
                // re-apply the removal on the next attempt so the connection is not left stranded in the list.
                result = null;
            }
        }

        // Every attempt lost the version check, so the connection could not be removed. Return without throwing
        // so the hub disconnect does not crash; the stale-cleanup pass reconciles a connection list that a
        // sustained write storm left behind.
        _logger.LogWarning(
            "Could not remove Contact Center connection '{ConnectionId}' for user '{UserId}' after {Attempts} attempts because each lost the session version check.",
            connectionId.SanitizeLogValue(),
            userId.SanitizeLogValue(),
            MaxSessionWriteAttempts);

        return null;
    }

    /// <inheritdoc/>
    public async Task<AgentSession> HeartbeatAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        // A heartbeat rewrites the whole session document to move one timestamp, and it arrives from every
        // connected agent on a timer, so it is by far the most frequent write this service performs — and the
        // document it writes also carries the connection list that connect and disconnect maintain. Two things
        // therefore have to hold, and they pull against each other. The heartbeat must not overwrite a
        // connection list it read before a concurrent connect committed, which is what the store's
        // document-version check exists to prevent; and losing that check must not surface to the agent, whose
        // hub call would fail on a timer for a write that carries no information the agent needs.
        //
        // Neither a lock nor a second read on the ambient unit of work delivers this. Writes here are staged,
        // not committed: the version check runs when the shell scope commits, which is after any lock this
        // method could take has been released, so two heartbeats can serialize perfectly against each other and
        // still collide at commit. And a second read on the ambient session is answered from its identity map,
        // so it returns the instance already read rather than the row a concurrent connect committed.
        //
        // The stamp is applied in its own unit of work instead. A child scope has its own session, so the read
        // genuinely reflects what is committed and the connection list written back is the current one, and it
        // commits before returning, so a lost version check is raised here rather than thrown at the agent.
        var stampedUtc = _clock.UtcNow;
        AgentSession stamped = null;

        try
        {
            await _scopeExecutor.ExecuteAsync<IAgentSessionManager>(async manager =>
            {
                var current = await manager.FindByUserIdAsync(userId, cancellationToken);

                if (current is null)
                {
                    return;
                }

                current.LastHeartbeatUtc = stampedUtc;
                current.ModifiedUtc = stampedUtc;

                await manager.UpdateAsync(current, cancellationToken: cancellationToken);

                stamped = current;
            });
        }
        catch (ConcurrencyException)
        {
            // Losing the version check means another writer (connect, disconnect, the cleanup pass, or a
            // membership sync) committed a newer version of this session while the stamp was in flight.
            // Retrying is wrong: connect and the cleanup pass already carry a newer heartbeat, so a retry
            // would write an older timestamp over a newer one. Disconnect and membership sync do not advance
            // the heartbeat, so a heartbeat lost to one of them records no liveness — that is tolerated
            // because neither fires on a timer and the stale threshold spans several heartbeat intervals.
            stamped = null;
        }

        // Read back on the caller's unit of work, so the caller is told what that unit of work sees, including
        // when the stamp lost its race or the session had already been signed out.
        return stamped is null
            ? await _sessionManager.FindByUserIdAsync(userId, cancellationToken)
            : stamped;
    }

    /// <inheritdoc/>
    public async Task<AgentDesktopSnapshot> BuildSnapshotAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var profile = await _agentManager.FindByUserIdAsync(userId, cancellationToken);
        var session = await _sessionManager.FindByUserIdAsync(userId, cancellationToken);

        var snapshot = new AgentDesktopSnapshot
        {
            UserId = userId,
            ServerTimeUtc = _clock.UtcNow,
        };

        if (session is not null)
        {
            snapshot.IsOnline = session.IsOnline;
            snapshot.LastHeartbeatUtc = session.LastHeartbeatUtc;
            snapshot.DisplayName = session.DisplayName;
        }

        if (profile is not null)
        {
            snapshot.HasProfile = true;
            snapshot.PresenceStatus = profile.PresenceStatus.ToString();
            snapshot.PresenceReason = profile.PresenceReason;
            snapshot.RequestedPresenceStatus = profile.RequestedPresenceStatus?.ToString();
            snapshot.ActiveReservationId = profile.ActiveReservationId;
            snapshot.QueueIds = AgentEntitlementUtilities.FilterEntitled(profile.QueueIds, profile.AllowedQueueIds);
            snapshot.CampaignIds = AgentEntitlementUtilities.FilterEntitled(profile.CampaignIds, profile.AllowedCampaignIds);

            if (string.IsNullOrEmpty(snapshot.DisplayName))
            {
                snapshot.DisplayName = profile.DisplayName;
            }
        }

        if (string.IsNullOrEmpty(snapshot.DisplayName))
        {
            snapshot.DisplayName = userId;
        }

        return snapshot;
    }

    /// <inheritdoc/>
    public async Task<int> ExpireStaleAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = _clock.UtcNow.AddSeconds(-StaleThresholdSeconds);
        var stale = await _sessionManager.GetStaleAsync(cutoff, cancellationToken);
        var count = 0;

        foreach (var candidate in stale)
        {
            if (string.IsNullOrEmpty(candidate.UserId))
            {
                continue;
            }

            (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(GetLockKey(candidate.UserId), _lockTimeout, _lockExpiration);

            if (!locked)
            {
                continue;
            }

            await using var acquiredLock = locker;

            var session = await _sessionManager.FindByUserIdAsync(candidate.UserId, cancellationToken);

            if (session is null || (session.LastHeartbeatUtc.HasValue && session.LastHeartbeatUtc.Value >= cutoff))
            {
                continue;
            }

            var profile = await _agentManager.FindByUserIdAsync(session.UserId, cancellationToken);

            if (profile is not null && profile.PresenceStatus != AgentPresenceStatus.Offline)
            {
                // Take the agent offline but leave their queue and campaign memberships intact. A lapsed
                // heartbeat means the agent stopped being reachable, not that they chose to stop working, and
                // routing already refuses anyone who is not Available with a live session. Signing them out here
                // instead stranded agents in an "Available but signed into nothing" state: the agent bar happily
                // restores presence to Available on reconnect, but nothing restores the memberships, so routing
                // silently skipped them with "no agents are currently available for this queue".
                await _presenceManager.MarkOfflineAsync(session.UserId, "session-expired", cancellationToken);
            }

            // A session can reach this cleanup path purely by cookie expiry, which never raises a sign-out and so
            // never revokes the agent's browser soft-phone credentials. Revoke them here so the durable backstop
            // tears down the same credentials the interactive sign-out flow would have.
            await SoftPhoneCredentialRevocation.RevokeForUserAsync(_credentialRevokers, session.UserId, "session-expired", _logger, cancellationToken);

            await _sessionManager.DeleteAsync(session, cancellationToken);
            count++;
        }

        return count;
    }

    private static string GetLockKey(string userId)
    {
        return $"ContactCenterAgentSession:{userId}";
    }
}
