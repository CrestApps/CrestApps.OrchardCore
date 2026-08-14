using System.Text.RegularExpressions;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Establishes that a live call is described by a topology rather than by scalars. A call used to be summarized
/// by <c>MediaTopologyId</c>, <c>ConferenceId</c>, <c>SupervisorAgentId</c>, <c>SupervisorLegId</c>, a
/// <c>ParticipantCount</c>, and an <c>IsConference</c> flag, none of which could answer who was on the call at a
/// given moment, which of them left, or whether an agent was consulting a third party while the customer waited.
/// The session now carries legs, a bridge with an append-only membership history, consults, monitor sessions, and
/// typed relationships, and <see cref="CallTopologyProjector"/> is the only writer of any of it.
/// </summary>
/// <remarks>
/// The four scalars this replaces were read by the store's validation but were never written by any code path in
/// the product, so supervisor monitoring and conference membership had no durable representation at all: a
/// supervisor engagement fired a provider command and published an event and left nothing behind. That is why the
/// tests below assert on live representation and not merely on shape.
/// </remarks>
public sealed class CallTopologyAuthorityTests
{
    private static readonly DateTime _now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// The topology collections on <see cref="CallSession"/> that only the projector may mutate.
    /// </summary>
    private static readonly string[] _topologyMemberNames =
    [
        nameof(CallSession.Legs),
        nameof(CallSession.Bridge),
        nameof(CallSession.PriorBridges),
        nameof(CallSession.Consults),
        nameof(CallSession.MonitorSessions),
        nameof(CallSession.Relationships),
    ];

    private static readonly string[] _sourceProjectFolders =
    [
        Path.Combine("Core", "CrestApps.OrchardCore.ContactCenter.Core"),
        Path.Combine("Modules", "CrestApps.OrchardCore.ContactCenter"),
        Path.Combine("Modules", "CrestApps.OrchardCore.Telephony"),
        Path.Combine("Modules", "CrestApps.OrchardCore.Asterisk"),
        Path.Combine("Modules", "CrestApps.OrchardCore.Dialpad"),
        Path.Combine("Targets", "CrestApps.OrchardCore.Cms.Core.Targets"),
        Path.Combine("Startup", "CrestApps.Aspire.AppHost"),
        Path.Combine("Startup", "CrestApps.OrchardCore.Cms.Web"),
    ];

    /// <summary>
    /// The only file allowed to mutate live call topology.
    /// </summary>
    private const string ProjectorFileName = "CallTopologyProjector.cs";

