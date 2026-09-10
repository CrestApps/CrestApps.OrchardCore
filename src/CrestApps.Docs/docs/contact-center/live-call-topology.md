---
sidebar_position: 12
---

# Live call topology

A voice interaction is not one line between two people. A customer calls, an agent answers, the agent puts the customer on hold and consults a specialist, a supervisor listens in, the specialist joins and the original agent drops off. Every one of those moments changes *who is on the call*, and a contact center that cannot describe that shape cannot report on it, supervise it, or recover it after a failure.

Contact Center describes the shape of a live call with a topology on the call session rather than with a handful of scalar fields.

## What the topology contains

| Concept | What it records |
| --- | --- |
| **Leg** | One party's connection to the call: the provider leg identifier, the part the party plays (customer, agent, consult, supervisor, external), a normalized lifecycle status, the address, and the times the leg started, answered, and ended. |
| **Bridge** | The media the parties share. It carries a participant list where each entry has a join time and, once the party leaves, a leave time. |
| **Consult** | A private call an agent placed before deciding whether to complete a warm transfer, with its own lifecycle from initiated through connected to completed or cancelled. |
| **Monitor session** | A live supervisor engagement — monitor, whisper, or barge — with the supervisor, the agent being supervised, and the times it started and ended. |
| **Retained bridge** | A bridge the call has been moved off. It keeps its full membership history so a media move does not erase who was on the call before it. |
| **Relationship** | A typed link to another call: transferred from, transferred to, consult of, conferenced with, or callback of. |

## Membership history is append-only

When a party leaves the bridge, its entry is **not removed**. The leave time is stamped on the entry it already had.

That is what makes `Bridge.ParticipantsAt(instant)` able to answer "who was on this call at 14:32?" long after the call ended. A list that is edited in place can only ever answer "who is on this call now", which is useless the moment the call is over — and after-the-fact review of a disputed call is exactly when the question gets asked.

```csharp
var atTheTimeOfTheComplaint = callSession.ParticipantsAt(complaintUtc);
```

`CallSession.ParticipantsAt` searches the current bridge **and** every retained one. When a provider reports a new topology identifier the parties have been moved to different media, so the previous bridge is closed and retained in `CallSession.PriorBridges` rather than continued under the new identifier. Reusing the old bridge would attribute the new membership window to media that no longer exists, and dropping it would erase the earlier window entirely.

## Reported counts and observed membership are kept apart

Some providers publish only a participant count for a conference; they do not say who those parties are. `Bridge.ReportedParticipantCount` carries that count, separately from `Bridge.Participants`.

Fabricating participant entries to make the observed list match a reported count would make the membership history a fiction — it would show parties that were never identified, at times that were never observed. `CallSession.ParticipantCount` prefers the provider's reported count when there is one, so live displays stay accurate, while the membership history stays factual.

## One writer

`CallTopologyProjector` is the only place in the product that mutates legs, bridges, bridge membership, consults, monitor sessions, or relationships. Every service that observes a change to the call — the provider event stream, the transfer service, the monitoring service, the agent-connect command executor — calls into it.

Keeping the rules in one place is what makes them enforceable: that a leg cannot end before it started, that a participant cannot leave before it joined, that a destroyed bridge has no live members, that a supervisor cannot monitor themselves, and that the same supervisor cannot hold two live engagements on one call. A build gate scans every Contact Center, Telephony, and provider source file and fails the build if any of them mutates the topology directly.

## Ending a leg says why it ended

`CallTopologyProjector.EndLeg` sets the leg's status to `Failed` when the leg never reached an answered time, and to `Ended` when it did.

That single distinction is what lets abandon and no-answer reporting separate from normal hang-ups without any provider-specific vocabulary leaking into reports.

A leg's end time is clamped so it never precedes its start. A provider may stamp a hangup behind the state change that preceded it, and a leg started from the application clock can be ended from the provider's, so the two timestamps can invert without anything being wrong upstream. An inverted leg is not a fact, and persisting one would be rejected by the store, leaving the call stuck mid-teardown with its agent still occupied.

When the call itself reaches a terminal state, **every** leg still open is ended and the bridge is destroyed — not only the leg the provider named on the hangup. A terminal call session accepts no further provider deliveries, so a leg left open at that moment stays open permanently and the bridge goes on claiming a party who has already gone.

## Supervisor engagements

Starting a monitor, whisper, or barge records a `MonitorSession` on the call session. Barge — and only barge — also joins the supervisor to the bridge as a party, because barge puts the supervisor into the conversation while monitoring and whispering let the supervisor hear the call without being one of the parties on it.

A supervisor is always a user but is not always an agent, so the engagement records both identifiers: `SupervisorUserId` is the identity the engagement is started and stopped under, and `SupervisorAgentId` is the supervisor's agent profile when they have one. Keeping them apart is what makes the "a supervisor cannot monitor their own agent leg" rule enforceable, because that rule compares the supervisor against `CallSession.AgentId`, which is an agent-profile identifier — comparing a user identifier against it could never match, and the rule would never fire.

A second engagement by the same supervisor on the same call is refused. Two engagements by one supervisor would be indistinguishable from each other, so a later stop would release an arbitrary one and leave the other listening.

When the provider does not report a leg identifier for the engagement, the monitor session is still recorded with a null provider leg. The engagement is on the record, but a barge cannot place the supervisor on the bridge without a leg to place.

## Consultative transfers

A consultative (warm) transfer records a `ConsultCall` on the source session and a typed relationship to the destination. This is what lets a supervisor see that a customer is on hold while their agent talks to someone else, and lets reporting tell a completed warm transfer apart from a consult the agent abandoned.

When a consult reaches a terminal state, its leg is ended and its bridge membership released, so an abandoned consult never leaves a leg open on the topology.
