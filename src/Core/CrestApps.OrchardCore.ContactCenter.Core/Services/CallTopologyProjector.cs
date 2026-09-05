using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Applies live call topology transitions to a <see cref="CallSession"/>. This is the only place in the
/// product that mutates legs, bridges, and bridge membership, so the rules that keep membership history
/// append-only and internally consistent exist once instead of in every caller.
/// </summary>
public static class CallTopologyProjector
{
    /// <summary>
    /// Records a leg on the session, or updates the leg already recorded under the same provider identifier.
    /// </summary>
    /// <param name="session">The call session.</param>
    /// <param name="providerLegId">The provider identifier of the leg.</param>
    /// <param name="role">The part the leg's party plays in the call.</param>
    /// <param name="status">The normalized lifecycle state of the leg.</param>
    /// <param name="utcNow">The current UTC time, used as the start time of a leg seen for the first time.</param>
    /// <param name="address">The optional address of the party on the leg.</param>
    /// <param name="agentId">The optional agent identifier when the leg belongs to an agent or a supervisor.</param>
    /// <returns>The recorded leg, or <see langword="null"/> when no leg identifier was supplied.</returns>
    public static CallLeg UpsertLeg(
        CallSession session,
        string providerLegId,
        CallPartyRole role,
        CallLegStatus status,
        DateTime utcNow,
        string address = null,
        string agentId = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrEmpty(providerLegId))
        {
            return null;
        }

        var leg = FindLeg(session, providerLegId);

        if (leg is null)
        {
            leg = new CallLeg
            {
                ProviderLegId = providerLegId,
                StartedUtc = utcNow,
            };

            session.Legs.Add(leg);
        }

        leg.Status = status;

        if (role != CallPartyRole.Unknown)
        {
            leg.Role = role;
        }

        if (!string.IsNullOrEmpty(address))
        {
            leg.Address = address;
        }

        if (!string.IsNullOrEmpty(agentId))
        {
            leg.AgentId = agentId;
        }

        if (status == CallLegStatus.Answered && !leg.AnsweredUtc.HasValue)
        {
            leg.AnsweredUtc = utcNow;
        }

