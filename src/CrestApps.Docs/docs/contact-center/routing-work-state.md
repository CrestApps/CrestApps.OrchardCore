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

## Upgrading

No backfill job is required. Work that was already in flight when the feature was upgraded has no work state document yet, so the first read or mutation adopts the projected fields the activity already carries. Reporting that work as unassigned with no attempts would re-offer work an agent already holds and reset the dialer's attempt cap, so adoption is the default rather than an option.

## Enforcement

`ContactCenterWorkStateAuthorityTests` scans every Contact Center, Telephony, and provider source file and fails the build if any of them writes one of the ten routing-owned fields onto a CRM activity outside `ContactCenterWorkStateProjector`, which is the single definition of what the read model contains. The same suite proves the behaviour against a real database: a reservation running while the CRM holds an earlier read of the same activity commits without either writer conflicting and without either write being lost.

Two deviations are recorded rather than hidden. `ActivityStatus` remains CRM-owned and is not extracted. `AgentWorkspaceEndpoints` still reads the projected `AssignedToId` as a fallback beside the authoritative interaction agent, and `ContactCenterReportingService` reads the projected columns in bulk reporting queries.
