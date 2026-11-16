---
sidebar_label: Service Levels
title: Service Levels
description: "The shared Service Levels building block: policies with a target per metric and priority counted on business hours, a stored timer per record and metric that pauses and resumes exactly, the tracker, the monitor that raises warnings and breaches, scopes, observers, policy usage and the recipe and deployment step."
user_manual: user-manual/administration/service-levels
---

**Service Levels** measures how long work takes against targets: how soon a record must first be answered, answered again, or resolved, at its priority, counted on a business hours schedule or around the clock. Any module can use it. Work Orders keeps its response and resolution timers in it.

| Feature | Id | Depends on |
| --- | --- | --- |
| Service Levels | `CrestApps.OrchardCore.ServiceLevels` | Business Hours |

| Project | Holds |
| --- | --- |
| `CrestApps.OrchardCore.ServiceLevels.Abstractions` | `ServiceLevelPolicy`, `ServiceLevelTarget`, `ServiceLevelTimer`, `ServiceLevelTimerState`, `ServiceLevelMetrics`, `ServiceLevelStart`, `IServiceLevelTracker`, `IServiceLevelObserver`, `IServiceLevelScopeProvider`, `IServiceLevelPolicyManager`, `IServiceLevelPolicyUsageProvider`, the registration helpers and `ServiceLevelsPermissions` |
| `CrestApps.OrchardCore.ServiceLevels.Core` | The YesSql stores and indexes (collection `ServiceLevels`), `DefaultServiceLevelTracker`, `ServiceLevelMonitor`, `ServiceLevelTargetGrid` and `AddServiceLevelsCore()` |
| `CrestApps.OrchardCore.ServiceLevels` (module) | **Content > Service Levels**, the policy editor, the monitor background task, the migration and the recipe and deployment step |

## Policies

A `ServiceLevelPolicy` belongs to one **scope**, the kind of work it is for. The scope names the metrics and the priorities, so the policy editor shows a grid of minutes per metric and priority, plus an **Any other priority** row whose targets have an empty `Priority`. The scope is fixed once the policy exists.

| Member | Meaning |
| --- | --- |
| `Scope` | The name of a registered `IServiceLevelScopeProvider`, such as `WorkOrders` |
| `Targets` | `{ Metric, Priority, Minutes }`; `FindTarget(metric, priority)` returns the priority's target, else the metric's target for every priority. `Priority` is the key the scope gives (`ServiceLevelOption.Key`); a target written with a priority's name, as a recipe that can't know a site's ids writes it, is keyed by that priority when the policy is saved (`ServiceLevelPriorityKeys`), so renaming a priority keeps its targets |
| `HoursScheduleId` | A schedule from any Business Hours source; empty counts around the clock |
| `WarningPercent` | 1 to 99; 80 by default |
| `Properties` | Settings other modules keep on the policy (Work Orders keeps `WorkOrderServiceLevelSettings` with its pause statuses) |

The built-in metrics are `FirstResponse`, `NextResponse` and `Resolution` (`ServiceLevelMetrics`); a scope may add its own.

The editor is built through `IDisplayManager<ServiceLevelPolicy>`. A module adds a card for its scope with its own `DisplayDriver<ServiceLevelPolicy>` that returns `null` for other scopes and stores its values in `Properties`. A policy that a record still uses can't be deleted: each module says what uses a policy through `IServiceLevelPolicyUsageProvider`.

### Registering a scope

```csharp
public sealed class InspectionServiceLevelScope : IServiceLevelScopeProvider
{
    public string Name => "Inspections";

    public LocalizationSource DisplayName => _displayName;

    public Task<IReadOnlyList<ServiceLevelOption>> GetMetricsAsync(CancellationToken cancellationToken = default) => ...;

    public Task<IReadOnlyList<ServiceLevelOption>> GetPrioritiesAsync(CancellationToken cancellationToken = default) => ...;
}

services.AddServiceLevelScope<InspectionServiceLevelScope>();
services.AddServiceLevelObserver<InspectionServiceLevelObserver>();
services.AddServiceLevelPolicyUsage<InspectionDeskPolicyUsage>();
```

With one scope, **Add Policy** opens the editor for it; with several, it lists them.

## Timers

The module that owns a record tells `IServiceLevelTracker` what happens to it, while holding the record's own lock:

| Method | When |
| --- | --- |
| `StartAsync(ServiceLevelStart)` | The record is created, or a repeating metric starts a new cycle (a customer replies). An active timer of the same metric is cancelled first. `Paused` starts it paused; `AlreadyWarned` and `AlreadyBreached` mark a timer copied from another system so the monitor doesn't raise those again; `DueUtc`, `PausedSinceUtc` and `CompletedUtc` bring the due time, the start of a wait and an answer it already had. Starting never tells the observers. |
| `PauseAsync` / `ResumeAsync` | The record waits (on hold, waiting for the customer) and stops waiting |
| `CompleteAsync` | The work was done; the business time used is kept in `ElapsedSeconds`. Every running cycle of the metric is completed, and one completed after its due time is announced as breached first |
| `ReopenAsync` | The record was reopened after it was answered; its newest completed cycle counts on from the time it had used, keeping a warning or breach already raised |
| `CancelAsync` | The record is no longer measured (closed as spam, deleted) |
| `RetargetAsync` | The priority or the policy changed; the time already used counts against the new target, and a paused timer gets due times as if it went on counting now |
| `GetTimersAsync` | The record's timers, newest first |

A timer keeps the business seconds it has used (`UsedSeconds`), so a pause, a resume or a new target never adds wall-clock time to a deadline, and the warning point moves with the paused time.

## The monitor

A background task runs `ServiceLevelMonitor` every minute. It reads the running timers that are due in pages ordered by due time (never every record), marks each breach and warning with a concurrency-checked save, and only after that save calls the `IServiceLevelObserver`s (`WarningAsync`, `BreachedAsync`). Completing a timer calls `CompletedAsync`. The monitor marks each due timer in a transaction of its own, reading it right before the write and marking it only while it is still due, so an owner's change (completing, pausing, retargeting) always wins and an owner's request never fails on the monitor. One observer that fails is logged and doesn't stop the others.

When the hours of a Business Hours schedule change, or the schedule is deleted, a copy of the old hours is kept (through `IBusinessHoursScheduleObserver`), and a background step counts each running timer's time up to the change on the old hours and works out its due times on the new ones, a batch at a time. A schedule changed after its timers started so that it is never open (no open hour in the coming year) doesn't freeze them: `ServiceLevelClock` counts such a timer around the clock, the time used and the time left alike, sets `ServiceLevelTimer.NoBusinessHours`, and the schedule-change step tells the observers once (`IServiceLevelObserver.NoBusinessHoursAsync`; Tickets notes it in the ticket's history and shows **No business hours** by the target). It counts on the schedule again once the schedule opens. A policy is refused when its schedule no longer exists or is never open, when its scope isn't registered, when its id isn't valid or its name is longer than 255 characters, and a policy with running or paused timers can't be deleted.

Observers feed the owner's own events: Work Orders raises its **Work Order Service Level Warning** and **Breached** workflow events from one.

## Recipe and deployment step

The `ServiceLevelPolicies` step creates or updates policies, matched by `ItemId`, then by `Scope` and `Name`:

```json
{
  "name": "ServiceLevelPolicies",
  "Policies": [
    {
      "Name": "Standard service",
      "Scope": "WorkOrders",
      "HoursScheduleId": "",
      "WarningPercent": 75,
      "Targets": [
        { "Metric": "FirstResponse", "Priority": "Urgent", "Minutes": 120 },
        { "Metric": "Resolution", "Priority": "Urgent", "Minutes": 480 },
        { "Metric": "FirstResponse", "Minutes": 480 }
      ],
      "Properties": {
        "WorkOrderServiceLevelSettings": { "PauseStatuses": [ "OnHold" ] }
      }
    }
  ]
}
```

The **Service level policies** deployment step exports every policy in the same shape. Both have schemas for CrestApps Recipes.

## Data set

With Data Pipelines Core on, the **Service levels** data source has a `ServiceLevelTimers` data set: one row per timer with its record, metric, policy, priority, state, target, start, warning and due times, end, and whether it was met, warned or breached, with the business minutes it used. Those who manage policies see every timer. Others see the timers of records a module's `IServiceLevelTimerAccessProvider` (registered with `AddServiceLevelTimerAccessProvider`) lets them see: it gets a batch of record ids of its kind and returns the ones the user may see. Tickets registers one for its tickets and Work Orders one for work orders (whoever may view work orders); the timers of a kind with no provider are left to policy managers.

## Permissions

`ManageServiceLevelPolicies` (**Manage service level policies**) lets a user list, create, edit and delete policies. Administrators have it by default.

## Work Orders

Work Orders registers the `WorkOrders` scope (metrics **Response** and **Resolution**, priorities **Urgent**, **High**, **Normal** and **Low**), a **Work orders** card with the statuses that pause the timers, an observer that raises its warning and breach events, and a usage provider for the work order types that name a policy. `WorkOrderService` starts, pauses, resumes, completes and retargets the timers, and copies their due times to `WorkOrder.Sla`, which the work order list, index and reports read.

The Work Orders data migration (version 3) copies each policy Work Orders kept to a shared policy with the same id. Weekly hours become a Business Hours schedule named after the policy; a business hours calendar stays the policy's schedule. Each open work order gets timers that start at its creation, moved on by the time it already stood still; a response already given completes its timer, a work order on hold is paused, and a warning or breach already raised is not raised again. The `ServiceLevels` section of older `WorkOrders` recipe steps is still read and copied the same way.
