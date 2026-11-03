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
| Service Agreements | `CrestApps.OrchardCore.WorkOrders.Agreements` | Work Orders |

The module works with other features when they are on:

| Feature on | What it adds |
| --- | --- |
| Inventory | Lines can name a stock item; parts used come off a stock location, such as a van's, with `IInventoryService.ConsumeAsync` and the work order as the reference. Counted parts are used in whole units. |
| Transactions | Completing a billed work order makes an outstanding invoice (`ReferenceType` `WorkOrder`, source `WorkOrders`, due in 30 days) with an invoice number. |
| Geo | A new or changed service address gets the territory it is in, and is queued for geocoding (`WorkOrderServiceAddress`). |
| Assets | A work order can be for assets; the asset page links to the asset's work orders. |
| Workflows | The five work order events. |
| Skills | A type's required skills (with the lowest level) and certifications are edited on its page, for Dispatch. |
| Dispatch | Visits for each work order, shown in a panel on its page. |

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

- **Places.** With Geo on, the editor places the job with a pin on the map; a pinned place is kept (`GeocodeSource` `Pin`) and gives the territory, and an address without one is geocoded in the background.
- **New work orders.** `NewAsync(typeId)` makes a work order with the type's defaults and copies of its checklists. `CreateAsync` gives it its number and due times, runs the `IWorkOrderHandler`s and starts the **Created** event. `UpdateAsync` runs them again, so a changed address is placed again.
- **Statuses.** `ChangeStatusAsync` allows only the usual moves (`WorkOrderStatusRules`): a closed or canceled work order doesn't move, a completed one can be closed or reopened, and canceling needs a reason. A sub-status must belong to the status.
- **Service levels.** `SlaClock` counts a target in the policy's weekly hours in its time zone, in the hours an `IServiceHoursProvider` gives for its calendar, or around the clock. The clock stops in a pause status or a sub-status that pauses it, and the due times move out by the time stopped. Moving it to Ready, Scheduled or In progress is the first response. `SlaMonitor`, run every minute by a background task, starts the **Service Level Warning** event at the warning share of a target and the **Service Level Breached** event when it is missed, once each.
- **Parts.** `UsePartAsync` records what was used on a line; for a counted part it takes the units off the chosen location through every `IWorkOrderPartsService`, refusing more than the location has.
- **Checklists.** `AnswerAsync` checks an answer fits its question: yes or no, pass, fail or N/A, a number in range, one of the choices, or a date.
- **Completing.** `CompleteAsync` refuses a work order with a required question unanswered (one shown by its condition), without a signature its type requires, or with fewer photos than it needs. It then invoices the billable amount through `IWorkOrderBilling`: the fixed price, or the billable lines at their used quantities (or planned ones for services not yet recorded), and starts the **Completed** event.
- **Follow-ups.** `FollowUpAsync` makes a new work order of the same type for the same customer, contact, place and assets.

## Service agreements

The **Service Agreements** feature keeps recurring visits, such as preventive maintenance, for a customer over a period.

- **`ServiceAgreement`** (in the `WorkOrders` collection, indexed by `ServiceAgreementIndex`): `Number` (`SA-00001` and on), `Name`, `Status` (`Draft`, `Active`, `Expired`, `Canceled`), the customer and `Contact`, `ServiceAddress`, `AccessNotes`, `AssetIds`, `StartDate`, `EndDate`, `TimeZoneId`, `Schedules`, `Pricing` (`Included` or `PerVisit`) with `PricePerVisit`, `ExpiringNoticeDays` (30) and `ExpiringNotifiedUtc`.
- **`AgreementSchedule`**: `Name`, `TypeId`, `Rule` (an RFC 5545 rule, expanded from the start date with the Calendars recurrence engine), `LeadDays` (14) and `AssetIds`.