    [Fact]
    public void NoContactCenterSource_MutatesLiveCallTopology_OutsideTheProjector()
    {
        // Arrange
        var violations = new List<string>();

        // Act
        foreach (var file in EnumerateContactCenterSources())
        {
            if (string.Equals(Path.GetFileName(file), ProjectorFileName, StringComparison.Ordinal))
            {
                continue;
            }

            violations.AddRange(FindTopologyMutations(file));
        }

        // Assert
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void TheAuthorityScan_ReportsEveryTopologyMutation_InTheProjectorItself()
    {
        // Arrange
        // The scan above only fails when it recognizes a receiver as a call session, so a change that stopped
        // recognizing them would silently pass everything. The projector is the known-positive control: it is the
        // one file that mutates all three collections, and the scan has to see each of them at least once.
        var projector = FindProjectorFile();

        // Act
        var detected = FindTopologyMutations(projector);

        // Assert
        Assert.NotEmpty(detected);

        foreach (var member in _topologyMemberNames)
        {
            Assert.Contains(detected, violation => violation.Contains($".{member}", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void TheCallSession_NoLongerCarriesTheScalarsThatCouldNotDescribeACall()
    {
        // Arrange
        var properties = typeof(CallSession)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        // Act
        var survivors = new[] { "MediaTopologyId", "ConferenceId", "SupervisorAgentId", "SupervisorLegId" }
            .Where(properties.Contains);

        // Assert
        Assert.Empty(survivors);
    }

    [Fact]
    public void ParticipantsAt_ReconstructsConferenceMembership_AtAPastInstant()
    {
        // Arrange
        var session = CreateSession();

        CallTopologyProjector.EnsureBridge(session, "bridge-1", _now);
        CallTopologyProjector.Join(session, "leg-customer", CallPartyRole.Customer, _now);
        CallTopologyProjector.Join(session, "leg-agent", CallPartyRole.Agent, _now.AddSeconds(10), "agent-1");
        CallTopologyProjector.Join(session, "leg-third", CallPartyRole.External, _now.AddSeconds(20));
        CallTopologyProjector.Leave(session, "leg-agent", _now.AddSeconds(30));

        // Act
        var beforeAgentJoined = session.Bridge.ParticipantsAt(_now.AddSeconds(5)).Select(p => p.ProviderLegId);
        var whileAllThree = session.Bridge.ParticipantsAt(_now.AddSeconds(25)).Select(p => p.ProviderLegId);
        var afterAgentLeft = session.Bridge.ParticipantsAt(_now.AddSeconds(40)).Select(p => p.ProviderLegId);

        // Assert
        Assert.Equal(["leg-customer"], beforeAgentJoined);
        Assert.Equal(["leg-customer", "leg-agent", "leg-third"], whileAllThree);
        Assert.Equal(["leg-customer", "leg-third"], afterAgentLeft);
    }

    [Fact]
    public void Leave_DoesNotEraseTheParticipant_SoMembershipHistoryStaysAppendOnly()
    {
        // Arrange
        var session = CreateSession();

        CallTopologyProjector.EnsureBridge(session, "bridge-1", _now);
        CallTopologyProjector.Join(session, "leg-agent", CallPartyRole.Agent, _now, "agent-1");

        // Act
        CallTopologyProjector.Leave(session, "leg-agent", _now.AddSeconds(30));

        // Assert
        var participant = Assert.Single(session.Bridge.Participants);
        Assert.Equal(_now, participant.JoinedUtc);
        Assert.Equal(_now.AddSeconds(30), participant.LeftUtc);
        Assert.Empty(session.Bridge.ActiveParticipants);
    }

    [Fact]
    public void AConsultTransfer_IsRepresentable_WithoutProviderMetadataStrings()
    {
        // Arrange
        var session = CreateSession();

        // Act
        CallTopologyProjector.StartConsult(
            session,
            "consult-1",
            "agent-1",
            InteractionTransferTargetType.Agent,
            "agent-2",
            "PJSIP/agent-2",
            _now,
            "leg-consult");

        CallTopologyProjector.AdvanceConsult(session, "consult-1", ConsultCallStatus.Connected, _now.AddSeconds(5));

        // Assert
        var consult = Assert.Single(session.Consults);
        Assert.Equal("agent-1", consult.InitiatedByAgentId);
        Assert.Equal(InteractionTransferTargetType.Agent, consult.TargetType);
        Assert.Equal("agent-2", consult.TargetId);
        Assert.Equal(ConsultCallStatus.Connected, consult.Status);
        Assert.Equal(_now.AddSeconds(5), consult.ConnectedUtc);
        Assert.Empty(session.Metadata);

        var consultLeg = Assert.Single(session.Legs, leg => leg.Role == CallPartyRole.Consult);
        Assert.Equal("leg-consult", consultLeg.ProviderLegId);
    }

    [Fact]
    public void AnEndedConsult_EndsItsLeg_SoNoLegIsLeftDangling()
    {
        // Arrange
        var session = CreateSession();

        CallTopologyProjector.EnsureBridge(session, "bridge-1", _now);
        CallTopologyProjector.StartConsult(
            session,
            "consult-1",
            "agent-1",
            InteractionTransferTargetType.External,
            "+15550000000",
            "+15550000000",
            _now,
            "leg-consult");

        CallTopologyProjector.Join(session, "leg-consult", CallPartyRole.Consult, _now.AddSeconds(5));

        // Act
        CallTopologyProjector.AdvanceConsult(session, "consult-1", ConsultCallStatus.Cancelled, _now.AddSeconds(9));

        // Assert
        var consult = Assert.Single(session.Consults);
        Assert.Equal(_now.AddSeconds(9), consult.EndedUtc);

        var leg = Assert.Single(session.Legs);
        Assert.Equal(_now.AddSeconds(9), leg.EndedUtc);
        Assert.Empty(session.Bridge.ActiveParticipants);
    }

    [Fact]
    public void ATransferChain_IsWalkable_ThroughTypedRelationships()
    {
        // Arrange
        var session = CreateSession();

        // Act
        CallTopologyProjector.Relate(
            session,
            CallRelationshipKind.TransferredTo,
            _now,
            relatedInteractionId: "interaction-2");

        // Relating the same pair twice must not duplicate the edge, otherwise walking a chain would
        // traverse the same hop repeatedly.
        CallTopologyProjector.Relate(
            session,
            CallRelationshipKind.TransferredTo,
            _now.AddSeconds(5),
            relatedInteractionId: "interaction-2");

        // Assert
        var relationship = Assert.Single(session.Relationships);
        Assert.Equal(CallRelationshipKind.TransferredTo, relationship.Kind);
        Assert.Equal("interaction-2", relationship.RelatedInteractionId);
        Assert.Equal(_now, relationship.EstablishedUtc);
    }

    [Fact]
    public void Relate_WithNoIdentifier_RecordsNothing()
    {
        // Arrange
        var session = CreateSession();

        // Act
        CallTopologyProjector.Relate(session, CallRelationshipKind.ConsultOf, _now);

        // Assert
        // A relationship that points at nothing is worse than no relationship: it makes a chain look longer
        // than it is while being unwalkable.
        Assert.Empty(session.Relationships);
    }

    [Theory]
    [InlineData(MonitorMode.Monitor)]
    [InlineData(MonitorMode.Whisper)]
    [InlineData(MonitorMode.Barge)]
    public void EveryMonitorMode_HasALiveRepresentation(MonitorMode mode)
    {
        // Arrange
        var session = CreateSession();

        // Act
        CallTopologyProjector.StartMonitorSession(
            session,
            "monitor-1",
            "supervisor-user-1",
            "supervisor-agent-1",
            mode,
            _now,
            "leg-supervisor");

        // Assert
        var live = Assert.Single(session.ActiveMonitorSessions);
        Assert.Equal(mode, live.Mode);
        Assert.True(live.IsActive);
    }

    [Fact]
    public void AnEndedMonitorSession_IsNoLongerLive_ButIsStillOnTheRecord()
    {
        // Arrange
        var session = CreateSession();

        CallTopologyProjector.StartMonitorSession(
            session,
            "monitor-1",
            "supervisor-user-1",
            "supervisor-agent-1",
            MonitorMode.Monitor,
            _now);

        // Act
        var closed = CallTopologyProjector.EndMonitorSession(session, "supervisor-user-1", _now.AddMinutes(2));

        // Assert
        Assert.True(closed);
        Assert.Empty(session.ActiveMonitorSessions);
        Assert.Single(session.MonitorSessions);
    }

    [Fact]
    public void ParticipantCount_PrefersTheProviderReportedCount_OverObservedMembership()
    {
        // Arrange
        var session = CreateSession();

        CallTopologyProjector.EnsureBridge(session, "bridge-1", _now);
        CallTopologyProjector.Join(session, "leg-customer", CallPartyRole.Customer, _now);

        // Act
        // A provider that publishes only a count cannot say who those parties are. Fabricating members to match
        // the count would turn membership history into fiction, so the count is carried separately and wins.
        CallTopologyProjector.ApplyReportedParticipation(session, isConference: true, participantCount: 4, utcNow: _now);

        // Assert
        Assert.Equal(4, session.ParticipantCount);
        Assert.True(session.IsConference);
        Assert.Single(session.Bridge.ActiveParticipants);
    }

    [Fact]
    public void EndLeg_DistinguishesANeverAnsweredLeg_FromOneThatHungUp()
    {
        // Arrange
        var session = CreateSession();

        CallTopologyProjector.UpsertLeg(session, "leg-ringing", CallPartyRole.Agent, CallLegStatus.Ringing, _now);
        CallTopologyProjector.UpsertLeg(session, "leg-answered", CallPartyRole.Customer, CallLegStatus.Answered, _now);

        // Act
        CallTopologyProjector.EndLeg(session, "leg-ringing", _now.AddSeconds(20));
        CallTopologyProjector.EndLeg(session, "leg-answered", _now.AddSeconds(20));

        // Assert
        // Abandon and no-answer reporting depends on this distinction, and no provider vocabulary is involved.
        Assert.Equal(CallLegStatus.Failed, Assert.Single(session.Legs, leg => leg.ProviderLegId == "leg-ringing").Status);
        Assert.Equal(CallLegStatus.Ended, Assert.Single(session.Legs, leg => leg.ProviderLegId == "leg-answered").Status);
    }

    [Fact]
    public void EndLeg_NeverStampsAnEndBeforeTheLegStarted()
    {
        // Arrange
        // A hangup can carry a timestamp behind the state change that preceded it, and a leg started from the
        // application clock can be ended from the provider's. An inverted leg is not a fact and the store
        // rejects it, which would leave the call stuck mid-teardown and the agent occupied forever.
        var session = CreateSession();

        CallTopologyProjector.UpsertLeg(session, "leg-1", CallPartyRole.Agent, CallLegStatus.Answered, _now);

        // Act
        CallTopologyProjector.EndLeg(session, "leg-1", _now.AddSeconds(-30));

        // Assert
        var leg = Assert.Single(session.Legs);

        Assert.Equal(_now, leg.EndedUtc);
    }

    [Fact]
    public void EndRemainingLegs_ClosesEveryLegAndLetsTheBridgeBeDestroyed()
    {
        // Arrange
        // Only the leg a provider names on the hangup used to be closed. Because a terminal session accepts no
        // further deliveries, every other leg stayed open forever and the bridge kept claiming a party that had
        // already gone, which is exactly the reconstruction this model exists to get right.
        var session = CreateSession();

        CallTopologyProjector.EnsureBridge(session, "bridge-1", _now);
        CallTopologyProjector.UpsertLeg(session, "leg-customer", CallPartyRole.Customer, CallLegStatus.Answered, _now);
        CallTopologyProjector.UpsertLeg(session, "leg-agent", CallPartyRole.Agent, CallLegStatus.Answered, _now);
        CallTopologyProjector.Join(session, "leg-customer", CallPartyRole.Customer, _now);
        CallTopologyProjector.Join(session, "leg-agent", CallPartyRole.Agent, _now);

        var ended = _now.AddMinutes(3);

        // Act
        CallTopologyProjector.EndLeg(session, "leg-customer", ended);
        CallTopologyProjector.EndRemainingLegs(session, ended);
        CallTopologyProjector.DestroyBridge(session, ended);

        // Assert
        Assert.All(session.Legs, leg => Assert.True(leg.EndedUtc.HasValue));
        Assert.Empty(session.Bridge.ActiveParticipants);
        Assert.Equal(0, session.ParticipantCount);
        Assert.True(session.Bridge.DestroyedUtc.HasValue);
    }

    [Fact]
    public void Join_IsRefused_OnceTheBridgeHasBeenDestroyed()
    {
        // Arrange
        // A destroyed bridge is a closed membership window. Work already in flight when the call ended still
        // reports its leg afterwards, and admitting it would both contradict the record and produce a bridge the
        // store refuses to persist, failing the whole delivery rather than the one late join that caused it.
        var session = CreateSession();

        CallTopologyProjector.EnsureBridge(session, "bridge-1", _now);
        CallTopologyProjector.UpsertLeg(session, "leg-customer", CallPartyRole.Customer, CallLegStatus.Answered, _now);
        CallTopologyProjector.Join(session, "leg-customer", CallPartyRole.Customer, _now);

        var ended = _now.AddMinutes(3);

        CallTopologyProjector.EndRemainingLegs(session, ended);
        CallTopologyProjector.DestroyBridge(session, ended);

        // Act
        CallTopologyProjector.Join(session, "leg-late-agent", CallPartyRole.Agent, ended.AddSeconds(1));

        // Assert
        Assert.DoesNotContain(session.Bridge.Participants, participant => participant.ProviderLegId == "leg-late-agent");
        Assert.Empty(session.Bridge.ActiveParticipants);
    }

    [Fact]
    public void AMediaMove_RetainsThePreviousBridge_AndAdoptsTheNewIdentifier()
    {
        // Arrange
        // A changed provider topology identifier means the parties were moved to different media. Returning the
        // destroyed bridge under its old identifier would keep every later join on a bridge the provider has
        // already torn down, which the store rejects at persist time.
        var session = CreateSession();

        CallTopologyProjector.EnsureBridge(session, "bridge-1", _now);
        CallTopologyProjector.Join(session, "leg-customer", CallPartyRole.Customer, _now);

        // Act
        var moved = CallTopologyProjector.EnsureBridge(session, "bridge-2", _now.AddSeconds(30));

        // Assert
        Assert.Equal("bridge-2", moved.ProviderBridgeId);
        Assert.Null(moved.DestroyedUtc);
        Assert.Empty(moved.Participants);

        var retained = Assert.Single(session.PriorBridges);

        Assert.Equal("bridge-1", retained.ProviderBridgeId);
        Assert.True(retained.DestroyedUtc.HasValue);

        // The whole point of retaining it: who was on the call before the move is still answerable.
        Assert.Single(session.ParticipantsAt(_now.AddSeconds(5)));
    }

    [Fact]
    public void AMonitorSession_RecordsTheSupervisorsUserAndAgentIdentifiers_Separately()
    {
        // Arrange
        // A supervisor is always a user but is not always an agent. Storing the user identifier in the agent
        // field made the store's self-monitoring guard compare two different identity spaces, so it could never
        // fire and a supervisor could silently monitor their own call.
        var session = CreateSession();

        // Act
        var monitorSession = CallTopologyProjector.StartMonitorSession(
            session,
            "monitor-1",
            "supervisor-user-1",
            "supervisor-agent-1",
            MonitorMode.Monitor,
            _now);

        // Assert
        Assert.Equal("supervisor-user-1", monitorSession.SupervisorUserId);
        Assert.Equal("supervisor-agent-1", monitorSession.SupervisorAgentId);
    }

    /// <summary>
    /// Reports every statement in a file that mutates live call topology.
    /// </summary>
    /// <remarks>
    /// The scan deliberately does not try to infer the type of the receiver. An earlier version did, and a
    /// sabotage probe that added <c>callSession.Legs.Add(...)</c> to a service passed the gate, because the
    /// receiver came from <c>var callSession = await manager.FindByInteractionIdAsync(...)</c> and no written
    /// type was there to recognize. The member names below are declared on exactly one type in the product
    /// — a fact <see cref="TheTopologyMemberNames_AreUniqueToTheCallSession"/> pins — so matching the member
    /// name alone is both sufficient and impossible to blind with an inference change.
    /// <para>
    /// Matching only the member itself was still evadable in four ways a reviewer demonstrated: aliasing the
    /// collection into a local first, mutating the objects reached <em>through</em> a topology member rather
    /// than the member itself, seeding topology inside an object initializer, and using a mutator the scan did
    /// not list. The scan therefore treats any expression whose left spine reaches a topology member as
    /// topology, flags aliasing and hand-off of a topology member, and recognizes reads through an allow list
    /// so that an unfamiliar method is treated as a mutation rather than ignored.
    /// </para>
    /// </remarks>
    /// <param name="file">The full path of the source file to scan.</param>
    /// <returns>One description per mutation, empty when the file mutates none.</returns>
    private static List<string> FindTopologyMutations(string file)
    {
        var root = ParseFile(file);
        var mutations = new List<string>();
        var name = Path.GetFileName(file);

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax call)
            {
                continue;
            }

            var method = call.Name.Identifier.ValueText;
            var isRead = _readOnlyMemberNames.Contains(method, StringComparer.Ordinal);
            var rooted = TopologyRootName(call.Expression);

            // The allow list is inverted on purpose. Listing mutators would let any newly introduced one — an
            // extension method, 'Sort', 'Reverse' — slip through unlisted, so anything unrecognized counts.
            if (rooted is not null && !isRead)
            {
                mutations.Add($"{name}:{LineOf(invocation)} calls {method} on .{rooted} of a call session.");
            }

            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                var handedOff = TopologyMemberAccessName(argument.Expression);

                if (handedOff is not null && !isRead)
                {
                    mutations.Add(
                        $"{name}:{LineOf(argument)} hands .{handedOff} of a call session to {method}, " +
                        "which can then mutate it out of sight of this scan.");
                }
            }
        }

        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            var target = TopologyRootName(assignment.Left);

            if (target is not null)
            {
                mutations.Add($"{name}:{LineOf(assignment)} assigns .{target} on a call session.");
            }

            // An object or collection initializer assigns to a bare identifier, so the left side alone never
            // looks like topology. Seeding a session's legs in an initializer is still a topology write.
            if (assignment.Parent is InitializerExpressionSyntax &&
                assignment.Left is IdentifierNameSyntax identifier &&
                _topologyMemberNames.Contains(identifier.Identifier.ValueText, StringComparer.Ordinal))
            {
                mutations.Add(
                    $"{name}:{LineOf(assignment)} seeds .{identifier.Identifier.ValueText} in an initializer.");
            }

            // Aliasing the member into a local hides every later mutation behind an identifier.
            var aliased = TopologyMemberAccessName(assignment.Right);

            if (aliased is not null &&
                assignment.Left is IdentifierNameSyntax alias &&
                AliasIsMutated(root, EnclosingScope(assignment), alias.Identifier.ValueText))
            {
                mutations.Add($"{name}:{LineOf(assignment)} aliases .{aliased} of a call session and mutates it.");
            }
        }

        foreach (var declarator in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (declarator.Initializer is null)
            {
                continue;
            }

            var aliased = TopologyMemberAccessName(declarator.Initializer.Value);

            if (aliased is not null &&
                AliasIsMutated(root, EnclosingScope(declarator), declarator.Identifier.ValueText))
            {
                mutations.Add($"{name}:{LineOf(declarator)} aliases .{aliased} of a call session and mutates it.");
            }
        }

        // A loop variable is a plain identifier, not a declarator, so iterating the collection would otherwise
        // hand out every element with nothing left for the left-spine walk to root in.
        foreach (var iteration in root.DescendantNodes().OfType<ForEachStatementSyntax>())
        {
            var iterated = TopologyMemberAccessName(iteration.Expression);

            if (iterated is not null &&
                AliasIsMutated(root, iteration.Statement, iteration.Identifier.ValueText))
            {
                mutations.Add($"{name}:{LineOf(iteration)} iterates .{iterated} of a call session and mutates it.");
            }
        }

        return mutations;
    }

    [Fact]
    public void TheTopologyMemberNames_AreUniqueToTheCallSession()
    {
        // Arrange
        // The scan matches on member name alone, so it is only sound while no other shipped type declares one
        // of these names. If a second type ever does, the scan starts reporting mutations that are not topology
        // and this test says so before the gate turns into noise.
        var declarations = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var member in _topologyMemberNames)
        {
            declarations[member] = [];
        }

        // Act
        foreach (var file in EnumerateContactCenterSources())
        {
            foreach (var property in ParseFile(file).DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                var name = property.Identifier.ValueText;

                if (declarations.TryGetValue(name, out var owners))
                {
                    owners.Add($"{Path.GetFileName(file)}:{LineOf(property)}");
                }
            }
        }

        // Assert
        foreach (var (member, owners) in declarations)
        {
            Assert.True(
                owners.Count == 1,
                $"'{member}' must be declared exactly once but was declared at: {string.Join(", ", owners)}.");
        }
    }