        return leg;
    }

    /// <summary>
    /// Marks a leg as finished and removes its party from the bridge it was on.
    /// </summary>
    /// <param name="session">The call session.</param>
    /// <param name="providerLegId">The provider identifier of the leg.</param>
    /// <param name="endedUtc">The UTC time the leg ended.</param>
    /// <param name="hangupCause">The provider-neutral reason the leg ended.</param>
    public static void EndLeg(
        CallSession session,
        string providerLegId,
        DateTime endedUtc,
        HangupCause? hangupCause = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        var leg = FindLeg(session, providerLegId);

        if (leg is null)
        {
            return;
        }

        EndLegCore(session, leg, endedUtc, hangupCause);
    }

    /// <summary>
    /// Ends every leg the session still believes is up.
    /// </summary>
    /// <remarks>
    /// A call that has reached a terminal state takes every one of its legs with it. Ending only the leg the
    /// provider named would leave the remaining legs live forever, because a terminal session accepts no
    /// further provider deliveries that could close them.
    /// </remarks>
    /// <param name="session">The call session.</param>
    /// <param name="endedUtc">The UTC time the legs ended.</param>
    public static void EndRemainingLegs(CallSession session, DateTime endedUtc)
    {
        ArgumentNullException.ThrowIfNull(session);

        foreach (var leg in session.Legs)
        {
            if (leg is null || leg.EndedUtc.HasValue)
            {
                continue;
            }

            EndLegCore(session, leg, endedUtc, hangupCause: null);
        }
    }

    /// <summary>
    /// Closes every supervisor engagement the session still believes is live.
    /// </summary>
    /// <remarks>
    /// Stopping an engagement is refused once the call is terminal, so an engagement that outlives its call can
    /// never be closed by the supervisor who opened it. Left alone it would report a supervisor as listening to
    /// a call that ended, which is the same "nothing can say who is on this call" failure the topology exists to
    /// remove, only inverted.
    /// </remarks>
    /// <param name="session">The call session.</param>
    /// <param name="endedUtc">The UTC time the engagements ended.</param>
    public static void EndRemainingMonitorSessions(CallSession session, DateTime endedUtc)
    {
        ArgumentNullException.ThrowIfNull(session);

        foreach (var monitorSession in session.MonitorSessions)
        {
            if (monitorSession is null || monitorSession.EndedUtc.HasValue)
            {
                continue;
            }

            monitorSession.EndedUtc = endedUtc < monitorSession.StartedUtc
                ? monitorSession.StartedUtc
                : endedUtc;

            if (!string.IsNullOrEmpty(monitorSession.ProviderLegId))
            {
                Leave(session, monitorSession.ProviderLegId, endedUtc);
            }
        }
    }

    private static void EndLegCore(
        CallSession session,
        CallLeg leg,
        DateTime endedUtc,
        HangupCause? hangupCause)
    {
        // A leg that never reached an answered state failed rather than cleared, which is what lets abandon
        // and no-answer reporting tell the two apart without re-reading the provider's own vocabulary.
        leg.Status = leg.AnsweredUtc.HasValue
            ? CallLegStatus.Ended
            : CallLegStatus.Failed;

        // A provider may stamp a hangup behind the state change that preceded it, and the leg's start may come
        // from a different clock than its end. A leg that ends before it starts is not a fact, so the end is
        // clamped rather than persisted as an inversion the store would reject.
        leg.EndedUtc ??= endedUtc < leg.StartedUtc
            ? leg.StartedUtc
            : endedUtc;
        leg.HangupCause ??= hangupCause;

        Leave(session, leg.ProviderLegId, endedUtc);
    }

    /// <summary>
    /// Ensures the session has a bridge under the given provider identifier, creating one when it does not.
    /// </summary>
    /// <param name="session">The call session.</param>
    /// <param name="providerBridgeId">The provider identifier of the media topology.</param>
    /// <param name="utcNow">The UTC time the bridge was observed.</param>
    /// <returns>The session's bridge.</returns>
    public static Bridge EnsureBridge(CallSession session, string providerBridgeId, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(session);

        // A new provider topology identifier means the parties were moved to different media, so the previous
        // membership window is closed and retained rather than continued under the new identifier.
        if (session.Bridge is not null &&
            !string.IsNullOrEmpty(providerBridgeId) &&
            !string.IsNullOrEmpty(session.Bridge.ProviderBridgeId) &&
            !string.Equals(session.Bridge.ProviderBridgeId, providerBridgeId, StringComparison.Ordinal))
        {
            DestroyBridge(session, utcNow);

            session.PriorBridges.Add(session.Bridge);
            session.Bridge = null;
        }

        if (session.Bridge is null)
        {
            session.Bridge = new Bridge
            {
                ProviderBridgeId = providerBridgeId,
                Kind = BridgeKind.TwoParty,
                CreatedUtc = utcNow,
            };

            return session.Bridge;
        }

        if (string.IsNullOrEmpty(session.Bridge.ProviderBridgeId))
        {
            session.Bridge.ProviderBridgeId = providerBridgeId;
        }

        return session.Bridge;
    }

    /// <summary>
    /// Records a party joining the session's bridge.
    /// </summary>
    /// <param name="session">The call session.</param>
    /// <param name="providerLegId">The provider identifier of the joining leg.</param>
    /// <param name="role">The part the joining party plays in the call.</param>
    /// <param name="joinedUtc">The UTC time the party joined.</param>
    /// <param name="agentId">The optional agent identifier when the party is an agent or a supervisor.</param>
    /// <param name="address">The optional address of the joining party.</param>
    public static void Join(
        CallSession session,
        string providerLegId,
        CallPartyRole role,
        DateTime joinedUtc,
        string agentId = null,
        string address = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        // A destroyed bridge is a closed membership window. Admitting a live participant to it would both
        // contradict the record and violate the store's rule that a destroyed bridge retains no live member,
        // which would fail the whole delivery rather than the one late join that caused it.
        if (string.IsNullOrEmpty(providerLegId) ||
            session.Bridge is null ||
            session.Bridge.DestroyedUtc.HasValue)
        {
            return;
        }

        if (FindActiveParticipant(session.Bridge, providerLegId) is not null)
        {
            return;
        }

        session.Bridge.Participants.Add(new BridgeParticipant
        {
            ProviderLegId = providerLegId,
            Role = role,
            AgentId = agentId,
            Address = address,
            JoinedUtc = joinedUtc,
        });

        RefreshKind(session.Bridge);
    }

    /// <summary>
    /// Records a party leaving the session's bridge. The membership record is retained with its end time so
    /// the bridge's membership at a past instant stays reconstructible.
    /// </summary>
    /// <param name="session">The call session.</param>
    /// <param name="providerLegId">The provider identifier of the leaving leg.</param>
    /// <param name="leftUtc">The UTC time the party left.</param>
    public static void Leave(CallSession session, string providerLegId, DateTime leftUtc)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrEmpty(providerLegId) || session.Bridge is null)
        {
            return;
        }

        var participant = FindActiveParticipant(session.Bridge, providerLegId);

        if (participant is null)
        {
            return;
        }

        participant.LeftUtc = leftUtc < participant.JoinedUtc
            ? participant.JoinedUtc
            : leftUtc;

        RefreshKind(session.Bridge);
    }

    /// <summary>
    /// Closes the session's bridge and every membership still open on it.
    /// </summary>
    /// <param name="session">The call session.</param>
    /// <param name="destroyedUtc">The UTC time the bridge was destroyed.</param>
    public static void DestroyBridge(CallSession session, DateTime destroyedUtc)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Bridge is null)
        {
            return;
        }

        foreach (var participant in session.Bridge.Participants)
        {
            if (participant.LeftUtc is null)
            {
                participant.LeftUtc = destroyedUtc < participant.JoinedUtc
                    ? participant.JoinedUtc
                    : destroyedUtc;
            }
        }

        session.Bridge.DestroyedUtc ??= destroyedUtc;
        session.Bridge.ReportedParticipantCount = null;
    }

    /// <summary>
    /// Applies the conference flag and participant count a provider reports.
    /// </summary>
    /// <remarks>
    /// A provider that publishes only a number cannot say who those parties are. The number is therefore
    /// stored as the provider's reported count and is never turned into invented membership records, so the
    /// membership history stays a record of parties the platform actually observed.
    /// </remarks>
    /// <param name="session">The call session.</param>
    /// <param name="isConference">The provider's conference flag, when it published one.</param>
    /// <param name="participantCount">The provider's live participant count, when it published one.</param>
    /// <param name="utcNow">The UTC time the report was observed.</param>
    public static void ApplyReportedParticipation(
        CallSession session,
        bool? isConference,
        int? participantCount,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (!isConference.HasValue && !participantCount.HasValue)
        {
            return;
        }

        var bridge = EnsureBridge(session, session.Bridge?.ProviderBridgeId, utcNow);

        if (participantCount.HasValue)
        {
            bridge.ReportedParticipantCount = Math.Max(0, participantCount.Value);
        }

        if (isConference.HasValue)
        {
            bridge.Kind = isConference.Value
                ? BridgeKind.Conference
                : BridgeKind.TwoParty;

            return;
        }

        bridge.Kind = bridge.ReportedParticipantCount >= 3
            ? BridgeKind.Conference
            : BridgeKind.TwoParty;
    }

    /// <summary>
    /// Records a link from this session to another call.
    /// </summary>
    /// <param name="session">The call session.</param>
    /// <param name="kind">How the related call relates to this one.</param>
    /// <param name="establishedUtc">The UTC time the relationship was established.</param>
    /// <param name="relatedCallSessionId">The optional identifier of the related call session.</param>
    /// <param name="relatedInteractionId">The optional identifier of the related interaction.</param>
    /// <param name="relatedProviderCallId">The optional provider call identifier of the related call.</param>
    public static void Relate(
        CallSession session,
        CallRelationshipKind kind,
        DateTime establishedUtc,
        string relatedCallSessionId = null,
        string relatedInteractionId = null,
        string relatedProviderCallId = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrEmpty(relatedCallSessionId) &&
            string.IsNullOrEmpty(relatedInteractionId) &&
            string.IsNullOrEmpty(relatedProviderCallId))
        {
            return;
        }

        var existing = session.Relationships.FirstOrDefault(relationship =>
            relationship.Kind == kind &&
            string.Equals(relationship.RelatedCallSessionId, relatedCallSessionId, StringComparison.Ordinal) &&
            string.Equals(relationship.RelatedInteractionId, relatedInteractionId, StringComparison.Ordinal) &&
            string.Equals(relationship.RelatedProviderCallId, relatedProviderCallId, StringComparison.Ordinal));

        if (existing is not null)
        {
            return;
        }

        session.Relationships.Add(new CallRelationship
        {
            Kind = kind,
            RelatedCallSessionId = relatedCallSessionId,
            RelatedInteractionId = relatedInteractionId,
            RelatedProviderCallId = relatedProviderCallId,
            EstablishedUtc = establishedUtc,
        });
    }

    /// <summary>
    /// Records a consult the agent placed before deciding whether to complete a warm transfer. Calling this
    /// again with the same consult identifier advances the existing consult instead of adding a second one.
    /// </summary>
    /// <param name="session">The call session that owns the consult.</param>
    /// <param name="consultId">The platform identifier of the consult.</param>
    /// <param name="initiatedByAgentId">The agent that placed the consult.</param>
    /// <param name="targetType">The kind of destination that was consulted.</param>
    /// <param name="targetId">The identifier of the consulted destination.</param>
    /// <param name="targetAddress">The resolved address of the consulted destination.</param>
    /// <param name="startedUtc">The UTC time the consult was placed.</param>
    /// <param name="providerLegId">The provider identifier of the consult leg when the provider reports one.</param>
    /// <returns>The consult that was created or advanced.</returns>
    public static ConsultCall StartConsult(
        CallSession session,
        string consultId,
        string initiatedByAgentId,
        InteractionTransferTargetType targetType,
        string targetId,
        string targetAddress,
        DateTime startedUtc,
        string providerLegId = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrEmpty(consultId);

        var consult = FindConsult(session, consultId);

        if (consult is null)
        {
            consult = new ConsultCall
            {
                ConsultId = consultId,
                StartedUtc = startedUtc,
                Status = ConsultCallStatus.Initiated,
            };

            session.Consults.Add(consult);
        }

        consult.InitiatedByAgentId = initiatedByAgentId;
        consult.TargetType = targetType;
        consult.TargetId = targetId;
        consult.TargetAddress = targetAddress;

        if (!string.IsNullOrEmpty(providerLegId))
        {
            consult.ProviderLegId = providerLegId;

            UpsertLeg(session, providerLegId, CallPartyRole.Consult, CallLegStatus.Dialing, startedUtc, targetAddress);
        }

        return consult;
    }

    /// <summary>
    /// Advances a consult to a new lifecycle state. Terminal states stamp the end time, and a consult that
    /// reaches a terminal state also ends the consult leg so no leg is left dangling.
    /// </summary>
    /// <param name="session">The call session that owns the consult.</param>
    /// <param name="consultId">The platform identifier of the consult.</param>
    /// <param name="status">The state the consult moved to.</param>
    /// <param name="utcNow">The UTC time the transition was observed.</param>
    public static void AdvanceConsult(
        CallSession session,
        string consultId,
        ConsultCallStatus status,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(session);

        var consult = FindConsult(session, consultId);

        if (consult is null)
        {
            return;
        }

        consult.Status = status;

        if (status == ConsultCallStatus.Connected && consult.ConnectedUtc is null)
        {
            consult.ConnectedUtc = utcNow;
        }

        if (status is ConsultCallStatus.Completed or ConsultCallStatus.Cancelled or ConsultCallStatus.Failed)
        {
            consult.EndedUtc ??= utcNow;

            if (!string.IsNullOrEmpty(consult.ProviderLegId))
            {
                EndLeg(session, consult.ProviderLegId, utcNow);
                Leave(session, consult.ProviderLegId, utcNow);
            }
        }
    }

    /// <summary>
    /// Opens a supervisor engagement on the session. Barge places the supervisor in the conversation itself so
    /// the topology gains a party; listening and whispering do not, because the supervisor hears the call without
    /// being one of the parties on it.
    /// </summary>
    /// <param name="session">The call session being supervised.</param>
    /// <param name="monitorSessionId">The platform identifier of the engagement.</param>
    /// <param name="supervisorUserId">The user identifier of the supervisor that engaged.</param>
    /// <param name="supervisorAgentId">
    /// The supervisor's agent-profile identifier when the supervisor has one, so the engagement stays joinable
    /// against the agent it targets. A supervisor without an agent profile has none.
    /// </param>
    /// <param name="mode">The engagement mode.</param>
    /// <param name="startedUtc">The UTC time the engagement started.</param>
    /// <param name="providerLegId">The provider identifier of the supervisor leg when the provider reports one.</param>
    /// <returns>The recorded engagement.</returns>
    public static MonitorSession StartMonitorSession(
        CallSession session,
        string monitorSessionId,
        string supervisorUserId,
        string supervisorAgentId,
        MonitorMode mode,
        DateTime startedUtc,
        string providerLegId = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrEmpty(monitorSessionId);

        var monitorSession = new MonitorSession
        {
            MonitorSessionId = monitorSessionId,
            SupervisorUserId = supervisorUserId,
            SupervisorAgentId = supervisorAgentId,
            TargetAgentId = session.AgentId,
            Mode = mode,
            ProviderLegId = providerLegId,
            StartedUtc = startedUtc,
        };

        session.MonitorSessions.Add(monitorSession);

        if (mode == MonitorMode.Barge && !string.IsNullOrEmpty(providerLegId))
        {
            EnsureBridge(session, session.Bridge?.ProviderBridgeId, startedUtc);
            Join(session, providerLegId, CallPartyRole.Supervisor, startedUtc, supervisorAgentId);
        }

        return monitorSession;
    }

    /// <summary>
    /// Closes the live supervisor engagement a supervisor holds on the session and releases the supervisor's
    /// bridge membership when the engagement had placed them on the bridge.
    /// </summary>
    /// <param name="session">The call session being supervised.</param>
    /// <param name="supervisorUserId">The user identifier of the supervisor whose engagement ends.</param>
    /// <param name="endedUtc">The UTC time the engagement ended.</param>
    /// <returns><see langword="true"/> when a live engagement was found and closed.</returns>
    public static bool EndMonitorSession(CallSession session, string supervisorUserId, DateTime endedUtc)
    {
        ArgumentNullException.ThrowIfNull(session);

        var live = session.ActiveMonitorSessions.FirstOrDefault(monitorSession =>
            string.Equals(monitorSession.SupervisorUserId, supervisorUserId, StringComparison.Ordinal));

        if (live is null)
        {
            return false;
        }

        live.EndedUtc = endedUtc;

        if (!string.IsNullOrEmpty(live.ProviderLegId))
        {
            Leave(session, live.ProviderLegId, endedUtc);
        }

        return true;
    }

    private static ConsultCall FindConsult(CallSession session, string consultId)
    {
        if (string.IsNullOrEmpty(consultId))
        {
            return null;
        }

        return session.Consults.FirstOrDefault(consult =>
            string.Equals(consult.ConsultId, consultId, StringComparison.Ordinal));
    }

    private static CallLeg FindLeg(CallSession session, string providerLegId)
    {
        if (string.IsNullOrEmpty(providerLegId))
        {
            return null;
        }

        return session.Legs.FirstOrDefault(leg =>
            string.Equals(leg.ProviderLegId, providerLegId, StringComparison.Ordinal));
    }

    private static BridgeParticipant FindActiveParticipant(Bridge bridge, string providerLegId)
    {
        return bridge.Participants.FirstOrDefault(participant =>
            participant.LeftUtc is null &&
            string.Equals(participant.ProviderLegId, providerLegId, StringComparison.Ordinal));
    }

    private static void RefreshKind(Bridge bridge)
    {
        // A provider that publishes its own live count has already decided the shape; the topology is only
        // inferred from observed membership when the provider is not reporting one.
        if (bridge.ReportedParticipantCount.HasValue)
        {
            return;
        }

        var active = bridge.Participants.Count(participant => participant.LeftUtc is null);

        bridge.Kind = active >= 3
            ? BridgeKind.Conference
            : BridgeKind.TwoParty;
    }
}
