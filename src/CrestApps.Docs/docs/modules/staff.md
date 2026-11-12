---
sidebar_label: Staff
title: Staff
description: "One record for the people who work for the business: the staff member with or without a user account, employment details, the reporting line, dated team memberships, capabilities other modules register, the staff directory and picker, manager scoping, time zones, employment history, lifecycle observers, badges and PINs, pay rates, the Staff role, reports, workflow events, AI tools, recipes, and how Workforce, Contact Center, field service, Scheduling, Point of Sale and Accounts build on it."
---

The **Staff** module keeps one record for every person who works for the business: an employee, a contractor or a crew member, with or without a user account. Other modules build on it instead of keeping their own people records. Workforce schedules staff members, Contact Center agents and Point of Sale cashiers are staff members, Dispatch sends staff members to jobs, Scheduling books them, and Accounts names the staff who look after each account. Each of those modules adds its own card to the staff editor and its own capability to the record.

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Staff` | Staff | CrestApps Users, Teams, Roles |
| `CrestApps.OrchardCore.Staff.Compensation` | Staff Compensation | Staff |
| `CrestApps.OrchardCore.Staff.Badges` | Staff Badges | Staff |
| `CrestApps.OrchardCore.Staff.OrgChart` | Staff Organization Chart | Staff |

Projects: `CrestApps.OrchardCore.Staff.Abstractions` (the `StaffMember` model and the contracts other modules use), `CrestApps.OrchardCore.Staff.Core` (the store, indexes and services), `CrestApps.OrchardCore.Staff` (the module).

## The staff member

| Field | Notes |
| --- | --- |
| `UserId` | The linked Orchard user, optional and unique. Kiosk-only and seasonal people have none. |
| `DisplayName`, `Email` | For a linked person, taken from the user (its display name, else its user name) every time the record is saved, and when the user changes. Other modules read them here and never copy them. |
| `Phone`, `EmployeeNumber`, `ExternalId` | The employee number is unique and doubles as the badge number. `ExternalId` is the id in an HR or payroll system. |
| `JobTitle`, `EmploymentType`, `Status` | `EmploymentType` is `FullTime`, `PartTime`, `Seasonal`, `Temporary`, `Contractor` or `Subcontractor`; `Status` is `Active`, `OnLeave`, `Suspended` or `Terminated`. |
| `HireDate`, `SeniorityDate`, `TerminationDate`, `DateOfBirth` | Pure dates. The date of birth is sensitive: only people with **View sensitive staff data** see it. A person whose last working day has passed is terminated by the hourly upkeep task. |
| `ManagerId` | The staff member the person reports to. A manager that doesn't exist, the person themselves, or a manager who reports to the person is refused. |
| `PrimaryTeamId`, `TeamMemberships` | Dated memberships (`TeamId`, `Role` of `Member`/`Lead`/`Scheduler`/`Manager`, `EffectiveFrom`, `EffectiveTo`) in the shared [Teams](./teams.md). The first membership is the main team. |
| `TimeZoneId`, `HomeBase`, `WorkLocation` | The home base is geocoded through [Geo](./geo.md) when it is on. |
| `ContractedHoursPerWeek` | The hours of the person's contract. |
| `Capabilities` | The kinds of work the person does, as registered by other modules (see [Capabilities](#capabilities)). |
| `Properties` | Other modules' data on the person, such as Workforce's scheduling hours (`WorkforceStaffSettings`), pay rates (`StaffPayRates`) and the badge PIN (`StaffBadge`). |

Each staff member is a YesSql document in the `Staff` collection, with `StaffMemberIndex`, `StaffTeamMembershipIndex` (one row per membership, dates as `yyyyMMdd` numbers) and `StaffCapabilityIndex`. Every change to the job title, status, employment type, manager, teams, work location or last day adds a `StaffHistoryEntry` (who, when, from, to) to an append-only log, shown on the staff editor and in the Staff reports.

## Admin screens

**Staff > All Staff** lists staff members on Orchard Core's admin list, with a search and filters for team, status and capability. The editor is built by display drivers: Staff's own cards (person, employment, teams, place, capabilities), then the cards other modules add, then the reporting line and the employment history. **Staff > My Profile** shows a person their own record and the pages other modules add for them. **Staff > Organization Chart** (Staff Organization Chart feature) draws the reporting line. **Configuration > Settings > Staff** turns on **Offer only staff where people are assigned work**.

The user editor gets a **Staff** card that makes the person a staff member, or edits their record in place, for anyone who may manage them.

## Services

| Contract | What it does |
| --- | --- |
| `IStaffMemberManager` | `ICatalogManager<StaffMember>` plus `FindByUserIdAsync`, `FindByEmployeeNumberAsync`, `GetByUserIdsAsync`, `SearchAsync(StaffQuery, skip, take)` (text, team with its sub-teams, teams, status, has a user, capability, manager, ids), `ListByTeamAsync`, `ListByTeamInRangeAsync` and `ListDirectReportsAsync`. |
| `IStaffDirectory` | The read side other modules use: `IsStaffAsync(userId)`, `FindByUserIdAsync`, `GetAsync(ids)`, `GetNamesAsync(ids)`, `GetNamesByUserIdAsync(userIds)`, `SearchAsync(text, capability, requireUser, take)` for pickers (working staff only), and `GetCapabilities()`. Answers are cached for the request. |
| `IStaffHierarchy` | `GetManagerChainAsync`, `GetReportsAsync(includeIndirect)` and `ManagesAsync`, from one index read per request. |
| `IStaffScopeService` | Which staff a signed-in person may manage: everyone with **Manage staff** (or a module permission registered with `AddStaffScopePermissions`, such as **Manage the whole workforce**); with **Manage the staff you manage** (or **Manage workers**), their direct and indirect reports and the members of the teams where they are a `Manager` or `Scheduler`, with sub-teams. |
| `IStaffTimeZoneResolver` | A person's zone: their own, else their main team's or its parents', else the site's (`GetTimeZoneIdAsync`); `FindTimeZoneIdAsync` stops before the site's, for modules with a fallback of their own, as Dispatch has. |
| `IStaffAccountService` | `EnsureAsync(userId, name, capabilities, defaults)`: makes sure the person doing a module's work is a staff member, as Contact Center does for agents, Point of Sale for cashiers and Scheduling for bookable people. |
| `IStaffBadgeService` | Sets, clears and checks the PIN used with the employee number at kiosks and registers (PBKDF2, a badge locks for 15 minutes after five wrong PINs). The Workforce time clock signs people in through it. |
| `IStaffHistoryService` | Lists a person's employment history, newest first. |

Register the services with `services.AddStaffCore()`; it is safe to repeat. `AddStaffSkillLinks()` makes a staff member and their user one skill holder when Skills is on.

## Extension points

| Contract or helper | Use it for |
| --- | --- |
| `DisplayDriver<StaffMember>` | Adding a card to the staff editor (Workforce scheduling, Contact Center agent, Telephony phone, Skills, Pay, Badge). |
| `AddStaffCapability(name, displayName, description, position)` | Registering a kind of work, shown as a checkbox in the editor and usable as a picker and list filter. Built-in registrations: `Agent` (Contact Center), `Technician` (Dispatch), `Bookable` (Scheduling), `Cashier` (Point of Sale). |
| `IStaffLifecycleObserver` (`AddStaffLifecycleObserver`) | Reacting after a person is saved: `StaffChangedContext` tells whether they were just created, `Activated`, `Terminated`, whether the manager, user or teams changed. On termination, Workforce removes their future shifts, Dispatch unassigns their future visits, Contact Center signs them out and Telephony frees their extension. |
| `IStaffHomeLinkProvider` (`AddStaffHomeLinkProvider`) | Adding links to **My Profile**: Workforce adds My Work and the schedule, Time Tracker adds My time. |
| `AddStaffScopePermissions(manageEveryone, manageOwn)` | Letting holders of another module's permissions manage staff. |
| `ICatalogEntryHandler<StaffMember>` | The manager's lifecycle, such as Workforce writing its settings back before a save. |
| Index providers on `StaffMember` | Querying a module's own data on staff, in the `Staff` collection. |

## People pickers

The `StaffPicker` view component picks working staff (`capability` narrows to a kind of work, `valueType = "userId"` stores user ids, `multiple` picks several). Work orders pick their preferred and excluded technicians with it, fleet vehicles their default driver, Scheduling the person behind a staff resource, and accounts their account team.

Pickers that store users ask Users for the `Staff` audience (`UserSearchQuery.Audience`, the `audience` argument of the `UserPicker` view component and the user search endpoint): activity assignees, lead and opportunity owners, bulk assignment, phone extensions and lines, time entries and certificates. Until **Offer only staff where people are assigned work** is on, the audience isn't limited, so a site keeps offering every user while its staff are set up.

## The Staff role

Working staff with a user account are put in a role named **Staff** (created with no permissions) through `IRoleGrantService`, and taken out when they stop working or the account is unlinked. The role gets **View one's own staff profile**; give it only what every member of staff needs. The hourly upkeep task puts in the role anyone who was saved before the role existed.

## Other modules on Staff

- **Workforce** schedules staff members. Its hours and availability live in `WorkforceStaffSettings` on the staff member (`staff.MaxHoursPerWeek` and the like are C# extension properties), the Workers list is a scheduling view of staff, and payroll export reads `StaffPayRates`.
- **Contact Center** makes every new agent a staff member with the `Agent` capability (after the request, off the sign-in path), shows an agent card on the staff editor and signs out people who leave. Workforce for Contact Center gives agents their staff record through `IStaffAccountService`.
- **Dispatch** reads technicians, home bases and time zones from Staff, adds a **Technician** name and **Labor cost** (hours at each person's cost of an hour) to the Visits report data set, and unassigns the future visits of people who leave.
- **Scheduling** picks the person behind a staff resource among staff and fills an empty name or time zone from their record.
- **Point of Sale** makes the person who opens a register a staff member with the `Cashier` capability.
- **Accounts** adds the **Account Team** feature: an owner and an account team picked from staff, shown on the account page.
- **Skills** reads the old `Worker` subject kind as `Staff`; a staff member and their user hold skills together.
- **Teams** gets its members from Staff, people without a user account included (`TeamMember.StaffMemberId`).

## Reports, workflows and AI

With Data Pipelines on, the **Staff** report data source offers **Staff members** (one row per person: job, main team, manager, employment, status, capabilities, dates) and **Staff history** (one row per change), for headcount, hires, departures and turnover. With Workflows on, **Staff Member Hired**, **Staff Member Left** and **Staff Manager Changed** start workflows with the person's id, user, name, job, manager and team. With AI on, the **Find staff** (`staffFind`) and **Reporting line** (`staffReportingLine`) tools answer people who may view staff.

## Recipes and deployment

The `Staff` recipe step creates or updates staff members, matched by id, then employee number, then user (by `userName`); teams are given by name and the manager by id or `managerEmployeeNumber`, in any order. The **Staff** deployment step exports every staff member with their teams by name and their user by user name, and leaves out the date of birth unless **Include sensitive data** is ticked. The `StaffSettings` site settings travel in Orchard Core's `Settings` step.

## Upgrading from Workforce workers

Workforce used to keep its own `WorkerProfile` records. The Workforce migration moves each worker into the `Staff` collection as a staff member with the **same id**, so every stored worker id (punches, timesheets, shifts, time off, visits, territories, skills) still points at the same person. Workforce's hours and availability move to `WorkforceStaffSettings`, pay rates to `StaffPayRates`, the kiosk PIN to `StaffBadge`, and the manager worker id becomes the manager. A site that ran Workforce before Staff existed gets the Staff feature turned on first; the move runs at the next start. The old worker index tables are left in place, empty.