    private static SyntaxNode ParseFile(string file)
        => CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();

    /// <summary>
    /// Reports the syntax node a local's uses are confined to, so an alias is only examined where it is live.
    /// </summary>
    /// <param name="node">The node that introduces the local.</param>
    /// <returns>The enclosing member declaration, or the file root when the node has none.</returns>
    private static SyntaxNode EnclosingScope(SyntaxNode node)
        => node.FirstAncestorOrSelf<MemberDeclarationSyntax>() ?? node.SyntaxTree.GetRoot();

    /// <summary>
    /// Determines whether a local holding call-session topology is mutated, or handed somewhere that could
    /// mutate it, anywhere in its scope. Reading topology is not the concern of this gate: only writing it is,
    /// so an alias taken purely to read is not reported and the gate stays free of noise on validation code.
    /// </summary>
    /// <param name="root">The root of the file being scanned.</param>
    /// <param name="scope">The syntax node the alias's uses are confined to.</param>
    /// <param name="identifier">The name of the local holding the topology.</param>
    /// <returns><see langword="true"/> when the alias is written through or escapes to something that could.</returns>
    private static bool AliasIsMutated(SyntaxNode root, SyntaxNode scope, string identifier)
        => AliasIsMutated(root, scope, identifier, new HashSet<string>(StringComparer.Ordinal));

