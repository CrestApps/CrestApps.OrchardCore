---
sidebar_label: Service Levels
title: Service Levels
description: "The shared Service Levels building block: policies with a target per metric and priority counted on business hours, a stored timer per record and metric that pauses and resumes exactly, the tracker, the monitor that raises warnings and breaches, scopes, observers, policy usage and the recipe and deployment step."
user_manual: user-manual/administration/service-levels
---

**Service Levels** measures how long work takes against targets: how soon a record must first be answered, answered again, or resolved, at its priority, counted on a business hours schedule or around the clock. Any module can use it. Work Orders keeps its response and resolution timers in it, and Tickets will too.

| Feature | Id | Depends on |
| --- | --- | --- |
| Service Levels | `CrestApps.OrchardCore.ServiceLevels` | Business Hours |

| Project | Holds |
| --- | --- |
| `CrestApps.OrchardCore.ServiceLevels.Abstractions` | `ServiceLevelPolicy`, `ServiceLevelTarget`, `ServiceLevelTimer`, `ServiceLevelTimerState`, `ServiceLevelMetrics`, `ServiceLevelStart`, `IServiceLevelTracker`, `IServiceLevelObserver`, `IServiceLevelScopeProvider`, `IServiceLevelPolicyManager`, `IServiceLevelPolicyUsageProvider`, the registration helpers and `ServiceLevelsPermissions` |
| `CrestApps.OrchardCore.ServiceLevels.Core` | The YesSql stores and indexes (collection `ServiceLevels`), `DefaultServiceLevelTracker`, `ServiceLevelMonitor`, `ServiceLevelTargetGrid` and `AddServiceLevelsCore()` |
| `CrestApps.OrchardCore.ServiceLevels` (module) | **Configuration > Service Levels**, the policy editor, the monitor background task, the migration and the recipe and deployment step |

## Policies

A `ServiceLevelPolicy` belongs to one **scope**, the kind of work it is for. The scope names the metrics and the priorities, so the policy editor shows a grid of minutes per metric and priority, plus an **Any other priority** row whose targets have an empty `Priority`. The scope is fixed once the policy exists.

| Member | Meaning |
| --- | --- |
| `Scope` | The name of a registered `IServiceLevelScopeProvider`, such as `WorkOrders` |
| `Targets` | `{ Metric, Priority, Minutes }`; `FindTarget(metric, priority)` returns the priority's target, else the metric's target for every priority |
| `HoursScheduleId` | A schedule from any Business Hours source; empty counts around the clock |
| `WarningPercent` | 1 to 99; 80 by default |
| `Properties` | Settings other modules keep on the policy (Work Orders keeps `WorkOrderServiceLevelSettings` with its pause statuses) |

The built-in metrics are `FirstResponse`, `NextResponse` and `Resolution` (`ServiceLevelMetrics`); a scope may add its own.

The editor is built through `IDisplayManager<ServiceLevelPolicy>`. A module adds a card for its scope with its own `DisplayDriver<ServiceLevelPolicy>` that returns `null` for other scopes and stores its values in `Properties`. A policy that a record still uses can't be deleted: each module says what uses a policy through `IServiceLevelPolicyUsageProvider`.

### Registering a scope

```csharp
public sealed class TicketServiceLevelScope : IServiceLevelScopeProvider
{
    public string Name => "Tickets";

    public LocalizationSource DisplayName => _displayName;

    public Task<IReadOnlyList<ServiceLevelOption>> GetMetricsAsync(CancellationToken cancellationToken = default) => ...;

    public Task<IReadOnlyList<ServiceLevelOption>> GetPrioritiesAsync(CancellationToken cancellationToken = default) => ...;
}

services.AddServiceLevelScope<TicketServiceLevelScope>();
services.AddServiceLevelObserver<TicketServiceLevelObserver>();
services.AddServiceLevelPolicyUsage<TicketDeskPolicyUsage>();
```

With one scope, **Add Policy** opens the editor for it; with several, it lists them.

## Timers

The module that owns a record tells `IServiceLevelTracker` what happens to it, while holding the record's own lock:

| Method | When |
| --- | --- |
| `StartAsync(ServiceLevelStart)` | The record is created, or a repeating metric starts a new cycle (a customer replies). An active timer of the same metric is cancelled first. `Paused` starts it paused; `AlreadyWarned` and `AlreadyBreached` mark a timer copied from another system so the monitor doesn't raise those again. |
| `PauseAsync` / `ResumeAsync` | The record waits (on hold, waiting for the customer) and stops waiting |
| `CompleteAsync` | The work was done; the business time used is kept in `ElapsedSeconds` |
| `CancelAsync` | The record is no longer measured (closed as spam, deleted) |
| `RetargetAsync` | The priority or the policy changed; the time already used counts against the new target |
| `GetTimersAsync` | The record's timers, newest first |

A timer keeps the business seconds it has used (`UsedSeconds`), so a pause, a resume or a new target never adds wall-clock time to a deadline, and the warning point moves with the paused time.

## The monitor

A background task runs `ServiceLevelMonitor` every minute. It reads the running timers that are due in pages ordered by due time (never every record), marks each breach and warning with a concurrency-checked save, and only after that save calls the `IServiceLevelObserver`s (`WarningAsync`, `BreachedAsync`). Completing a timer calls `CompletedAsync`. A timer that an owner changed while the monitor was marking it is read again on the next pass, and nobody is told twice.

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

## Permissions

`ManageServiceLevelPolicies` (**Manage service level policies**) lets a user list, create, edit and delete policies. Administrators have it by default.

## Work Orders

Work Orders registers the `WorkOrders` scope (metrics **Response** and **Resolution**, priorities **Urgent**, **High**, **Normal** and **Low**), a **Work orders** card with the statuses that pause the timers, an observer that raises its warning and breach events, and a usage provider for the work order types that name a policy. `WorkOrderService` starts, pauses, resumes, completes and retargets the timers, and copies their due times to `WorkOrder.Sla`, which the work order list, index and reports read.

The Work Orders data migration (version 3) copies each policy Work Orders kept to a shared policy with the same id. Weekly hours become a Business Hours schedule named after the policy; a business hours calendar stays the policy's schedule. Each open work order gets timers that start at its creation, moved on by the time it already stood still; a response already given completes its timer, a work order on hold is paused, and a warning or breach already raised is not raised again. The `ServiceLevels` section of older `WorkOrders` recipe steps is still read and copied the same way.
