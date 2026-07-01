using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

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

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly IAgentSessionManager _sessionManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IAgentPresenceManager _presenceManager;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentSessionService"/> class.
    /// </summary>
    /// <param name="sessionManager">The agent session manager.</param>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="presenceManager">The agent presence manager used to sign out abandoned sessions.</param>
    /// <param name="distributedLock">The distributed lock used to serialize per-user session writes.</param>
    /// <param name="clock">The clock used to stamp session activity.</param>
    public AgentSessionService(
        IAgentSessionManager sessionManager,
        IAgentProfileManager agentManager,
        IAgentPresenceManager presenceManager,
        IDistributedLock distributedLock,
        IClock clock)
    {
        _sessionManager = sessionManager;
        _agentManager = agentManager;
        _presenceManager = presenceManager;
        _distributedLock = distributedLock;
        _clock = clock;
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
            session.QueueIds = [.. profile.QueueIds];
            session.CampaignIds = [.. profile.CampaignIds];

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

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(GetLockKey(userId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return null;
        }

        await using var acquiredLock = locker;

        var session = await _sessionManager.FindByUserIdAsync(userId, cancellationToken);

        if (session is null)
        {
            return null;
        }

        session.ConnectionIds.Remove(connectionId);
        session.IsOnline = session.ConnectionIds.Count > 0;
        session.ModifiedUtc = _clock.UtcNow;

        if (!session.IsOnline)
        {
            session.LastDisconnectedUtc = _clock.UtcNow;
        }

        await _sessionManager.UpdateAsync(session, cancellationToken: cancellationToken);

        return session;
    }

    /// <inheritdoc/>
    public async Task<AgentSession> HeartbeatAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var session = await _sessionManager.FindByUserIdAsync(userId, cancellationToken);

        if (session is null)
        {
            return null;
        }

        session.LastHeartbeatUtc = _clock.UtcNow;
        session.ModifiedUtc = session.LastHeartbeatUtc;

        await _sessionManager.UpdateAsync(session, cancellationToken: cancellationToken);

        return session;
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
            snapshot.QueueIds = [.. session.QueueIds];
            snapshot.CampaignIds = [.. session.CampaignIds];
        }

        if (profile is not null)
        {
            snapshot.HasProfile = true;
            snapshot.PresenceStatus = profile.PresenceStatus.ToString();
            snapshot.PresenceReason = profile.PresenceReason;
            snapshot.RequestedPresenceStatus = profile.RequestedPresenceStatus?.ToString();
            snapshot.ActiveReservationId = profile.ActiveReservationId;
            snapshot.QueueIds = [.. profile.QueueIds];
            snapshot.CampaignIds = [.. profile.CampaignIds];

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
        var stale = await _sessionManager.ListStaleAsync(cutoff, cancellationToken);
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
                await _presenceManager.SignOutAsync(session.UserId, cancellationToken);
            }

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