    private static bool AliasIsMutated(
        SyntaxNode root,
        SyntaxNode scope,
        string identifier,
        HashSet<string> visited)
    {
        foreach (var usage in scope.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (!string.Equals(usage.Identifier.ValueText, identifier, StringComparison.Ordinal))
            {
                continue;
            }

            // Writing through the alias, at any depth: 'leg.EndedUtc = x' and 'leg.Foo.Bar = x' alike.
            if (usage.Ancestors().OfType<AssignmentExpressionSyntax>().Any(assignment =>
                assignment.Left.Span.Contains(usage.Span) &&
                RootsAtIdentifier(assignment.Left, identifier)))
            {
                return true;
            }

            var invocation = usage.FirstAncestorOrSelf<InvocationExpressionSyntax>();

            if (invocation is null)
            {
                continue;
            }

            // Calling something on the alias that is not a known read.
            if (RootsAtIdentifier(invocation.Expression, identifier))
            {
                if (!_readOnlyMemberNames.Contains(OutermostMemberName(invocation) ?? string.Empty, StringComparer.Ordinal))
                {
                    return true;
                }

                continue;
            }

            // Handing the alias to something else. A method declared in this same file is followed, so a
            // mutation moved one call away is still found; anything else is unknown and therefore a mutation.
            var argument = usage.FirstAncestorOrSelf<ArgumentSyntax>();

            // Only the object itself is handed off. Passing one of its fields passes a copy of that value, so
            // treating 'string.IsNullOrEmpty(leg.ProviderLegId)' as a hand-off would report every validation.
            if (argument is null ||
                argument.Parent?.Parent != invocation ||
                Unwrap(argument.Expression) is not IdentifierNameSyntax handedOff ||
                !string.Equals(handedOff.Identifier.ValueText, identifier, StringComparison.Ordinal))
            {
                continue;
            }

            var callee = OutermostMemberName(invocation) ??
                (invocation.Expression as IdentifierNameSyntax)?.Identifier.ValueText;

            if (callee is null || !visited.Add(callee))
            {
                return true;
            }

            var declaration = root
                .DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(method => string.Equals(method.Identifier.ValueText, callee, StringComparison.Ordinal));

            var position = ((ArgumentListSyntax)argument.Parent).Arguments.IndexOf(argument);

            if (declaration?.Body is null || position >= declaration.ParameterList.Parameters.Count)
            {
                return true;
            }

            if (AliasIsMutated(
                root,
                declaration.Body,
                declaration.ParameterList.Parameters[position].Identifier.ValueText,
                visited))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether an expression's left spine reaches the named local.
    /// </summary>
    /// <param name="expression">The expression to examine.</param>
    /// <param name="identifier">The local's name.</param>
    /// <returns><see langword="true"/> when the expression is rooted in that local.</returns>
    private static bool RootsAtIdentifier(ExpressionSyntax expression, string identifier)
    {
        var current = Unwrap(expression);

        while (current is not null)
        {
            switch (current)
            {
                case IdentifierNameSyntax name:
                    return string.Equals(name.Identifier.ValueText, identifier, StringComparison.Ordinal);

                case MemberAccessExpressionSyntax memberAccess:
                    current = Unwrap(memberAccess.Expression);
                    break;

                case ElementAccessExpressionSyntax elementAccess:
                    current = Unwrap(elementAccess.Expression);
                    break;

                case InvocationExpressionSyntax invocation:
                    current = Unwrap(invocation.Expression);
                    break;

                case ConditionalAccessExpressionSyntax conditionalAccess:
                    current = Unwrap(conditionalAccess.Expression);
                    break;

                default:
                    return false;
            }
        }

        return false;
    }

    private const string CoreProjectName = "CrestApps.OrchardCore.ContactCenter.Core";

    /// <summary>
    /// Reads the absolute paths of the projects a project file references directly.
    /// </summary>
    /// <param name="project">The full path of the project file to read.</param>
    /// <returns>The referenced project paths, empty when the project references none.</returns>
    private static List<string> ReadProjectReferences(string project)
    {
        var folder = Path.GetDirectoryName(project);
        var referenced = new List<string>();

        foreach (Match match in Regex.Matches(
            File.ReadAllText(project),
            "<ProjectReference\\s+Include=\"([^\"]+)\""))
        {
            var relative = match.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar);

            referenced.Add(Path.GetFullPath(Path.Combine(folder, relative)));
        }

        return referenced;
    }

    /// <summary>
    /// Determines whether a project can reach the Contact Center core through its reference closure.
    /// </summary>
    /// <param name="project">The full path of the project file to start from.</param>
    /// <param name="references">The direct references of every project under the source root.</param>
    /// <returns><see langword="true"/> when the core is reachable.</returns>
    private static bool ReachesCore(string project, Dictionary<string, List<string>> references)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>();

        pending.Enqueue(project);

        while (pending.Count > 0)
        {
            var current = pending.Dequeue();

            if (!visited.Add(current))
            {
                continue;
            }

            if (string.Equals(
                Path.GetFileNameWithoutExtension(current),
                CoreProjectName,
                StringComparison.Ordinal))
            {
                return true;
            }

            if (!references.TryGetValue(current, out var direct))
            {
                continue;
            }

            foreach (var next in direct)
            {
                pending.Enqueue(next);
            }
        }

        return false;
    }