`ServiceAgreementScheduler` runs every active agreement each hour (and from the agreement's page). For each schedule, it makes the work order of every visit day within the lead time that has none yet: the source is `Agreement` and the source id is the agreement, schedule and day, so each visit gets one work order however often it runs. The work order's window is the visit day in the agreement's time zone, across daylight saving changes; it copies the customer, place and assets, and its `AgreedPrice` (zero when included, the price per visit otherwise) replaces its type's billing. An agreement past its end date expires, and an active one within its notice of the end starts the **Service Agreement Expiring** event once (`IServiceAgreementObserver`). The `ManageServiceAgreements` permission covers the agreement pages.

## Workflow events

| Event | Starts when | Input |
| --- | --- | --- |
| Work Order Created | A work order is created. | |
| Work Order Status Changed | Its status or sub-status changes. | `PreviousStatus` |
| Work Order Service Level Warning | It is close to missing a target. | `Target` |
| Work Order Service Level Breached | It misses a target. | `Target` |
| Work Order Completed | It is completed. | |
| Service Agreement Expiring | An agreement comes within its notice of its end date. | `AgreementId`, `Number`, `Name`, `EndDate` |

Each can be limited to some work order types. Every event's input has `WorkOrderId`, `Number`, `TypeId`, `Status`, `Priority`, `Summary`, `CustomerId` and `TerritoryId`.

## Bookings, dispositions and contacts

Work Orders links up with three other features when they are on.

- **Scheduling** (`CrestApps.OrchardCore.Scheduling`). A work order type lists, under **Bookings**, the appointment types whose bookings make work orders of it (`WorkOrderType.AppointmentTypeIds`). An `IAppointmentObserver` (`AppointmentWorkOrderObserver`) acts on each confirmed booking of one of those types, after it is saved:
  - it makes a work order with the appointment type's name as its summary, the customer's name, phone and email as the contact, the booking's notes as the description, and the booked time as its earliest and latest time (source `Appointment`, the booking's id), once per booking;
  - rescheduling moves the work order's window and adds a note;
  - canceling cancels the work order, with the booking's reason.

  The type editor lists the appointment types through `IWorkOrderLookup.AppointmentTypesAsync`.
- **Omnichannel Management** (`CrestApps.OrchardCore.Omnichannel.Managements`). The **Create Work Order** subject action (`CreateWorkOrder`) makes a work order of the chosen type, and optionally priority, when its disposition is chosen. The work order has the activity's contact, the notes' first line as its summary, the whole notes as the description, and the number the activity reached as the contact phone. It is made once per activity (source `Activity`, the activity's id); Dispatch then gives it a visit.
- **Contacts.** A CRM contact's page (any content item with `OmnichannelContactPart`) shows a **Work orders** card with the contact's latest ten work orders and a **New work order** menu of types. A work order started there keeps the contact (`ContactId`, a hidden field of the editor, and the contact's name).

## Extending

- `IWorkOrderHandler`: change a work order before it is created or saved.
- `IWorkOrderObserver`: hear about the events after they happen.
- `IWorkOrderPartsService` and `IWorkOrderBilling`: take parts off stock and invoice, in place of Inventory and Transactions.
- `IServiceHoursProvider`: give the open hours of a policy's calendar.
- `WorkOrderPanels`: a display driver for it adds a panel, placed in the Content zone, to the work order's page.

## Recipes and deployment

The `WorkOrders` recipe step creates or updates, by `ItemId`, checklists (`Checklists`), service levels (`ServiceLevels`), sub-statuses (`SubStatuses`), types (`Types`, which name their checklists and service level by id) and, with Service Agreements on, agreements (`Agreements`). The **Work order settings** deployment step exports them, the agreements only when asked, since their customers, places and assets must be on the other site too. Work orders themselves aren't exported: they carry numbers and service level clocks of the site they belong to.

## Permissions

| Permission | Allows |
| --- | --- |
| `ManageWorkOrderSettings` (Manage work order settings) | Types, checklists, service levels and sub-statuses. |
| `ManageWorkOrders` (Manage work orders) | Creating and working work orders. |
| `ViewWorkOrders` (View work orders) | The work order list and pages. Implied by Manage work orders. |
