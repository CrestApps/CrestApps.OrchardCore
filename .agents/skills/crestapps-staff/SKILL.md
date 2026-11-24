---
name: crestapps-staff
description: How to build on the CrestApps Staff building block (CrestApps.OrchardCore.Staff) instead of keeping another people record. Use when a module needs employees, workers, agents, technicians, cashiers, account managers or any person who works for the business; a staff picker; a manager or reporting line; a person's time zone; reacting when someone is hired or leaves; a card on the staff editor; or a capability such as Technician. Covers StaffMember, IStaffMemberManager, IStaffDirectory, IStaffHierarchy, IStaffScopeService, IStaffTimeZoneResolver, IStaffAccountService, IStaffBadgeService, AddStaffCapability, IStaffLifecycleObserver, IStaffHomeLinkProvider, AddStaffScopePermissions, the StaffPicker view component and the Staff user audience.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# Building on Staff

Staff owns the person who works for the business. Never add another worker, agent, technician or cashier record, never copy a staff member's name, and never write another manager walk or time-zone chain. Reference `CrestApps.OrchardCore.Staff.Abstractions` only (never the Staff module or `Staff.Core`), and put the integration in a `[RequireFeatures(StaffConstants.Features.Staff)]` startup unless your feature depends on Staff.

## Read people

- `IStaffDirectory`: `IsStaffAsync(userId)`, `FindByUserIdAsync`, `GetAsync(ids)`, `GetNamesAsync(ids)`, `GetNamesByUserIdAsync(userIds)`, `SearchAsync(text, capability, requireUser, take)` (working staff only). Cached per request. Use it to show names; store staff member ids (or user ids), never names.
- `IStaffMemberManager.SearchAsync(new StaffQuery { TeamId, Capability, ManagerId, Status, HasUser, Ids, Search }, skip, take)` for lists and reports.
- `IStaffHierarchy`: manager chain, direct or all reports, `ManagesAsync`.
- `IStaffTimeZoneResolver.GetTimeZoneIdAsync(staff)` (person, main team and parents, site) or `FindTimeZoneIdAsync` when you have your own last fallback.

## Make the person doing your work staff

Call `IStaffAccountService.EnsureAsync(userId, name, [YourCapability], staff => staff.JobTitle ??= "...")`. On a hot path (sign-in, a sale), do it in `ShellScope.AddDeferredTask` and catch failures, as `AgentProfileStaffHandler` (Contact Center) and `CashierStaffHandler` (Point of Sale) do.

## Register a capability

`services.AddStaffCapability("Technician", LocalizationSource.Create("Technician", typeof(Startup)), LocalizationSource.Create("...", typeof(Startup)), position)`. It shows as a checkbox on the staff editor and filters pickers (`StaffPicker` `capability`) and lists. Don't make a capability required by existing behavior unless a migration sets it: records moved from Workforce have no capabilities.

## Add a card to the staff editor

Write a `DisplayDriver<StaffMember>` with `Edit`/`UpdateAsync` and a `Location("Content:N")`. Keep your values in `staff.Put(new YourSettings())` / `staff.TryGet<YourSettings>(out var settings)`; add an `IndexProvider<StaffMember>` with `CollectionName = StaffConstants.CollectionName` when you must query them. Workforce's `WorkforceStaffDisplayDriver`, Contact Center's `AgentStaffDisplayDriver` and Telephony's `PhoneStaffDisplayDriver` are examples.

## React to hires and departures

Implement `IStaffLifecycleObserver.ChangedAsync(StaffChangedContext)` and register it with `AddStaffLifecycleObserver<T>()`. Use `context.Terminated`, `context.Activated`, `context.ManagerChanged`, `context.TeamsChanged`, `context.UserChanged`. Observers run after the save in the same scope; don't call `SaveChangesAsync`, don't save the same staff member again, and expect failures to be logged, not to undo the save. Examples: `WorkforceStaffLifecycleObserver` (removes future shifts), `DispatchStaffLifecycleObserver` (unassigns future visits), `ContactCenterStaffLifecycleObserver` (signs out), `TelephonyStaffLifecycleObserver` (frees the extension).

## Pick people

- Staff ids: `@await Component.InvokeAsync("StaffPicker", new { name, selectedValues, capability = "Technician", multiple = true })`.
- User ids among staff: the same with `valueType = "userId"`, or the `UserPicker` view component with `audience = "Staff"` (honors the **Offer only staff** setting; lists everyone while it is off).

## Other extension points

- `AddStaffScopePermissions(manageEveryone, manageOwn)`: your module's permissions that let people manage staff.
- `AddStaffHomeLinkProvider<T>()`: links on **My Profile**.
- `IStaffBadgeService`: badge (employee number) and PIN sign-in.
- `StaffPayRates` (Compensation feature): `RateOn(date, activityCodeId)` and `CostOn(date)` for payroll and labor costing.

## Tests

Use `WorkforceTests.CreateAsync()` (it registers `AddStaffDoubles()`, the Staff tables and collection) and `WorkforceTests.AddWorkerAsync(harness, name, configure)` to create staff. `InMemoryUserDirectory` sets the user a linked staff member takes their name from; `RecordingRoleGrantService` tells whether the Staff role was granted.

See `src/CrestApps.Docs/docs/modules/staff.md` and `docs/engineering/building-blocks.md`.
