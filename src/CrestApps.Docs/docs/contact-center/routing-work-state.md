---
sidebar_label: Routing Work State
title: Routing Work State and the CRM Activity
description: Which document owns assignment, reservation, and attempt state for contact center work, why the CRM activity still carries the same fields, and which of the two a routing decision must read.
---

The CRM `OmnichannelActivity` is the universal work item. It is created by the CRM, edited by people in the admin UI, listed and filtered by CRM screens, and reported on by the enterprise report catalog. The contact center also needs to record, for the same piece of work, who currently holds it, which reservation offered it, when that offer expires, and how many dial attempts it has consumed.

Those two responsibilities used to share one document, and that is a problem rather than a convenience. The reservation loop and a person editing the activity in the admin UI both wrote the same optimistic-concurrency document, so one of the two writes was lost, or the routing commit failed with a concurrency exception. Losing a routing commit strands live work with a caller on the line.

## Who owns what

| Concern | Owner | Document |
| --- | --- | --- |
| Assignment status, reservation identity and expiry, reserving and assigned agent, attempt count | Contact Center | `ContactCenterWorkState` |
| Activity status, terminal reason, scheduling, subject, contact resolution, disposition, notes | CRM | `OmnichannelActivity` |
| Communication history for one attempt | Contact Center | `Interaction` |

`ContactCenterWorkState` is keyed one-to-one to the activity by a unique index on `ActivityItemId`, and it lives in the Contact Center collection. Every contact center writer mutates it through `IContactCenterWorkStateService`:

```csharp
await _workStateService.MutateAsync(activityItemId, workState =>
{
    workState.AssignmentStatus = ActivityAssignmentStatus.Reserved;
    workState.ReservationId = reservation.ItemId;
    workState.ReservedById = agent.UserId;
    workState.ReservationExpiresUtc = reservation.ExpiresUtc;
}, cancellationToken);
```

The routing transaction commits without touching the CRM activity at all.

## The activity still carries the same fields

`OmnichannelActivity` keeps `AssignmentStatus`, `ReservationId`, `ReservedById`, `ReservedByUsername`, `ReservedUtc`, `ReservationExpiresUtc`, `AssignedToId`, `AssignedToUsername`, `AssignedToUtc`, and `Attempts`, but only as a **read model**. They are reconciled by `ContactCenterWorkStateActivityProjection` after the routing transaction has committed, in its own scope, with a bounded retry on conflict.

They were retained rather than deleted because they are load-bearing outside routing. `OmnichannelActivityAuthorizationHandler` decides activity ownership from `AssignedToId`, and the CRM activity list, bulk-manage filters, and enterprise reports query the same columns through `OmnichannelActivityIndex`. Deleting them would either remove CRM function or invert the layering by making the CRM query a Contact Center store.

Because the read model is reconciled after the fact, it can lag. That leads to one rule.

## Which one to read

Read `IContactCenterWorkStateService` for anything that decides routing: whether work may be offered, who holds it, whether an offer has expired, and whether the dialer's attempt cap has been reached. Read the activity's projected columns only for CRM presentation and bulk reporting, where a per-row work state lookup would be a query per row.

`IContactCenterActivityWriter` is the counterpart for CRM-owned fields. Contact center code that has to set an activity's terminal status schedules the write through the writer rather than writing the activity inside the routing transaction:

```csharp
await _activityWriter.ScheduleUpdateAsync(activityItemId, activity =>
{
    activity.Status = ActivityStatus.Cancelled;
    activity.TerminalReasonCode = reasonCode;
    activity.CompletedUtc = endedUtc;
}, cancellationToken);
```

The activity is therefore written twice for a terminal transition — once to reconcile the read model and once to apply the CRM-owned status — and both writes happen after the routing transaction has committed rather than inside it.

## When the CRM ends an activity

An activity can also leave the routable set from the CRM side: purged or bulk-purged from the admin, cancelled, completed or failed by hand, or deleted. The CRM never references the contact center, so `ContactCenterActivityRoutabilityHandler` listens on the Omnichannel activity manager's catalog handlers and, after the change commits, asks `IQueuedWorkWithdrawalService` to withdraw the activity's queued work. "Routable" is the opposite of `ActivityStatus.IsTerminal()` (Completed, Cancelled, Failed, Purged), plus the activity still existing.

- **Waiting** work is removed from its queue (`QueueItemStatus.Removed`) and its work state released.
- **Ringing** work, an offer that has not been answered yet, has the offer revoked through `IActivityReservationService.CompensateAsync`, which releases the agent back to Available and removes the item in the same step a suppressed dial uses.
- **Accepted** work is left with the agent. They may be on the call, and pulling the work out from under them would drop a live customer. Their own completion settles the queue item, and the CRM refuses a disposition on an activity that has already finished.

Every withdrawal is recorded once as `QueueItemWithdrawn` through `IContactCenterAuditRecorder`, naming the actor: the agent when the activity's assigned agent closed it, a supervisor when anyone else did (for a purge, the user in `PurgedById`), and the system when no user was involved.

Routing also withdraws work itself, as a safety net for items stranded before this existed or by a writer that bypassed the activity manager. Before offering the item at the head of a queue, `ActivityAssignmentService` asks the withdrawal service whether that item's activity is still routable. When it is not, the item is withdrawn as the system and the next item is considered, so dead work heals on the next sweep instead of being offered on every pass.

## Upgrading

No backfill job is required. Work that was already in flight when the feature was upgraded has no work state document yet, so the first read or mutation adopts the projected fields the activity already carries. Reporting that work as unassigned with no attempts would re-offer work an agent already holds and reset the dialer's attempt cap, so adoption is the default rather than an option.

## Enforcement

`ContactCenterWorkStateAuthorityTests` scans every Contact Center, Telephony, and provider source file and fails the build if any of them stores into one of the ten routing-owned fields of a CRM activity outside `ContactCenterWorkStateProjector`, which is the single definition of what the read model contains. Every storing form is covered: assignment, compound assignment, increment and decrement — which is how the attempt count is written — and object initializers, including target-typed ones. The scan classifies the receiver of a write rather than the field name, because a queue item and a provider command both carry a reservation identifier they legitimately own. It does so by compiling the sources and asking the compiler for the receiver's type, and it is fail-closed: a receiver whose type cannot be resolved is reported as a violation rather than assumed to be some other document, because a scan that recognizes receivers by the shape of the identifier naming them proves only that nothing writes routing state onto a receiver it happened to recognize — aliasing the activity through a single local defeats it. Known-positive and known-negative controls sit either side of that rule: the projector must be reported for all ten fields and each report must have recognized the receiver as the CRM activity rather than merely failed to resolve it, and the queue-item and provider-command writers must not be reported at all. The same suite proves the behaviour against a real database: a reservation running while the CRM holds an earlier read of the same activity commits without either writer conflicting and without either write being lost.

Two deviations are recorded rather than hidden. `ActivityStatus` remains CRM-owned and is not extracted. `AgentWorkspaceEndpoints` still reads the projected `AssignedToId` as a fallback beside the authoritative interaction agent, and `ContactCenterReportingService` reads the projected columns in bulk reporting queries.
