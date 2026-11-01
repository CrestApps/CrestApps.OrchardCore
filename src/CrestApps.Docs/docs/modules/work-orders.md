---
sidebar_label: Work Orders
title: Work Orders
description: "Work orders for field service: types with their defaults, checklists, service levels whose clock runs in opening hours and stops while waiting, parts taken off a van's stock, invoices through Transactions, follow-ups and workflow events."
user_manual:
  - user-manual/field-service/work-orders
---

The **Work Orders** module (`CrestApps.OrchardCore.WorkOrders`, with `WorkOrders.Abstractions` and `WorkOrders.Core`) keeps the jobs done for customers. A work order is made from a type, which gives it its duration, priority, checklists, lines, service level and billing; it then moves through its statuses until it is completed, invoiced and closed.

The admin screens are described in the User Manual: [Work Orders](../user-manual/field-service/work-orders.md).

## Features

| Feature | Id | Depends on |
| --- | --- | --- |
| Work Orders | `CrestApps.OrchardCore.WorkOrders` | — |

The module works with other features when they are on:

| Feature on | What it adds |
| --- | --- |
| Inventory | Lines can name a stock item; parts used come off a stock location, such as a van's, with `IInventoryService.ConsumeAsync` and the work order as the reference. Counted parts are used in whole units. |
| Transactions | Completing a billed work order makes an outstanding invoice (`ReferenceType` `WorkOrder`, source `WorkOrders`, due in 30 days) with an invoice number. |
| Geo | A new or changed service address gets the territory it is in, and is queued for geocoding (`WorkOrderServiceAddress`). |
| Assets | A work order can be for assets; the asset page links to the asset's work orders. |
| Workflows | The five work order events. |

## Records

All live in the `WorkOrders` YesSql collection.