    [Fact]
    public void EveryProjectThatCanSeeTheCallSession_IsScannedByTheAuthorityGate()
    {
        // Arrange
        // The scan walks a fixed list of folders. A new project that references the Contact Center core can
        // reach CallSession and mutate its topology, and would be invisible to the gate unless the list grows
        // with it. This test is what forces the list to stay complete.
        var root = FindRepositoryRoot();
        var sourceRoot = Path.Combine(root, "src");
        var unscanned = new List<string>();

        // Act
        // Project files live exactly one folder below a source group, so the search stays out of 'obj' and
        // 'node_modules', which a recursive walk of the whole tree would spend minutes in.
        var projects = Directory
            .EnumerateDirectories(sourceRoot)
            .SelectMany(group => Directory.EnumerateDirectories(group))
            .SelectMany(project => Directory.EnumerateFiles(project, "*.csproj", SearchOption.TopDirectoryOnly));

        var references = projects.ToDictionary(
            project => Path.GetFullPath(project),
            ReadProjectReferences,
            StringComparer.OrdinalIgnoreCase);

        foreach (var project in references.Keys)
        {
            if (string.Equals(
                Path.GetFileNameWithoutExtension(project),
                CoreProjectName,
                StringComparison.Ordinal))
            {
                continue;
            }

            // A project reaches the call session through the whole reference closure, not only a direct
            // reference. Matching direct references alone let a project that references the Contact Center
            // module rather than its core stay out of the scan while still compiling against the type.
            if (!ReachesCore(project, references))
            {
                continue;
            }

            var folder = Path.GetDirectoryName(project);

            if (!_sourceProjectFolders.Any(scanned =>
                folder.Contains(scanned, StringComparison.OrdinalIgnoreCase)))
            {
                unscanned.Add(Path.GetRelativePath(root, project));
            }
        }

        // Assert
        Assert.True(
            unscanned.Count == 0,
            "These projects can reach CallSession but are not scanned by the topology authority gate: " +
            string.Join(", ", unscanned));
    }

