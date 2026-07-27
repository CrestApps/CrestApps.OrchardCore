using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.StateMachine;

/// <summary>
/// Generates randomized but reproducible call-lifecycle deliveries for the state machine property tests.
/// A sequence is produced in two stages: a plausible provider lifecycle is emitted first, then the delivery
/// order is corrupted with reordering, duplication and replay, which is exactly what an at-least-once
/// provider webhook or a multi-node listener can do to an otherwise well-formed lifecycle.
/// </summary>
public static class CallStateMachineSequenceGenerator
{
    private static readonly VoiceCallState[] _terminalStates =
    [
        VoiceCallState.Ended,
        VoiceCallState.Failed,
        VoiceCallState.NoAnswer,
        VoiceCallState.Rejected,
        VoiceCallState.Canceled,
        VoiceCallState.Transferred,
    ];

    private static readonly VoiceCallState[] _earlyStates =
    [
        VoiceCallState.Planned,
        VoiceCallState.Dialing,
        VoiceCallState.Ringing,
    ];

    private static readonly RecordingState[] _recordingStates =
    [
        RecordingState.None,
        RecordingState.Recording,
        RecordingState.Paused,
        RecordingState.Stopped,
    ];

    /// <summary>
    /// Emits the lifecycle a well-behaved provider would report, in the order the provider observed it.
    /// The result is the truth the invariants are evaluated against, regardless of how it is later delivered.
    /// </summary>
    /// <param name="random">The seeded random source.</param>
    /// <param name="baseUtc">The instant the first delivery is stamped with.</param>
    /// <param name="emitSequenceNumbers">Whether the simulated provider stamps sequence numbers.</param>
    /// <param name="skewMilliseconds">
    /// The clock skew, in milliseconds, applied to individual deliveries. Provider nodes do not share a clock,
    /// so the timestamp a delivery carries can disagree with the order the events actually occurred in. A skew
    /// wider than the spacing between deliveries makes the reported order contradict the real one.
    /// </param>
    /// <param name="unsequencedProbability">
    /// The probability that a delivery carries no sequence number even though the provider stamps them, which
    /// models a provider that sequences only some of its event types.
    /// </param>
    /// <returns>The emitted lifecycle, in the order the events really occurred.</returns>
    public static List<CallStateMachineStep> EmitLifecycle(
        Random random,
        DateTime baseUtc,
        bool emitSequenceNumbers,
        int skewMilliseconds = 0,
        double unsequencedProbability = 0)
    {
        ArgumentNullException.ThrowIfNull(random);

        var steps = new List<CallStateMachineStep>();
        var state = random.Next(3) switch
        {
            0 => VoiceCallState.Planned,
            1 => VoiceCallState.Dialing,
            _ => VoiceCallState.Ringing,
        };

        var length = random.Next(1, 9);
        Add(steps, random, baseUtc, emitSequenceNumbers, skewMilliseconds, unsequencedProbability, state);

        for (var index = 0; index < length; index++)
        {
            state = NextState(random, state);
            Add(steps, random, baseUtc, emitSequenceNumbers, skewMilliseconds, unsequencedProbability, state);

            if (IsTerminal(state))
            {
                break;
            }
        }

        // Three quarters of the generated lifecycles reach a terminal state so the terminal properties are
        // exercised densely, while the remainder keep proving the machine tolerates a call that is still live.
        if (!IsTerminal(steps[steps.Count - 1].State) && random.Next(4) != 0)
        {
            Add(
                steps,
                random,
                baseUtc,
                emitSequenceNumbers,
                skewMilliseconds,
                unsequencedProbability,
                _terminalStates[random.Next(_terminalStates.Length)]);
        }

        return steps;
    }

