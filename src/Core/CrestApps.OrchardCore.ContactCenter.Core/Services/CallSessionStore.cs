using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a YesSql-based implementation of <see cref="ICallSessionStore"/>.
/// </summary>
public sealed class CallSessionStore : DocumentCatalog<CallSession, CallSessionIndex>, ICallSessionStore
{
    /// <summary>
    /// Gets a value indicating that call session updates use YesSql document-version concurrency checks so
    /// concurrent provider-event ingestion cannot lose or reverse a high-water/state update. A losing writer
    /// observes a <see cref="ConcurrencyException"/> instead of silently overwriting a newer commit.
    /// </summary>
    protected override bool CheckConcurrency => true;

    /// <inheritdoc/>
    protected override ValueTask SavingAsync(CallSession record)
    {
        ValidateTopology(record);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CallSessionStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public CallSessionStore(ISession session)
        : base(session)
    {
        CollectionName = ContactCenterStorage.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<CallSession> FindByProviderCallIdAsync(string providerCallId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerCallId);

        return await Session.Query<CallSession, CallSessionIndex>(
            index => index.ProviderCallId == providerCallId,
            collection: ContactCenterStorage.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CallSession> FindByProviderCallIdAsync(
        string providerName,
        string providerCallId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentException.ThrowIfNullOrEmpty(providerCallId);

        return await Session.Query<CallSession, CallSessionIndex>(
            index => index.ProviderName == providerName &&
                index.ProviderCallId == providerCallId,
            collection: ContactCenterStorage.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CallSession> FindByInteractionIdAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(interactionId);

        return await Session.Query<CallSession, CallSessionIndex>(
            index => index.InteractionId == interactionId,
            collection: ContactCenterStorage.CollectionName)
            .OrderByDescending(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> CountActiveAsync(CancellationToken cancellationToken = default)
    {
        return await Session.Query<CallSession, CallSessionIndex>(
            index => index.EndedUtc == null,
            collection: ContactCenterStorage.CollectionName)
            .CountAsync(cancellationToken);
    }

    private static void ValidateTopology(CallSession record)
    {
        if (!string.IsNullOrEmpty(record.AgentSessionId) &&
            string.IsNullOrEmpty(record.AgentId))
        {
            throw new InvalidOperationException("A Contact Center call session cannot claim an agent session without an owning agent.");
        }

        foreach (var leg in record.Legs)
        {
            if (leg.EndedUtc.HasValue && leg.EndedUtc.Value < leg.StartedUtc)
            {
                throw new InvalidOperationException("A Contact Center call leg cannot end before it started.");
            }
        }

        if (record.Bridge is not null)
        {
            ValidateBridge(record.Bridge);
        }

        foreach (var priorBridge in record.PriorBridges)
        {
            ValidateBridge(priorBridge);

            // A bridge is retained only because the parties were moved off it, so it is closed by definition.
            // Retaining an open one would let two bridges each claim to hold the call's live membership.
            if (!priorBridge.DestroyedUtc.HasValue)
            {
                throw new InvalidOperationException("A retained Contact Center bridge must have been destroyed.");
            }
        }

        foreach (var monitorSession in record.MonitorSessions)
        {
            if (string.IsNullOrEmpty(monitorSession.SupervisorUserId))
            {
                throw new InvalidOperationException("A Contact Center monitor session cannot exist without a supervisor.");
            }

            // Both sides of this comparison are agent-profile identifiers. Comparing the supervisor's user
            // identifier here would make the guard unfalsifiable, because the two live in different spaces.
            if (!string.IsNullOrEmpty(monitorSession.SupervisorAgentId) &&
                string.Equals(monitorSession.SupervisorAgentId, record.AgentId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A Contact Center call session supervisor cannot monitor their own agent leg.");
            }

            if (monitorSession.EndedUtc.HasValue && monitorSession.EndedUtc.Value < monitorSession.StartedUtc)
            {
                throw new InvalidOperationException("A Contact Center monitor session cannot end before it started.");
            }
        }

        // A supervisor engaged twice on one call would produce two live legs the platform cannot tell apart,
        // so a stop would release an arbitrary one and leave the other listening.
        var duplicateSupervisor = record.MonitorSessions
            .Where(monitorSession => monitorSession.IsActive)
            .GroupBy(monitorSession => monitorSession.SupervisorUserId, StringComparer.Ordinal)
            .Any(group => group.Count() > 1);

        if (duplicateSupervisor)
        {
            throw new InvalidOperationException("A Contact Center supervisor cannot hold more than one live engagement on the same call.");
        }

        foreach (var consult in record.Consults)
        {
            if (string.IsNullOrEmpty(consult.InitiatedByAgentId))
            {
                throw new InvalidOperationException("A Contact Center consult cannot exist without the agent that placed it.");
            }

            var isTerminal = consult.Status is ConsultCallStatus.Completed
                or ConsultCallStatus.Cancelled
                or ConsultCallStatus.Failed;

            if (isTerminal && !consult.EndedUtc.HasValue)
            {
                throw new InvalidOperationException("A finished Contact Center consult must record when it ended.");
            }
        }
    }

    private static void ValidateBridge(Bridge bridge)
    {
        foreach (var participant in bridge.Participants)
        {
            if (participant.LeftUtc.HasValue && participant.LeftUtc.Value < participant.JoinedUtc)
            {
                throw new InvalidOperationException("A Contact Center bridge participant cannot leave before it joined.");
            }
        }

        if (bridge.DestroyedUtc.HasValue &&
            bridge.Participants.Any(participant => participant.LeftUtc is null))
        {
            throw new InvalidOperationException("A destroyed Contact Center bridge cannot retain a participant that never left.");
        }
    }
}