- **`WorkOrderType`**: `Name`, `Color`, `DefaultDurationMinutes`, `RequiredSkills`, `RequiredCertificationIds`, `CrewSize`, `RequiredEquipmentTypeIds`, `ChecklistTemplateIds`, `DefaultLines`, `ServiceLevelPolicyId`, `BillingMode` (`NotBillable`, `FixedPrice`, `TimeAndMaterials`), `Currency`, `FixedPrice`, `DefaultPriority`, `RequiresSignature` and `RequiredPhotos`.
- **`WorkOrder`**: `Number` (`WO-000001` and on), `TypeId`, `Priority` (`Low`, `Normal`, `High`, `Urgent`), `Status` (`New`, `Ready`, `Scheduled`, `InProgress`, `OnHold`, `Completed`, `Closed`, `Canceled`), `SubStatusId`, `Summary`, `Description`, the customer (`CustomerId`, `CompanyId`, `ContactId`) and `Contact` (name, phone, email), `ServiceAddress` (an `Address`), `AccessNotes`, `TerritoryId`, `AssetIds`, the window (`EarliestUtc`, `LatestUtc`), `Sla`, `RequiredSkills`, `EstimatedMinutes`, preferred and excluded workers, `Lines`, `Checklists` (each a copy of its template's questions with the answers), `Attachments`, `Signatures`, `Notes` (the history), `Source` with `SourceId`, `ParentWorkOrderId` (follow-ups), `InvoiceTransactionId` and when and by whom it was completed and closed.
- **`WorkOrderLine`**: `Kind` (`Product`, `Service`, `Labor`, `Expense`), `ProductId`, `StockItemId`, `Description`, `QuantityEstimated`, `QuantityUsed`, `UnitPrice`, `LocationId` (where the parts came from) and `Billable`.
- **`ChecklistTemplate`**: `Name` and `Questions`: `Section`, `Text`, `Kind` (`YesNo`, `PassFailNA`, `Text`, `Number`, `Choice`, `MultiChoice`, `Photo`, `Signature`, `MeterReading`, `Date`), `Required`, `Help`, `Min` and `Max`, `Unit`, `Choices`, `MinPhotos`, `Meter` and `ShowWhen` (shown only when an earlier answer is a value).
- **`ServiceLevelPolicy`**: `Targets` (per priority, minutes to respond and to resolve), `WeeklyHours` (the days and hours the clock runs in) or `HoursCalendarId` (hours from an `IServiceHoursProvider`), `TimeZoneId`, `PauseStatuses` (on hold for a new policy) and `WarningPercent` (80).
- **`WorkOrderSubStatus`**: `Name`, `ParentStatus`, `Color` and `PausesSla`.

Each settings record has its own index table (`WorkOrderTypeIndex`, `ChecklistTemplateIndex`, `ServiceLevelPolicyIndex`, `WorkOrderSubStatusIndex`). `WorkOrderIndex` keeps the number, type, status, whether it is open, priority, summary, customer, territory, due times, creation time and source; `WorkOrderAssetIndex` has a row per asset.

## Behavior

`WorkOrderService` does everything to a work order, and the pages, API and tools go through it.

- **New work orders.** `NewAsync(typeId)` makes a work order with the type's defaults and copies of its checklists. `CreateAsync` gives it its number and due times, runs the `IWorkOrderHandler`s and starts the **Created** event. `UpdateAsync` runs them again, so a changed address is placed again.
- **Statuses.** `ChangeStatusAsync` allows only the usual moves (`WorkOrderStatusRules`): a closed or canceled work order doesn't move, a completed one can be closed or reopened, and canceling needs a reason. A sub-status must belong to the status.
- **Service levels.** `SlaClock` counts a target in the policy's weekly hours in its time zone, in the hours an `IServiceHoursProvider` gives for its calendar, or around the clock. The clock stops in a pause status or a sub-status that pauses it, and the due times move out by the time stopped. Moving it to Ready, Scheduled or In progress is the first response. `SlaMonitor`, run every minute by a background task, starts the **Service Level Warning** event at the warning share of a target and the **Service Level Breached** event when it is missed, once each.
- **Parts.** `UsePartAsync` records what was used on a line; for a counted part it takes the units off the chosen location through every `IWorkOrderPartsService`, refusing more than the location has.
- **Checklists.** `AnswerAsync` checks an answer fits its question: yes or no, pass, fail or N/A, a number in range, one of the choices, or a date.
- **Completing.** `CompleteAsync` refuses a work order with a required question unanswered (one shown by its condition), without a signature its type requires, or with fewer photos than it needs. It then invoices the billable amount through `IWorkOrderBilling`: the fixed price, or the billable lines at their used quantities (or planned ones for services not yet recorded), and starts the **Completed** event.
- **Follow-ups.** `FollowUpAsync` makes a new work order of the same type for the same customer, contact, place and assets.

## Workflow events

| Event | Starts when | Input |
| --- | --- | --- |
| Work Order Created | A work order is created. | |
| Work Order Status Changed | Its status or sub-status changes. | `PreviousStatus` |
| Work Order Service Level Warning | It is close to missing a target. | `Target` |
| Work Order Service Level Breached | It misses a target. | `Target` |
| Work Order Completed | It is completed. | |

Each can be limited to some work order types. Every event's input has `WorkOrderId`, `Number`, `TypeId`, `Status`, `Priority`, `Summary`, `CustomerId` and `TerritoryId`.

## Extending

- `IWorkOrderHandler`: change a work order before it is created or saved.
- `IWorkOrderObserver`: hear about the events after they happen.
- `IWorkOrderPartsService` and `IWorkOrderBilling`: take parts off stock and invoice, in place of Inventory and Transactions.
- `IServiceHoursProvider`: give the open hours of a policy's calendar.

## Permissions

| Permission | Allows |
| --- | --- |
| `ManageWorkOrderSettings` (Manage work order settings) | Types, checklists, service levels and sub-statuses. |
| `ManageWorkOrders` (Manage work orders) | Creating and working work orders. |
| `ViewWorkOrders` (View work orders) | The work order list and pages. Implied by Manage work orders. |