    /// <summary>
    /// Corrupts the delivery order of an emitted lifecycle with reordering, duplication and replay.
    /// </summary>
    /// <param name="random">The seeded random source.</param>
    /// <param name="emitted">The emitted lifecycle to derive deliveries from.</param>
    /// <returns>The deliveries in the order the ingestion pipeline receives them.</returns>
    public static List<CallStateMachineStep> Deliver(Random random, List<CallStateMachineStep> emitted)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(emitted);

        var deliveries = new List<CallStateMachineStep>(emitted);
        deliveries.AddRange(EmitLateRegressions(random, emitted));
        deliveries.AddRange(EmitCompetingTerminals(random, emitted));

        var duplicateCount = random.Next(0, emitted.Count + 1);

        for (var index = 0; index < duplicateCount; index++)
        {
            deliveries.Add(emitted[random.Next(emitted.Count)]);
        }

        // A full replay of the lifecycle, which is what a provider redelivery or an outbox reprocess produces.
        if (random.Next(3) == 0)
        {
            deliveries.AddRange(emitted);
        }

        for (var index = deliveries.Count - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (deliveries[index], deliveries[swap]) = (deliveries[swap], deliveries[index]);
        }

        return deliveries;
    }

    /// <summary>
    /// Emits deliveries that report an earlier lifecycle state while carrying the freshest timestamp and the
    /// highest sequence number seen so far. These are not part of the emitted truth: they are the spurious
    /// backward reports a provider produces when a second leg starts alerting, when a reconciliation sweep
    /// re-reports a superseded state, or when a normalizer mislabels an event. Because they defeat both
    /// staleness guards, they are the only deliveries that reach the lifecycle-ordering guard, so without them
    /// that guard is never exercised.
    /// </summary>
    /// <param name="random">The seeded random source.</param>
    /// <param name="emitted">The emitted lifecycle to regress against.</param>
    /// <returns>The spurious backward deliveries, which may be empty.</returns>
    private static List<CallStateMachineStep> EmitLateRegressions(Random random, List<CallStateMachineStep> emitted)
    {
        var regressions = new List<CallStateMachineStep>();

        if (random.Next(2) == 0 || !emitted.Exists(step => IsConnectedOrLater(step.State)))
        {
            return regressions;
        }

        var latestUtc = emitted[0].OccurredUtc;
        long highestSequence = 0;

        foreach (var step in emitted)
        {
            if (step.OccurredUtc > latestUtc)
            {
                latestUtc = step.OccurredUtc;
            }

            if (step.SequenceNumber.HasValue && step.SequenceNumber.Value > highestSequence)
            {
                highestSequence = step.SequenceNumber.Value;
            }
        }

        var count = random.Next(1, 3);

        for (var index = 0; index < count; index++)
        {
            regressions.Add(new CallStateMachineStep
            {
                DeliveryId = $"regression-{index}",
                State = _earlyStates[random.Next(_earlyStates.Length)],
                OccurredUtc = latestUtc.AddMilliseconds(1000 + index),
                SequenceNumber = highestSequence + index + 1,
                IsMuted = null,
                RecordingState = null,
                ParticipantCount = null,
            });
        }

        return regressions;
    }

    private static bool IsConnectedOrLater(VoiceCallState state)
    {
        return state is VoiceCallState.Connected or
            VoiceCallState.OnHold or
            VoiceCallState.Ending ||
            IsTerminal(state);
    }

    /// <summary>
    /// Emits a second, different terminal report for a call that already ended. Providers do this routinely:
    /// a hangup is reported by the media path while a reconciliation sweep independently reports the call as
    /// failed or transferred. Only the first terminal report may take effect, so these deliveries are the ones
    /// that exercise terminal absorption; without them every later delivery is rejected by an earlier guard.
    /// </summary>
    /// <param name="random">The seeded random source.</param>
    /// <param name="emitted">The emitted lifecycle to compete with.</param>
    /// <returns>The competing terminal deliveries, which may be empty.</returns>
    private static List<CallStateMachineStep> EmitCompetingTerminals(Random random, List<CallStateMachineStep> emitted)
    {
        var competing = new List<CallStateMachineStep>();
        var settled = emitted.Find(step => IsTerminal(step.State));

        if (settled is null || random.Next(2) == 0)
        {
            return competing;
        }

        var latestUtc = emitted[0].OccurredUtc;
        long highestSequence = 0;

        foreach (var step in emitted)
        {
            if (step.OccurredUtc > latestUtc)
            {
                latestUtc = step.OccurredUtc;
            }

            if (step.SequenceNumber.HasValue && step.SequenceNumber.Value > highestSequence)
            {
                highestSequence = step.SequenceNumber.Value;
            }
        }

        var count = random.Next(1, 3);

        for (var index = 0; index < count; index++)
        {
            var state = _terminalStates[random.Next(_terminalStates.Length)];

            if (state == settled.State)
            {
                state = state == VoiceCallState.Ended
                    ? VoiceCallState.Failed
                    : VoiceCallState.Ended;
            }

            competing.Add(new CallStateMachineStep
            {
                DeliveryId = $"competing-{index}",
                State = state,
                OccurredUtc = latestUtc.AddMilliseconds(2000 + index),
                SequenceNumber = highestSequence + 100 + index,
                IsMuted = null,
                RecordingState = null,
                ParticipantCount = null,
            });
        }

        return competing;
    }

    /// <summary>
    /// Determines whether the supplied state is one of the terminal call states.
    /// </summary>
    /// <param name="state">The state to inspect.</param>
    /// <returns><see langword="true"/> when the state is terminal; otherwise, <see langword="false"/>.</returns>
    public static bool IsTerminal(VoiceCallState state)
    {
        return Array.IndexOf(_terminalStates, state) >= 0;
    }

    private static void Add(
        List<CallStateMachineStep> steps,
        Random random,
        DateTime baseUtc,
        bool emitSequenceNumbers,
        int skewMilliseconds,
        double unsequencedProbability,
        VoiceCallState state)
    {
        var skew = skewMilliseconds == 0
            ? 0
            : random.Next(-skewMilliseconds, skewMilliseconds + 1);

        steps.Add(new CallStateMachineStep
        {
            DeliveryId = $"delivery-{steps.Count}",
            State = state,
            OccurredUtc = baseUtc.AddMilliseconds(((steps.Count + 1) * 250) + skew),
            SequenceNumber = emitSequenceNumbers && random.NextDouble() >= unsequencedProbability
                ? steps.Count + 1
                : null,
            IsMuted = random.Next(3) == 0 ? random.Next(2) == 0 : null,
            RecordingState = random.Next(3) == 0 ? _recordingStates[random.Next(_recordingStates.Length)] : null,
            ParticipantCount = random.Next(4) == 0 ? random.Next(0, 4) : null,
        });
    }

    private static VoiceCallState NextState(Random random, VoiceCallState current)
    {
        return current switch
        {
            VoiceCallState.Planned => random.Next(2) == 0
                ? VoiceCallState.Dialing
                : VoiceCallState.Ringing,
            VoiceCallState.Dialing => random.Next(4) == 0
                ? _terminalStates[random.Next(_terminalStates.Length)]
                : VoiceCallState.Ringing,
            VoiceCallState.Ringing => random.Next(3) == 0
                ? _terminalStates[random.Next(_terminalStates.Length)]
                : VoiceCallState.Connected,
            VoiceCallState.Connected => random.Next(3) switch
            {
                0 => VoiceCallState.OnHold,
                1 => VoiceCallState.Ending,
                _ => _terminalStates[random.Next(_terminalStates.Length)],
            },
            VoiceCallState.OnHold => random.Next(3) == 0
                ? _terminalStates[random.Next(_terminalStates.Length)]
                : VoiceCallState.Connected,
            VoiceCallState.Ending => _terminalStates[random.Next(_terminalStates.Length)],
            _ => current,
        };
    }
}