    private static bool IsTopologyMember(MemberAccessExpressionSyntax memberAccess)
    {
        var name = memberAccess.Name.Identifier.ValueText;

        if (_topologyMemberNames.Contains(name, StringComparer.Ordinal))
        {
            return true;
        }

        // 'Participants' is also declared on the interaction, so it only counts as topology when it is reached
        // through the bridge.
        return string.Equals(name, nameof(Bridge.Participants), StringComparison.Ordinal) &&
            TopologyRootName(memberAccess.Expression) is not null;
    }

    /// <summary>
    /// Determines whether the expression is itself an access of a topology member, ignoring parentheses and
    /// the null-forgiving operator. This is what identifies aliasing and hand-off of the member itself.
    /// </summary>
    /// <param name="expression">The expression to examine.</param>
    /// <returns>The topology member name when the expression names one; otherwise <see langword="null"/>.</returns>
    private static string TopologyMemberAccessName(ExpressionSyntax expression)
    {
        var root = TopologyRootName(expression);

        if (root is null)
        {
            return null;
        }

        var outermost = OutermostMemberName(expression);

        if (outermost is null)
        {
            return null;
        }

        // Naming the member hands the collection out whole; a read that preserves element identity hands out
        // the topology objects themselves, which the receiver can then mutate with nothing left to root in. A
        // read that reduces to a scalar or projects to a different shape hands out neither, so it is not
        // aliasing and must not be reported or the gate becomes noise on ordinary read-only code.
        return _topologyMemberNames.Contains(outermost, StringComparer.Ordinal) ||
            _identityPreservingMemberNames.Contains(outermost, StringComparer.Ordinal)
            ? root
            : null;
    }

