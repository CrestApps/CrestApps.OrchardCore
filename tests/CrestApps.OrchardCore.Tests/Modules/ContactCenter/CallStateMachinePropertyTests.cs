using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Modules.ContactCenter.StateMachine;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Property tests for the Contact Center call state machine (W5.6). Each test generates many reproducible
/// lifecycles, corrupts their delivery order with reordering, duplication and replay, ingests them through
/// the production <see cref="CrestApps.OrchardCore.ContactCenter.Core.Services.ProviderVoiceEventService"/>,
/// and asserts a property that must hold for every sequence rather than for one hand-picked example.
/// </summary>
public sealed class CallStateMachinePropertyTests
{
    private const int _sequenceCount = 400;

    private static readonly DateTime _baseUtc = new(2026, 4, 2, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Ingest_WithReorderedDuplicatedAndReplayedDeliveries_NeverLosesATerminalTransition()
    {
        await ForEachSequenceAsync(async (seed, emitted, delivered, harness, cancellationToken) =>
        {
            foreach (var delivery in delivered)
            {
                await harness.IngestAsync(delivery, cancellationToken);
            }

            if (!emitted.Exists(step => CallStateMachineSequenceGenerator.IsTerminal(step.State)))
            {
                return;
            }

            Assert.True(
                harness.Session is not null &&
                CallStateMachineSequenceGenerator.IsTerminal(harness.Session.State),
                Describe(
                    seed,
                    emitted,
                    delivered,
                    $"a terminal delivery was emitted but the session settled on '{harness.Session?.State}'"));

            Assert.Equal(1, harness.CountPublished(ContactCenterConstants.Events.CallEnded));
        });
    }

    [Fact]
    public async Task Ingest_WithReorderedDuplicatedAndReplayedDeliveries_NeverPublishesMoreThanOneCallEnded()
    {
        await ForEachSequenceAsync(async (seed, emitted, delivered, harness, cancellationToken) =>
        {
            foreach (var delivery in delivered)
            {
                await harness.IngestAsync(delivery, cancellationToken);
            }

            var ended = harness.CountPublished(ContactCenterConstants.Events.CallEnded);

            Assert.True(
                ended <= 1,
                Describe(seed, emitted, delivered, $"{ended} CallEnded events were published"));
        });
    }

    [Fact]
    public async Task Ingest_WithReorderedDuplicatedAndReplayedDeliveries_NeverRegressesTheLifecycleRank()
    {
        await ForEachSequenceAsync(async (seed, emitted, delivered, harness, cancellationToken) =>
        {
            var highestRank = -1;

            foreach (var delivery in delivered)
            {
                await harness.IngestAsync(delivery, cancellationToken);

                if (harness.Session is null)
                {
                    continue;
                }

                var rank = GetLifecycleRank(harness.Session.State);

                Assert.True(
                    rank >= highestRank,
                    Describe(
                        seed,
                        emitted,
                        delivered,
                        $"the lifecycle rank fell from {highestRank} to {rank} at delivery '{delivery}'"));

                highestRank = rank;
            }
        });
    }

    [Fact]
    public async Task Ingest_AfterATerminalStateIsReached_NeverChangesTheStateAgain()
    {
        await ForEachSequenceAsync(async (seed, emitted, delivered, harness, cancellationToken) =>
        {
            VoiceCallState? terminalState = null;

            foreach (var delivery in delivered)
            {
                await harness.IngestAsync(delivery, cancellationToken);

                if (harness.Session is null)
                {
                    continue;
                }

                if (terminalState.HasValue)
                {
                    Assert.True(
                        harness.Session.State == terminalState.Value,
                        Describe(
                            seed,
                            emitted,
                            delivered,
                            $"the terminal state '{terminalState}' changed to '{harness.Session.State}' at delivery '{delivery}'"));
                }
                else if (CallStateMachineSequenceGenerator.IsTerminal(harness.Session.State))
                {
                    terminalState = harness.Session.State;
                }
            }
        });
    }

    [Fact]
    public async Task Ingest_WhenTheSameDeliveryArrivesAgain_NeverPublishesAnExtraStateUpdate()
    {
        await ForEachSequenceAsync(async (seed, emitted, delivered, harness, cancellationToken) =>
        {
            var distinctDeliveries = new HashSet<string>(StringComparer.Ordinal);

            foreach (var delivery in delivered)
            {
                distinctDeliveries.Add(delivery.DeliveryId);
                await harness.IngestAsync(delivery, cancellationToken);

                // One delivery can justify at most one state update, so a redelivery that produced another one
                // means the duplicate was applied a second time rather than suppressed.
                var updates = harness.CountPublished(ContactCenterConstants.Events.CallSessionUpdated);

                Assert.True(
                    updates <= distinctDeliveries.Count,
                    Describe(
                        seed,
                        emitted,
                        delivered,
                        $"{updates} state updates were published for {distinctDeliveries.Count} distinct deliveries"));
            }
        });
    }

    [Fact]
    public async Task Ingest_WithReorderedDuplicatedAndReplayedDeliveries_KeepsDerivedCallFieldsConsistent()
    {
        await ForEachSequenceAsync(async (seed, emitted, delivered, harness, cancellationToken) =>
        {
            foreach (var delivery in delivered)
            {
                await harness.IngestAsync(delivery, cancellationToken);

                var session = harness.Session;

                if (session is null)
                {
                    continue;
                }

                string Reason(string failure) => Describe(seed, emitted, delivered, failure);

                Assert.True(Enum.IsDefined(session.State), Reason($"the state '{session.State}' is not a declared value"));
                Assert.True(session.TalkSeconds >= 0, Reason($"talk seconds were {session.TalkSeconds}"));
                Assert.True(
                    !session.AnsweredUtc.HasValue || session.StartedUtc.HasValue,
                    Reason("the call was answered but was never started"));

                if (CallStateMachineSequenceGenerator.IsTerminal(session.State))
                {
                    Assert.True(session.EndedUtc.HasValue, Reason($"'{session.State}' recorded no end time"));
                    Assert.False(session.IsOnHold, Reason($"'{session.State}' is still on hold"));
                    Assert.False(session.IsMuted, Reason($"'{session.State}' is still muted"));
                }

                // The session and the interaction must never disagree about whether the call is still live.
                // This is stated as a domain property rather than by restating the production status map, so a
                // change to that map is only a failure when it makes the two projections contradict each other.
                var sessionIsTerminal = CallStateMachineSequenceGenerator.IsTerminal(session.State);
                var interactionIsTerminal = harness.Interaction.Status is
                    InteractionStatus.Ended or
                    InteractionStatus.Failed or
                    InteractionStatus.Transferring;

                Assert.True(
                    sessionIsTerminal == interactionIsTerminal,
                    Reason($"the session is '{session.State}' while the interaction is '{harness.Interaction.Status}'"));
            }
        });
    }

    [Fact]
    public async Task Ingest_WhenTheWholeSequenceIsReplayed_ChangesNothing()
    {
        await ForEachSequenceAsync(async (seed, emitted, delivered, harness, cancellationToken) =>
        {
            foreach (var delivery in delivered)
            {
                await harness.IngestAsync(delivery, cancellationToken);
            }

            var settledState = harness.Session?.State;
            var settledEndedUtc = harness.Session?.EndedUtc;
            var settledAnsweredUtc = harness.Session?.AnsweredUtc;
            var settledEventCount = harness.PublishedEvents.Count;
            var settledStatus = harness.Interaction.Status;

            foreach (var delivery in delivered)
            {
                await harness.IngestAsync(delivery, cancellationToken);
            }

            var reason = Describe(seed, emitted, delivered, "replaying the whole sequence changed the outcome");

            Assert.Equal(settledState, harness.Session?.State);
            Assert.Equal(settledEndedUtc, harness.Session?.EndedUtc);
            Assert.Equal(settledAnsweredUtc, harness.Session?.AnsweredUtc);
            Assert.Equal(settledStatus, harness.Interaction.Status);
            Assert.True(harness.PublishedEvents.Count == settledEventCount, reason);
        });
    }

    [Fact]
    public async Task Ingest_WithAnyDeliveryOrder_SettlesOnTheSameTerminalOutcomeAsOrderedDelivery()
    {
        await ForEachSequenceAsync(async (seed, emitted, delivered, harness, cancellationToken) =>
        {
            foreach (var delivery in delivered)
            {
                await harness.IngestAsync(delivery, cancellationToken);
            }

            var ordered = new CallStateMachineHarness("Asterisk", "call-ordered", "agent-1", _baseUtc);

            foreach (var delivery in emitted)
            {
                await ordered.IngestAsync(delivery, cancellationToken);
            }

            var reason = Describe(
                seed,
                emitted,
                delivered,
                $"corrupted delivery settled on '{harness.Session?.State}' while ordered delivery settled on '{ordered.Session?.State}'");

            Assert.True(
                CallStateMachineSequenceGenerator.IsTerminal(harness.Session?.State ?? VoiceCallState.Planned) ==
                CallStateMachineSequenceGenerator.IsTerminal(ordered.Session?.State ?? VoiceCallState.Planned),
                reason);

            Assert.Equal(
                ordered.CountPublished(ContactCenterConstants.Events.CallEnded),
                harness.CountPublished(ContactCenterConstants.Events.CallEnded));
        });
    }

    // W1.3: no code path may produce a call ending without a recorded reason. A call that ended for an
    // unrecorded reason cannot be counted in outbound compliance reporting or abandon analytics later,
    // and no amount of downstream repair can recover a cause the machine never wrote.
    [Fact]
    public async Task Ingest_WheneverTheSessionEnds_AlwaysRecordsAHangupCause()
    {
        await ForEachSequenceAsync(async (seed, emitted, delivered, harness, cancellationToken) =>
        {
            foreach (var delivery in delivered)
            {
                await harness.IngestAsync(delivery, cancellationToken);
            }

            var session = harness.Session;

            if (session is null ||
                !CallStateMachineSequenceGenerator.IsTerminal(session.State))
            {
                return;
            }

            Assert.True(
                session.HangupCause.HasValue,
                Describe(
                    seed,
                    emitted,
                    delivered,
                    $"the session settled on terminal state '{session.State}' with no hangup cause"));

            Assert.True(
                session.EndedUtc.HasValue,
                Describe(seed, emitted, delivered, "a terminated session carried no end time"));
        });
    }

    [Fact]
    public void Generator_ProducesCorruptedSequencesRatherThanOrderedOnes()
    {
        var reordered = 0;
        var duplicated = 0;
        var terminated = 0;
        var skewed = 0;
        var mixedSequencing = 0;
        var regressing = 0;
        var competingTerminals = 0;

        for (var seed = 0; seed < _sequenceCount; seed++)
        {
            var random = new Random(seed);
            var emitted = CallStateMachineSequenceGenerator.EmitLifecycle(
                random,
                _baseUtc,
                seed % 2 == 0,
                SkewMillisecondsFor(seed),
                UnsequencedProbabilityFor(seed));
            var delivered = CallStateMachineSequenceGenerator.Deliver(random, emitted);

            if (emitted.Exists(step => step.SequenceNumber is null) &&
                emitted.Exists(step => step.SequenceNumber is not null))
            {
                mixedSequencing++;
            }

            for (var index = 1; index < emitted.Count; index++)
            {
                if (emitted[index].OccurredUtc < emitted[index - 1].OccurredUtc)
                {
                    skewed++;
                    break;
                }
            }

            if (delivered.Count > emitted.Count)
            {
                duplicated++;
            }

            if (delivered.Exists(step => step.DeliveryId.StartsWith("regression-", StringComparison.Ordinal)))
            {
                regressing++;
            }

            if (delivered.Exists(step => step.DeliveryId.StartsWith("competing-", StringComparison.Ordinal)))
            {
                competingTerminals++;
            }

            if (!DeliveredInEmissionOrder(emitted, delivered))
            {
                reordered++;
            }

            if (emitted.Exists(step => CallStateMachineSequenceGenerator.IsTerminal(step.State)))
            {
                terminated++;
            }
        }

        // Floors, so the property tests above cannot pass vacuously by generating tidy ordered lifecycles.
        Assert.True(reordered >= _sequenceCount / 2, $"only {reordered} of {_sequenceCount} sequences were reordered");
        Assert.True(duplicated >= _sequenceCount / 2, $"only {duplicated} of {_sequenceCount} sequences contained duplicates");
        Assert.True(terminated >= _sequenceCount / 2, $"only {terminated} of {_sequenceCount} sequences reached a terminal state");
        Assert.True(skewed >= _sequenceCount / 8, $"only {skewed} of {_sequenceCount} sequences carried timestamps that contradict the real order");
        Assert.True(mixedSequencing >= _sequenceCount / 16, $"only {mixedSequencing} of {_sequenceCount} sequences mixed sequenced and unsequenced deliveries");
        Assert.True(regressing >= _sequenceCount / 4, $"only {regressing} of {_sequenceCount} sequences carried a spurious backward report that defeats both staleness guards");
        Assert.True(competingTerminals >= _sequenceCount / 4, $"only {competingTerminals} of {_sequenceCount} sequences carried a second, competing terminal report");
    }

    private static async Task ForEachSequenceAsync(
        Func<int, List<CallStateMachineStep>, List<CallStateMachineStep>, CallStateMachineHarness, CancellationToken, Task> assert)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        for (var seed = 0; seed < _sequenceCount; seed++)
        {
            var random = new Random(seed);
            var emitted = CallStateMachineSequenceGenerator.EmitLifecycle(
                random,
                _baseUtc,
                seed % 2 == 0,
                SkewMillisecondsFor(seed),
                UnsequencedProbabilityFor(seed));
            var delivered = CallStateMachineSequenceGenerator.Deliver(random, emitted);
            var harness = new CallStateMachineHarness("Asterisk", "call-1", "agent-1", _baseUtc);

            await assert(seed, emitted, delivered, harness, cancellationToken);
        }
    }

    /// <summary>
    /// Selects the clock skew for a seed. Provider nodes do not share a clock, so two thirds of the generated
    /// lifecycles carry timestamps wide enough to contradict the order the events really occurred in.
    /// </summary>
    /// <param name="seed">The sequence seed.</param>
    /// <returns>The skew in milliseconds.</returns>
    private static int SkewMillisecondsFor(int seed)
    {
        return seed % 3 == 0
            ? 0
            : 400;
    }

    /// <summary>
    /// Selects how often a delivery arrives without a sequence number even though the provider stamps them,
    /// which models a provider that sequences only some of its event types.
    /// </summary>
    /// <param name="seed">The sequence seed.</param>
    /// <returns>The probability that a delivery carries no sequence number.</returns>
    private static double UnsequencedProbabilityFor(int seed)
    {
        return seed % 4 == 0
            ? 0.4
            : 0;
    }

    /// <summary>
    /// Determines whether the emitted lifecycle first reaches the ingestion pipeline in the order it occurred.
    /// Duplicates and injected deliveries are ignored, because counting them as reordering would let the
    /// reordering floor pass without a single delivery actually arriving out of order.
    /// </summary>
    /// <param name="emitted">The emitted lifecycle.</param>
    /// <param name="delivered">The deliveries in the order they reach the pipeline.</param>
    /// <returns><see langword="true"/> when the emitted order is preserved; otherwise, <see langword="false"/>.</returns>
    private static bool DeliveredInEmissionOrder(
        List<CallStateMachineStep> emitted,
        List<CallStateMachineStep> delivered)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var firstOccurrences = new List<string>();