    /// <summary>
    /// Reports the member or method named at the outside of an expression, seeing through a null-conditional
    /// access and an invocation so the name examined is the one that decides what the expression hands out.
    /// </summary>
    /// <param name="expression">The expression to examine.</param>
    /// <returns>The outermost member name, or <see langword="null"/> when the expression names none.</returns>
    private static string OutermostMemberName(ExpressionSyntax expression)
    {
        var current = Unwrap(expression);

        if (current is ConditionalAccessExpressionSyntax conditionalAccess)
        {
            current = Unwrap(conditionalAccess.WhenNotNull);
        }

        if (current is InvocationExpressionSyntax invocation)
        {
            current = Unwrap(invocation.Expression);
        }

        return current switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            _ => null,
        };
    }

    /// <summary>
    /// Finds the topology member an expression's left spine reaches, so that mutations of the objects held
    /// inside the topology count as topology writes rather than as writes to unrelated objects.
    /// </summary>
    /// <param name="expression">The expression to examine.</param>
    /// <returns>The topology member name the expression is rooted in, or <see langword="null"/>.</returns>
    private static string TopologyRootName(ExpressionSyntax expression)
    {
        var current = Unwrap(expression);

        while (current is not null)
        {
            switch (current)
            {
                case MemberAccessExpressionSyntax memberAccess:
                    if (IsTopologyMember(memberAccess))
                    {
                        return memberAccess.Name.Identifier.ValueText;
                    }

                    current = Unwrap(memberAccess.Expression);
                    break;

                case ElementAccessExpressionSyntax elementAccess:
                    current = Unwrap(elementAccess.Expression);
                    break;

                case InvocationExpressionSyntax invocation:
                    current = Unwrap(invocation.Expression);
                    break;

                case MemberBindingExpressionSyntax binding:
                    var bound = binding.Name.Identifier.ValueText;

                    if (_topologyMemberNames.Contains(bound, StringComparer.Ordinal))
                    {
                        return bound;
                    }

                    // '?.' splits the chain, so the part before the operator lives on the enclosing node. The
                    // enclosing node must be the one whose right-hand side actually contains this binding:
                    // taking the nearest ancestor instead walks a chained 'a?.b?.c' back onto itself forever.
                    var conditional = binding
                        .Ancestors()
                        .OfType<ConditionalAccessExpressionSyntax>()
                        .FirstOrDefault(candidate => candidate.WhenNotNull.Span.Contains(binding.Span));

                    current = conditional is null ? null : Unwrap(conditional.Expression);
                    break;

                case ConditionalAccessExpressionSyntax conditionalAccess:
                    var whenNotNull = TopologyRootName(conditionalAccess.WhenNotNull);

                    if (whenNotNull is not null)
                    {
                        return whenNotNull;
                    }

                    current = Unwrap(conditionalAccess.Expression);
                    break;

                default:
                    return null;
            }
        }

        return null;
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    break;

                case PostfixUnaryExpressionSyntax postfix
                    when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    expression = postfix.Operand;
                    break;

                default:
                    return expression;
            }
        }
    }

    /// <summary>
    /// The reads that hand back the topology objects themselves rather than a scalar or a newly shaped value.
    /// Assigning one of these to a local is aliasing: the elements stay mutable and the left-spine walk has
    /// nothing to root the later mutation in.
    /// </summary>
    private static readonly string[] _identityPreservingMemberNames =
    [
        "Cast",
        "DefaultIfEmpty",
        "Distinct",
        "ElementAt",
        "ElementAtOrDefault",
        "Except",
        "First",
        "FirstOrDefault",
        "Last",
        "LastOrDefault",
        "OfType",
        "OrderBy",
        "OrderByDescending",
        "ParticipantsAt",
        "Single",
        "SingleOrDefault",
        "Skip",
        "Take",
        "ThenBy",
        "ThenByDescending",
        "ToArray",
        "ToList",
        "Where",
    ];

    /// <summary>
    /// The members that only read call-session topology. Anything not named here is treated as a mutation, so
    /// a mutator introduced later fails the gate instead of passing unnoticed.
    /// </summary>
    private static readonly string[] _readOnlyMemberNames =
    [
        "Any",
        "All",
        "Cast",
        "Contains",
        "Count",
        "DefaultIfEmpty",
        "Distinct",
        "ElementAt",
        "ElementAtOrDefault",
        "Except",
        "First",
        "FirstOrDefault",
        "GroupBy",
        "IndexOf",
        "Last",
        "LastOrDefault",
        "Max",
        "Min",
        "OfType",
        "OrderBy",
        "OrderByDescending",
        "ParticipantsAt",
        "Select",
        "SelectMany",
        "Single",
        "SingleOrDefault",
        "Skip",
        "Sum",
        "Take",
        "ThenBy",
        "ThenByDescending",
        "ToArray",
        "ToDictionary",
        "ToList",
        "Where",
    ];

    private static CallSession CreateSession()
    {
        return new CallSession
        {
            ItemId = "call-1",
            InteractionId = "interaction-1",
            ProviderCallId = "provider-call-1",
        };
    }

    private static string FindProjectorFile()
    {
        return Directory
            .EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src"), ProjectorFileName, SearchOption.AllDirectories)
            .Single(file => !IsGeneratedPath(file));
    }

    private static IEnumerable<string> EnumerateContactCenterSources()
    {
        var repositoryRoot = FindRepositoryRoot();

        foreach (var projectFolder in _sourceProjectFolders)
        {
            var root = Path.Combine(repositoryRoot, "src", projectFolder);

            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (IsGeneratedPath(file))
                {
                    continue;
                }

                yield return file;
            }
        }
    }

    private static bool IsGeneratedPath(string file)
    {
        var directory = Path.GetDirectoryName(file) ?? string.Empty;

        return directory.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            directory.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static int LineOf(SyntaxNode node)
        => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("The repository root could not be located.");
    }
}