        foreach (var delivery in delivered)
        {
            if (seen.Add(delivery.DeliveryId))
            {
                firstOccurrences.Add(delivery.DeliveryId);
            }
        }

        for (var index = 0; index < emitted.Count; index++)
        {
            if (index >= firstOccurrences.Count ||
                !string.Equals(firstOccurrences[index], emitted[index].DeliveryId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string Describe(
        int seed,
        List<CallStateMachineStep> emitted,
        List<CallStateMachineStep> delivered,
        string failure)
    {
        return $"Seed {seed}: {failure}.{Environment.NewLine}" +
            $"Emitted: {string.Join(" -> ", emitted)}{Environment.NewLine}" +
            $"Delivered: {string.Join(" -> ", delivered)}";
    }

    /// <summary>
    /// Orders the call states along the call lifecycle. This is the domain ordering the monotonicity property
    /// is stated against and is deliberately independent of the production staleness helper, so corrupting the
    /// production ordering is caught here instead of silently redefining the property.
    /// </summary>
    /// <param name="state">The state to rank.</param>
    /// <returns>The lifecycle position of the state.</returns>
    private static int GetLifecycleRank(VoiceCallState state)
    {
        return state switch
        {
            VoiceCallState.Planned => 0,
            VoiceCallState.Dialing => 1,
            VoiceCallState.Ringing => 1,
            VoiceCallState.Connected => 2,
            VoiceCallState.OnHold => 2,
            VoiceCallState.Ending => 3,
            _ => 4,
        };
    }

}
