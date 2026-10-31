# Workforce Management plan (issue #1225)

Status: design finalized 2026-10-30, nothing built yet. Progress is tracked in the **Progress** section at the end.

Issue [#1225](https://github.com/CrestApps/CrestApps.OrchardCore/issues/1225) asks for a Workforce Management module on top of the Calendars foundation from [#1197](https://github.com/CrestApps/CrestApps.OrchardCore/issues/1197) (PR [#1222](https://github.com/CrestApps/CrestApps.OrchardCore/pull/1222)). This plan widens it to the three things a workforce suite does, built as small reusable modules that share one core:

1. **Plan and track who works when** for any kind of worker: shifts, time off, rules, self-service, time and attendance.
2. **Contact center workforce management**: interval statistics, forecasting, staffing, intraday management and real-time adherence for agents.
3. **Field service management**: work orders, dispatching technicians, crews, trucks and equipment, routes, van stock and a technician mobile app.

Everything reuses what the repository already has (Calendars, Scheduling, Reports, Report Builder, Workflows, AI tools, Messaging, Transactions, Inventory, Addresses, Contact Center agent states and the payroll timecard) and extends it with additive changes, rather than rebuilding any of it.

## Contents

1. Industry baseline
2. Decisions
3. What exists and how it is reused
4. Module map
5. Shared core: workers, teams, skills, the Workforce calendar
6. Shift planning
7. Time off
8. Labor rules
9. Time clock (time and attendance)
10. Self-service portal (PWA)
11. Contact center workforce management
12. Geo, maps and territories
13. Assets, fleet and equipment
14. Work orders
15. Dispatch
16. Technician mobile (offline)
17. Optimization engines
18. Cross-cutting: workflows, APIs, AI tools, reports, permissions, recipes, notifications
19. Changes to existing modules
20. Non-functional requirements
21. Phases and delivery
22. Testing strategy
23. Out of scope
24. Risks
25. Glossary
26. Progress

---

## 1. Industry baseline

This is the capability map of current enterprise workforce suites (contact center WFM, field service management, hourly workforce scheduling, time and attendance). It is the checklist the design was measured against. The plan uses generic industry terms only (AGENTS.md forbids product names in code and public docs).

| Area | Industry-standard capability | Where this plan covers it |
|---|---|---|
| People and organization | Worker profile with employment data, org hierarchy, manager scoping, skills with proficiency, certifications with expiry, seniority | §5 |
| Scheduling | Shift templates, rotations, coverage requirements by interval, auto-scheduling against demand, draft and what-if scenarios, publishing with notice, open shifts, copy a week | §6 |
| Self-service | Mobile app, schedule view, availability preferences, shift swap, give away and pick up, open shift claim, voluntary overtime and time off (VOT/VTO) offers, schedule bidding, iCal subscription, push notifications | §10 |
| Time off | Types, policies, accruals, carryover and caps, balances, request and approval, allotments with auto-approval, waitlist, blackout dates | §7 |
| Compliance | Max hours, rest between shifts, breaks, minors, overtime, consecutive days, predictive-scheduling (Fair Workweek) notice and predictability pay, an audit of schedule changes | §8 |
| Time and attendance | Punch clock (web, kiosk, mobile, geofence), rounding, timesheets, overtime and pay codes, approvals, attendance exceptions and points, payroll export | §9 |
| Contact center forecasting | Interval history (15/30 minutes), volume and handle-time forecasts, seasonality and special days, backtest accuracy, Erlang C and Erlang A staffing, channel concurrency, shrinkage, long-term capacity plans | §11 |
| Intraday | Forecast compared with actuals, reforecast, staffing gaps, moving breaks, scheduling meetings and training at the best time, VOT/VTO offers | §11 |
| Adherence | Mapping of scheduled activities to agent states, real-time adherence board, adherence and conformance percentages, exceptions | §11 |
| Field service | Work orders with types, tasks and checklists, SLA, parts, customer assets (installed base), preventive-maintenance agreements, billing | §13, §14 |
| Dispatch | Schedule board (resources over time plus a map), unscheduled queue, matching on skills, territory, availability and travel, crews, tools and vehicles as resources, optimization, ETA and "on the way" messages, live map | §15 |
| Mobile workforce | Offline job details, checklists, photos, signatures, parts used, status updates, navigation, GPS while on shift | §16 |
| Optimization | Solver for shift scheduling (set covering) and for routing (vehicle routing with time windows), run on demand, nightly and intraday | §17 |
| AI (current state of the art) | Assistants that answer "who's working", draft schedules, propose and explain dispatch optimizations that a dispatcher approves, suggest forecast adjustments, and serve self-service through chat and voice agents | §18 |
| Analytics | Service level, ASA, abandonment, occupancy, shrinkage, adherence, forecast accuracy, overtime, first-time fix rate, SLA compliance, utilization, travel compared with work time | §18 |

## 2. Decisions

Decisions WD1 to WD12 were made with the product owner on 2026-10-30. WD13 onward follow from them.

| # | Decision | Why |
|---|---|---|
| WD1 | **Build order: shared core, then contact center WFM, then field service.** | Both verticals use the core. Contact center WFM can use the agent-state log and timecard that already exist. |
| WD2 | **Work orders are their own module** (`CrestApps.OrchardCore.WorkOrders`), not an Omnichannel activity. | A work order needs a type, tasks, parts, SLA, a site and an asset. That is too heavy for `OmnichannelActivity`, and it would tie dispatch to the CRM. Work orders link to CRM contacts, activities, appointments and transactions by id. |
| WD3 | **`WorkerProfile` with an optional user, plus a `WorkforceTeam` hierarchy.** | Subcontractors and crew members can be scheduled without a login. The hierarchy (organization, region, site, department, team, crew) gives manager scoping. A team can link a role for permissions. Each team gets one Workforce calendar that code manages. |
| WD4 | **Time and attendance is its own feature** (Time Clock). | Punches, timesheets, overtime and payroll export. Contact center agents' time comes from the existing reconciled timecard. |
| WD5 | **Optimizers sit behind seams.** A built-in solver with no native dependencies ships with the module, and Google OR-Tools (Apache-2.0) is an add-on module. | OR-Tools ships about 100 MB of native binaries for each platform, so only sites that need it pay that cost. |
| WD6 | **Maps use Leaflet (BSD) vendored, with a configurable tile URL.** Geocoding, travel-time matrix and routing sit behind provider seams. Straight-line distance (haversine) is the default, and Azure Maps, Google Maps and OSRM are separate provider features. | No vendor lock-in and no required key to try it out. |
| WD7 | **An installable PWA "My Work" portal with push notifications.** It works offline for field technicians and online for everyone else. The public API makes a native app possible later. | Technicians lose signal in basements and rural areas. One web code base serves agents, staff and technicians. |
| WD8 | **One shared skill catalog** in a new `CrestApps.OrchardCore.Skills` module, which also adds certifications with expiry. Contact Center routing reads the same skills, so an agent's skills are set once. | Skills belong to the person, not to the channel. |
| WD9 | **Time off includes policies, accruals, a balance ledger, allotments with auto-approval, a waitlist and blackout dates.** | This is the contact center standard. Allotments are what keep a contact center staffed. |
| WD10 | **Labor rules are a code-registered rule catalog** that admins combine into rule sets per team, location or jurisdiction, each set to Warn, Block or Require an override reason. No legal presets ship, only documented examples. | Laws change. Shipping presets would make us responsible for keeping them legally correct. |
| WD11 | **Forecasting uses built-in statistical models plus an `IForecastModel` seam.** The built-ins are weighted history, Holt-Winters and interval curves, with Erlang C and Erlang A staffing and channel concurrency. Backtest MAPE/WAPE is shown so models can be compared. | It works with no AI connection, and an AI or ML model can plug in and be measured against the same backtest. |
| WD12 | **GPS is opt-in and collected only while on shift.** Position is reported only while a technician is on shift with a job (or traveling), at a set interval, with consent and a retention period. | Privacy by default, and enough for ETAs and the live map. |
| WD13 | **Only published shifts are calendar events.** Draft schedules and what-if scenarios are Workforce documents until they are published. | Workers, Scheduling and free/busy only ever see the real plan. Drafts can be thrown away without touching calendar history. |
| WD14 | **Shift patterns (rotations) are expanded into individual shifts** when a schedule is generated. They are not stored as RRULE series. | Each shift is edited, swapped and paid on its own, so a recurring series would be split on almost every occurrence. |
| WD15 | **Breaks, meetings and other segments are stored inside the shift event** (`ShiftSegments` in the event's `Properties`), not as separate events. A Workforce `ICalendarBusyTimeSource` reports the non-work segments as busy. | This keeps the event count low (one event per worker per day), and Scheduling still treats breaks as unavailable. |
| WD16 | **Approved time off is a confirmed `TimeOff` calendar event with Source = Workforce.** Pending requests appear on the calendar through an `ICalendarEventSource` (read-only), not as stored tentative events. | Approval happens in the approval queue, not by editing an event. Pending requests never block availability. |
| WD17 | **Every event Workforce or Dispatch writes is source-owned** (`Source` = `Workforce` or `Dispatch`), so it is read-only in the generic calendar UI and edited only in the planner or board. | The calendar module's existing source-ownership rule. |
| WD18 | **Contact Center owns interval statistics.** The 15-minute queue and agent aggregates are computed in Contact Center Analytics, and WFM reads them. | Interval reports are useful without WFM, and Contact Center owns the event-log semantics. It also keeps the dependency direction right: Workforce.ContactCenter depends on Contact Center, never the reverse. |
| WD19 | **The timeline UI (shift planner, intraday agent view and dispatch board) uses vis-timeline** (dual Apache-2.0/MIT, vendored). FullCalendar 6 stays for the calendar views. | FullCalendar's resource-timeline views are paid-only and not MIT. |
| WD20 | **Visits, not bookings or appointments.** A scheduled piece of a work order is a `ServiceVisit`. | `Booking` (Commerce Bookings) and `Appointment` (Scheduling) are taken, and visit is the common field-service term. |
| WD21 | **Delivery is three pull requests, one per vertical, each with a commit per slice.** The PRs are (A) core, (B) contact center WFM, (C) field service. | Following "one PR per plan" to the letter would put about 15 phases in one PR. Three plan-sized PRs keep reviews workable. |
| WD22 | **Manager scoping is resource-based.** A manager sees and edits only the workers in the teams they manage (and those teams' sub-teams), through `IWorkforceScopeService`. Global permissions are combined with scope. | This is the industry norm, and it is required when a site has many locations. |

## 3. What exists and how it is reused

| Existing piece | Path or contract | Reused for |
|---|---|---|
| Calendars (PR #1222) | `AddCalendarType`, `ICalendarEventManager`, `ICalendarQueryService`, `ICalendarAvailabilityService`, `ICalendarBusyTimeSource`, `ICalendarEventSource`, reminders ledger, feeds, `ManagedByCode()`, `RequiresAttendee()`, `NeedsApproval()` | The Workforce calendar type, published shifts, time off, free/busy, the worker's iCal feed, reminders ("shift starts in 30 minutes"), Equipment calendars for vehicles, and visits on each resource's calendar |
| Scheduling (PR #1222) | `SchedulingResource.AvailabilityMode = Shifts` reads `Shift` events on `ShiftCalendarId`, `IAppointmentObserver`, Self-Service pages | Customer self-booking works from shifts with no change, because Workforce registers the `Shift` event type that Scheduling expects. Appointments of a "creates work order" type become work orders |
| Calendars RRULE engine | `RecurrenceExpander` (Core) | Service agreements (preventive maintenance), recurring meetings in the intraday activity scheduler, accrual schedules |
| Contact Center agent states | `AgentPresenceStatus`, `AgentStateReasonCode`, `IAgentPresenceManager`, `InteractionEvent` log (`IInteractionEventStore`), `AgentStateTimeline`, `AgentStateEventReader` | Adherence (actual state compared with the scheduled activity), conformance, and interval statistics |
| Contact Center reports | `ReconciledPayrollTimecardReportProvider`, `AgentWorkforceReportProvider` (Occupancy, Utilization, BreakAnalysis …), `QueueServiceLevelMetrics`, `InteractionMetrics` | Actual hours in timesheets, historical AHT and service level, shrinkage inputs |
| Contact Center queues and skills | `ActivityQueue` (service-level settings, `BusinessHoursCalendarId`, `SkillRequirements`), `ContactCenterSkill`, `AgentSkill`, `AgentProfile.SkillProficiencies`, `IAgentEntitlementPolicy` | Planning groups (queues plus channel), the shared skill catalog migration (WD8) |
| Business hours | `IBusinessHoursGate.IsOpenAsync(calendarId, utc, tz)` | Forecasting only in open hours, and SLA clocks in business hours for work orders |
| Reports and Report Builder | `AddReport<T>()`, `AddDataSource<T>()` (`RecordDataSource`/`RecordDataSet<T>`), report subscriptions, dashboard widgets, PDF and Excel exports, `ReportsHub` | Every WFM and FSM report, dashboards and scheduled reports. The skill to follow is `crestapps-report-data-source` |
| Messaging | `IMessagingConversationService.SendAsync`/`SendDirectAsync`, `MessageTemplate`, `MessagingQuietHoursGuard` | Customer notices ("technician on the way"), worker SMS notices |
| Transactions | `Transaction` (`ReferenceType`/`ReferenceId`), `ITransactionInvoiceService`, `ITransactionPayLinkService` | Work-order invoices and pay links (ReferenceType `WorkOrder`) |
| Inventory | `IInventoryService.AdjustAsync/TransferAsync/ReserveAsync`, `StockMovement` ledger | Van stock (a vehicle is a stock location), parts used on a work order, replenishment by transfer |
| Products | Products catalog and pricing | Parts and billable services on work orders |
| Customers and Companies | `Customer.Addresses`, `Company.Locations` (`CompanyLocation`) | Work-order customers and service sites |
| Addresses | `Address`, `IAddressFormatter`, `IAddressValidator`, `IAddressAutocompleteProvider` | Service addresses, worker home bases, team locations (gains an optional latitude and longitude, §19) |
| Time Zones | `ITimeZoneSelectListProvider`, NodaTime | Team and worker zones |
| Subscriptions | Recurring billing | Optional billing for service agreements |
| Wizard | `IWizardDefinitionProvider` | "Set up workforce" and "Set up field service" guided setup |
| Media | Orchard Media | Work-order photos, signatures, certification documents |
| Notifications | Orchard `INotificationService` | In-app notifications |
| SignalR | `SignalRHubRoutes.GetHubPath<T>()`, `ContactCenterHub` | Real-time adherence board, live dispatch board |
| AI | `AddCoreAITool<T>(name)`, AI agents and profiles, realtime voice | Workforce, dispatch and work-order AI tools, and self-service through chat and voice agents |
| Orchard Workflows, Recipes, Deployment, OpenApi, Audit Trail, Spatial (`GeoPointField`) | Orchard Core | Workflow events and tasks, recipe and deployment steps, API docs, audit, geo points on content |

**Gaps this plan fills:** forecasting, interval statistics, adherence, shifts and schedules, worker records, an org hierarchy, certifications, a punch clock, any geo, map or route support, serial-tracked assets, work orders, dispatch, and a mobile/PWA portal.

## 4. Module map

Every module follows the Abstractions/Core/Module split. Optional integrations are startups with `[RequireFeatures(...)]` (the decision recorded in the Calendars plan), not extra features, unless a site would realistically want to turn the piece off.

| Project(s) | Feature id(s) | Depends on | Holds |
|---|---|---|---|
| `CrestApps.OrchardCore.Skills` (+ `.Abstractions`, `.Core`) | `CrestApps.OrchardCore.Skills` | Users | Skill catalog, proficiency scale, certifications, skill assignments, certification records with expiry |
| `CrestApps.OrchardCore.Workforce` (+ `.Abstractions`, `.Core`) | `CrestApps.OrchardCore.Workforce` (base) | Calendars, Skills, Roles | Workers, teams, activity codes, availability preferences, the Workforce calendar type, scoping |
| | `CrestApps.OrchardCore.Workforce.ShiftPlanning` | Workforce | Shift templates, patterns, coverage requirements, draft schedules and scenarios, publishing, open shifts, schedule bidding, the auto-scheduler |
| | `CrestApps.OrchardCore.Workforce.TimeOff` | Workforce | Time-off types, policies, accruals, balance ledger, requests, approvals, allotments, waitlist, blackout dates |
| | `CrestApps.OrchardCore.Workforce.LaborRules` | Workforce | Rule catalog, rule sets, enforcement, the schedule change log, predictability pay |
| | `CrestApps.OrchardCore.Workforce.TimeClock` | Workforce | Punches, kiosks, geofence, time clock policies, pay periods, timesheets, attendance exceptions and points, payroll export |
| | `CrestApps.OrchardCore.Workforce.Portal` | Workforce | The "My Work" PWA, worker and manager self-service, swaps, VOT/VTO, web push |
| | `CrestApps.OrchardCore.Workforce.Api` | Workforce | `/api/workforce/v1` |
| `CrestApps.OrchardCore.Workforce.ContactCenter` | `CrestApps.OrchardCore.Workforce.ContactCenter.Adherence` | Workforce.ShiftPlanning, ContactCenter | Adherence rules, real-time adherence engine and board, adherence and conformance history, exceptions |
| | `CrestApps.OrchardCore.Workforce.ContactCenter.Forecasting` | Workforce.ShiftPlanning, ContactCenter | Planning groups, forecasts, Erlang staffing, shrinkage, forecast-driven coverage, intraday, capacity plans |
| `CrestApps.OrchardCore.Geo` (+ `.Abstractions`, `.Core`) | `CrestApps.OrchardCore.Geo` | — | Geo points, geocoding, travel-time and route seams, the haversine provider, territories, the map component, location pings |
| `CrestApps.OrchardCore.Geo.AzureMaps`, `.Geo.GoogleMaps`, `.Geo.Osrm` | one each | Geo | Provider implementations |
| `CrestApps.OrchardCore.Assets` (+ `.Abstractions`, `.Core`) | `CrestApps.OrchardCore.Assets` | — | Assets (customer installed base and internal), hierarchy, meters, warranty, documents |
| | `CrestApps.OrchardCore.Assets.Fleet` | Assets, Calendars.Resources | Vehicles and equipment as schedulable resources, van stock location, default driver |
| `CrestApps.OrchardCore.WorkOrders` (+ `.Abstractions`, `.Core`) | `CrestApps.OrchardCore.WorkOrders` | — (Assets, Inventory, Transactions, Geo through startups) | Work orders, types, checklists, SLA policies, parts and labor lines, billing |
| | `CrestApps.OrchardCore.WorkOrders.Agreements` | WorkOrders | Service agreements and preventive maintenance generation |
| `CrestApps.OrchardCore.Dispatch` (+ `.Abstractions`, `.Core`) | `CrestApps.OrchardCore.Dispatch` | WorkOrders, Workforce, Geo | Visits, candidate matching, the dispatch board, the built-in optimizer, customer notices, live tracking page |
| | `CrestApps.OrchardCore.Dispatch.Mobile` | Dispatch, Workforce.Portal | Field mode of the PWA: offline visits, checklists, photos, signatures, parts, GPS |
| `CrestApps.OrchardCore.Workforce.OrTools` | `CrestApps.OrchardCore.Workforce.OrTools` | Workforce.ShiftPlanning (Dispatch through a startup) | OR-Tools solvers (CP-SAT for shifts, routing for visits) |

Dependency rules (enforced by `ModuleDependencyGraph` and the project reference guards):

- Contact Center never references Workforce. `Workforce.ContactCenter` references both.
- WorkOrders does not reference Dispatch. Dispatch schedules work orders through `IWorkOrderService`.
- Geo, Assets and Skills reference no vertical, so other modules (CRM, Classifieds, Commerce) can reuse them.

## 5. Shared core: workers, teams, skills, the Workforce calendar

### 5.1 Skills module (WD8)

| Record | Fields |
|---|---|
| `Skill` (CatalogItem) | Name, DisplayName, Category, Description, `ProficiencyScaleId` (default 1–5), `RequiresCertificationId` (optional), Enabled |
| `ProficiencyScale` | Levels with value and label (1 Trainee … 5 Expert) |
| `Certification` (CatalogItem) | Name, Issuer, `ValidityMonths` (null = no expiry), `RequiresDocument`, `RenewalReminderDays` |
| `SkillAssignment` | `SubjectKind` (User or Worker), SubjectId, SkillId, Proficiency, EffectiveFrom, EffectiveTo, AssignedBy, AssignedUtc |
| `CertificationRecord` | WorkerId (or UserId), CertificationId, Number, IssuedOn, ExpiresOn, DocumentPath (Media), VerifiedBy, VerifiedUtc, Status (Pending, Valid, Expired, Revoked) |

Services:

- `ISkillCatalog` (CRUD plus lookup).
- `ISkillAssignmentService` handles assign, remove, `GetSkillsAsync(subject, onDate)` and `FindSubjectsAsync(skill, minProficiency, onDate)`.
- `ICertificationService` handles records, `IsValidOnAsync(worker, cert, date)` and expiring-soon queries.
- `ISkillAssignmentHandler` provides change hooks.

A background task runs daily and raises **Certification Expiring** (at the `RenewalReminderDays` offsets, using a ledger so each offset fires once) and **Certification Expired** (which sets Status = Expired).

**Contact Center migration (production data, so additive only):**

1. A Skills migration copies every `ContactCenterSkill` into `Skill` with the **same id**, and every `AgentProfile.SkillProficiencies` entry into `SkillAssignment` (subject = user).
2. Contact Center keeps `AgentProfile.SkillProficiencies` as a **read model** for the routing hot path. A Contact Center `ISkillAssignmentHandler` (in a startup with `RequireFeatures("CrestApps.OrchardCore.Skills")`) keeps it in sync. Routing code does not change.
3. The Contact Center skill admin pages redirect to the Skills pages. `ContactCenterSkillManager` becomes a thin adapter over `ISkillCatalog`. The `ContactCenterSkill` recipe and deployment steps stay as aliases that write through the adapter.
4. Contact Center gains a feature dependency on Skills (turned on by its own migration for existing tenants, so no site loses skills when upgrading).

### 5.2 Workers

`WorkerProfile` (CatalogItem, `Workforce` collection):

| Group | Fields |
|---|---|
| Identity | WorkerId, `UserId` (optional, WD3), DisplayName, Email, Phone (E.164, PhoneNumbers formatting), PhotoPath, EmployeeNumber (unique when set), ExternalIds (HR or payroll id map) |
| Employment | EmploymentType (FullTime, PartTime, Seasonal, Temporary, Contractor, Subcontractor), Status (Active, OnLeave, Suspended, Terminated), HireDate, SeniorityDate, TerminationDate, ManagerWorkerId, JobTitle, DateOfBirth (optional, permission-gated, used only for minor rules) |
| Placement | PrimaryTeamId, `TeamMemberships` (TeamId, Role: Member, Lead, Manager, Scheduler; EffectiveFrom, EffectiveTo), TimeZoneId (defaults to the team's), HomeBase (Address with geo), TerritoryIds |
| Capacity | ContractedHoursPerWeek, MinHoursPerWeek, MaxHoursPerWeek, MaxHoursPerDay, `WorkPreferences` (preferred shift lengths, preferred days, desires extra hours) |
| Extensions | `Properties` bag edited by `DisplayDriver<WorkerProfile>`. Examples: Contact Center adds the agent link, Fleet adds the default vehicle, Time Clock adds pay rates and badge or PIN hash, Labor Rules adds the rule set override |

Indexes:

- `WorkerProfileIndex`: WorkerId, UserId, EmployeeNumber, Status, EmploymentType, PrimaryTeamId, ManagerWorkerId.
- `WorkerTeamMembershipIndex`: one row per membership with effective dates.

Services:

- `IWorkerService` handles CRUD, terminate (end memberships, cancel future shifts with a reason, keep history), `GetByUserIdAsync` and `FindAsync(query)`.
- `IWorkerScopeService` (WD22) returns the teams and workers a user may see or manage.
- `IWorkerHandler` provides hooks.

Auto-creation: when Contact Center is on, a setting "Create a worker profile for every agent" adds a profile on agent profile creation, and a **Sync agents** action backfills existing agents.

### 5.3 Teams and organization

`WorkforceTeam` (CatalogItem):

| Field | Meaning |
|---|---|
| Name, Kind | `Organization`, `Region`, `Site`, `Department`, `Team` or `Crew` (crew = a team scheduled as one unit in dispatch, §15) |
| ParentId | Hierarchy, with the depth capped (`WorkforceOptions.MaxTeamDepth`, default 8) |
| Managers | Worker ids with the Manager or Scheduler role (also stored on memberships) |
| LinkedRoleName | An optional role whose members are synced as Members, and the role used for calendar sharing |
| TimeZoneId, Location | The team's zone (wall-clock rules and day boundaries) and address with geo (Site kind) |
| CalendarId | The team's Workforce calendar, created on team creation (managed by code) |
| Defaults | RuleSetId, TimeOffPolicyIds, TimeClockPolicyId, PayPeriodCalendarId, AdherenceRuleSetId: inherited down the hierarchy unless overridden |
| Crew only | LeadWorkerId, VehicleAssetId, Schedulable (bool) |

`ITeamService` covers the hierarchy queries (ancestors, descendants, effective defaults with inheritance) and membership changes with effective dating.

### 5.4 Workforce calendar type

The Workforce feature registers it:

```csharp
services.AddCalendarType(WorkforceConstants.CalendarType, type => type
    .WithDisplayName(S["Workforce"])
    .WithDescription(S["Published shifts, time off and training for a team."])
    .WithIcon("fa-solid fa-user-clock")
    .WithOwner(CalendarOwnerKind.None)
    .ManagedByCode()
    .WithCapabilities(CalendarCapabilities.Attendees | CalendarCapabilities.AllDay | CalendarCapabilities.FreeBusy)
    .WithConflictPolicy(CalendarConflictPolicy.AllowOverlaps)
    .AddEventType("Shift", S["Shift"], e => e.Color("#2b8a3e").Busy().RequiresAttendee())
    .AddEventType("TimeOff", S["Time off"], e => e.Color("#e8590c").Busy().AllDayByDefault().RequiresAttendee())
    .AddEventType("Training", S["Training"], e => e.Color("#1971c2").Busy().RequiresAttendee())
    .AddEventType("Meeting", S["Meeting"], e => e.Color("#6741d9").Busy())
    .AddEventType("OnCall", S["On call"], e => e.Color("#f08c00").Free().RequiresAttendee()));
```

- The event type is named exactly `Shift`, so Scheduling's "hours from shifts" works unchanged.
- Shift events carry `ShiftData` in `Properties`: ScheduleId, ShiftTemplateId, `Segments` (ActivityCodeId, start, end), PublishedVersion, LaborCost (optional), OpenShift flag, and the WorkOrder/visit ids when Dispatch adds travel or job segments later.
- `WorkforceSegmentBusyTimeSource : ICalendarBusyTimeSource` reports Break, Lunch, Meeting and Training segments as busy for the attendee (WD15).
- `PendingTimeOffEventSource : ICalendarEventSource` shows pending requests as read-only tentative blocks to managers (WD16).
- Access: team members get Read, managers and schedulers get Manage, and others get FreeBusy when the site setting allows it. This needs the new Calendars seam `ICalendarAccessContributor` (§19).
- A worker's personal view ("My schedule") queries occurrences where they are an attendee, across every Workforce calendar, through `CalendarEventAttendeeIndex`.

### 5.5 Activity codes

`ActivityCode` (CatalogItem, site-defined, seeded): Name, Kind (Work, Break, Meal, Meeting, Training, Coaching, OfflineWork, Travel, OnCall, TimeOff, Other), Color, `IsPaid`, `CountsAsWork` (toward hours rules), `CountsTowardCoverage` (staffing), `IsSchedulable`. Contact Center adherence mappings hang off activity codes (§11.4). Seeded codes: Work, Break (paid, 15 minutes), Meal (unpaid, 30 minutes), Meeting, Training, Coaching, Offline work, Travel, On call.

### 5.6 Availability preferences

`WorkerAvailability`: a weekly pattern (per weekday: Unavailable, Available or Preferred windows), dated exceptions, an effective date range, and a status (Pending, Approved) when the site requires a manager to approve availability changes (a setting). The auto-scheduler treats Unavailable as a hard constraint and Preferred as a soft one.

## 6. Shift planning

### 6.1 Records

| Record | Fields |
|---|---|
| `ShiftTemplate` | Name, Color, Start (wall-clock), Length, `StartFlexMinutes` (the scheduler may move the start within ±N), segment layout (activity code + offset + length, or a placement window for breaks and meals such as "meal between hours 3 and 5"), eligible team ids, required skill ids, Enabled |
| `ShiftPattern` (rotation) | Name, length in weeks (1–12), day cells (template id, or Off), assigned workers with start date and offset week |
| `CoverageRequirement` | Scope (team, or planning group for Contact Center, or territory for field), Skill (optional), Source (`Manual`, `Forecast`, `Workload`), per-date or per-weekday interval rows (interval start, Min, Target, Max headcount), effective range |
| `SchedulePeriod` | TeamId, start and end dates (one or more weeks, aligned to the team's week start), Status (`Draft`, `Generating`, `Ready`, `Published`, `Locked`), PublishedVersion, PublishedUtc, PublishedBy, NoticeDeadline (from rule sets) |
| `ScheduleScenario` | PeriodId, Name, IsPrimary, `DraftShifts` (worker id or null for open shift, date, template, start, end, segments, notes), generator run stats (coverage score, cost, rule violations, solver used, runtime) |
| `OpenShift` | PeriodId or ad hoc, team, date, template, start, end, segments, required skills, ClaimPolicy (`FirstCome`, `ManagerApproves`, `BySeniority` with a deadline), Status (Open, Claimed, Filled, Canceled), Claims |
| `ScheduleChange` (append-only) | Period, worker, kind (Added, Removed, Moved, Swapped, Segment changed, Open shift filled), before and after, changed by, reason, `InitiatedBy` (Employer or Worker), `WithinNoticeWindow`, predictability-pay entry id |
| `ScheduleBid` (later slice) | Bid window, line set (pre-built schedule lines), worker preferences ranked, award by seniority |

All are documents in the `Workforce` collection with indexes on period, team, worker and date. Draft shifts live inside the scenario document. A period of 500 workers by 4 weeks is about 10,000 rows, so the scenario stores shifts in **chunks per week** (`ScheduleScenarioWeek` documents) to stay well under document-size limits.

### 6.2 Flow

1. **Create a period** for a team or a set of teams.
2. **Generate.** The options are: apply patterns, copy from a previous period, or **auto-schedule** against coverage requirements with `IShiftScheduleSolver` (§17).
   - Inputs: workers (memberships, skills on each date, availability, contracted, min and max hours, approved time off, existing published shifts), templates, coverage, rule sets and the objective weights (coverage vs cost vs preferences vs fairness).
   - The run is a background job with progress (`SchedulePeriod.Status = Generating`) that the planner page polls.
3. **Edit.** This happens on the planner board (vis-timeline: workers as rows, days or hours as columns, with drag, resize, copy and paste, a template palette, and a segment editor). A coverage strip shows required, scheduled and gap per interval, and a rule panel shows violations live. Edits re-run labor rules through `ILaborRuleEvaluator` (debounced, server-side).
4. **Compare scenarios** side by side with their metrics (coverage %, hours, cost, violations, preference score). Mark one primary.
5. **Publish.** This validates (Block rules stop it, Warn rules are listed and acknowledged, override-required rules need a reason) and writes the primary scenario into the team calendar through `ICalendarEventManager`. It creates, updates or cancels events by stable shift key, in **chunks committed in separate scopes** (the YesSql "commit detaches a batch" trap). It then bumps `PublishedVersion` and notifies workers (only the ones whose shifts changed after a republish). It also records `ScheduleChange` entries when the change is a republish inside the notice window.
6. **After publish.** Edits go straight to the calendar through `IShiftService` (single shift add, move, remove, swap) with rules, change log, notification and predictability pay applied.

### 6.3 Services

| Interface | Does |
|---|---|
| `IShiftTemplateService`, `IShiftPatternService`, `ICoverageService` | CRUD, coverage per interval (required, scheduled and gap, computed from published shifts or a scenario) |
| `ISchedulePeriodService` | Create, generate (start a job), copy, compare, publish (job), lock |
| `IShiftService` | Published shift changes under a lock per worker (`WORKFORCE_WORKER_{id}`, sorted for multi-worker swaps), with rules, change log, notices |
| `IOpenShiftService` | Post, claim (validates rules and skills), approve, award by seniority at the deadline (background) |
| `IShiftTradeService` | Swap, give away, pick up. Peer accept, then manager approval if the policy requires it (team setting) |
| `IShiftScheduleSolver` | Seam (§17) |

### 6.4 Admin screens

- **Workforce > Schedules**: periods on an AdminList.
- **Planner**: the board.
- **Coverage**: requirements editor with a heatmap grid.
- **Templates**, **Patterns**, **Open shifts** (AdminList), and **Changes** (the change log, AdminList with filters).

## 7. Time off

| Record | Fields |
|---|---|
| `TimeOffType` | Name, Color, Unit (Hours, Days), IsPaid, RequiresBalance, AllowPartialDay, RequiresAttachment, RequiresReason, `CountsAgainstAllotment`, PayCode (Time Clock), visible to workers |
| `TimeOffPolicy` | Name, TypeId, accrual method (`None`, `PerPayPeriod`, `PerHourWorked`, `AnnualLumpSum`, `MonthlyFrontLoad`), rate, tenure tiers (from N months → rate), WaitingPeriodDays, MaxBalance, CarryoverMax, CarryoverExpiryMonths, ResetDate (anniversary or fixed), AllowNegativeBalance (up to N), Probation rules, eligible employment types |
| `TimeOffPolicyAssignment` | Worker or team (inherited), PolicyId, EffectiveFrom, EffectiveTo |
| `TimeOffLedgerEntry` (append-only) | WorkerId, PolicyId, Kind (`Accrual`, `Taken`, `Reversal`, `Adjustment`, `Carryover`, `Expiry`, `Payout`, `Opening`), Amount (signed), EffectiveDate, SourceRef (request id or pay period), `IdempotencyKey` (unique), Actor, Note |
| `TimeOffRequest` | WorkerId, TypeId, start and end (all-day dates, or wall-clock times plus zone for partial days), computed amount per day (from scheduled hours that day, or the default day length), Status (`Pending`, `Waitlisted`, `Approved`, `Declined`, `Canceled`, `Withdrawn`), Reason, Attachments, Approver, Decision note, CalendarEventId, the ledger entries it created |
| `TimeOffAllotment` | Scope (team, skill or planning group), TimeOffType filter, per date (or per weekday template) slots or hours available, optional interval-level slots (Contact Center), `AutoApprove` |
| `BlackoutPeriod` | Scope, date range, types blocked, message |

Behavior:

- **Balance** = the ledger sum up to a date. Projected balance = current plus scheduled accruals up to the request date, minus pending and approved requests. The request form shows both before submit.
- **Accrual runs.** A background task after each pay period closes (or nightly for daily-rate policies). It is idempotent through `IdempotencyKey = {worker}:{policy}:{period}`. Carryover and expiry run at the reset date.
- **Submit.** Validations run first: blackout, balance, overlap, rules (§8), and allotment.
  - Within allotment with `AutoApprove`: approved immediately.
  - Allotment full: waitlisted (FIFO) or pending, per the policy.
  - Otherwise: pending.
- **Approval.** The queue is an AdminList scoped to managed teams, with bulk approve. The detail shows team coverage that day (from §6.3) and who else is off.
- **Approved.** Creates a confirmed `TimeOff` calendar event (WD16) and a `Taken` ledger entry. It cancels or flags overlapping published shifts (setting: remove shifts automatically, or flag for the scheduler) with a `ScheduleChange` (InitiatedBy Worker, so no predictability pay).
- **Cancellation of approved time off** reverses the ledger entry, cancels the event and promotes the first waitlisted request (background, under the allotment lock `WORKFORCE_ALLOTMENT_{scope}_{date}`).
- **Import of opening balances and policy balances** comes from HR through a recipe step and the API (`Opening` or `Adjustment` entries).

## 8. Labor rules

### 8.1 Rule catalog (code)

`services.AddLaborRule<TRule>()`. Each rule has a key, display name, parameter model (edited by a driver) and `EvaluateAsync(LaborRuleContext)`. The context holds the worker, the candidate change, the published shifts and punches in a window, time off and a clock, and the rule returns `LaborRuleViolation` entries (rule, severity, message, affected shift ids).

Built-in rules:

| Rule | Parameters |
|---|---|
| Max hours per day, per week, per pay period | Hours; counts `CountsAsWork` activity codes; option to include on-call |
| Min hours per week | Hours (warn when the schedule is below contract) |
| Min rest between shifts | Hours (for example 8, 10 or 11); "clopening" label |
| Max consecutive work days | Days |
| Required days off per week | Count |
| Max shift length, min shift length | Hours |
| Break and meal requirements | Shift length thresholds, then required break or meal minutes and placement window |
| Overtime thresholds (forecast) | Daily or weekly thresholds and multipliers, used for cost and to warn on overtime |
| Minor work limits | Age under N (from date of birth), max hours on school days and weeks, latest end time, no-work periods |
| Skill or certification required | Shift template or work requires valid skills and certifications on the date |
| No work during approved time off | — |
| Schedule notice (predictive scheduling) | Notice days (for example 14), applies to added, removed, moved and shortened shifts |
| Predictability pay | Per change kind and notice band: premium hours or flat amount; exemptions (worker-initiated, mutual swap, voluntary VOT/VTO, emergencies) |
| Right to rest consent | When rest is below N hours, require recorded worker consent and record a premium |
| Offer hours to existing staff first | Open shifts must be offered internally for N hours before hiring or assigning temps (warning) |
| Good-faith estimate | Record and compare scheduled hours against the estimate given at hire (report) |

### 8.2 Rule sets

`LaborRuleSet` (admin): Name, description, jurisdiction label (free text), rule instances (rule key, parameters, Enforcement `Warn`, `Block` or `RequireOverride`), applies to employment types and minors. Rule sets are assigned to teams (inherited) or workers (override). The docs ship **example** rule sets in recipes, clearly labeled as examples that need legal review (WD10).

### 8.3 Enforcement points

`ILaborRuleEvaluator` is called by:

- the planner (live)
- schedule publish
- `IShiftService` changes
- open-shift claims and trades
- time-off approval (minimum hours)
- Dispatch assignment (hours and overtime)
- Time Clock punch-in (an optional "block early punch-in" rule)

It is also exposed as an `ICalendarEventHandler.ValidatingAsync` for Workforce calendar events, so the API and recipes can't bypass rules.

### 8.4 Predictability pay

Changes inside the notice window create `PredictabilityPayEntry` records (worker, change id, rule, hours or amount, status `Pending`, `Approved`, `Waived` with reason, or `Exported`). Time Clock adds them to the timesheet as a pay code, and reports list them.

## 9. Time clock (time and attendance)

| Record | Fields |
|---|---|
| `TimeClockPolicy` | Rounding (none, or nearest 1, 5, 6 or 15 minutes, with direction), early and late grace, auto-deduct meal after N hours (if not punched), allowed sources (Web, Mobile, Kiosk, Contact Center, API), geofence required (radius meters around team location or visit address), photo on punch (optional), missed-punch handling (flag, or auto-close at the scheduled end) |
| `PayPeriodCalendar` | Frequency (Weekly, Biweekly, SemiMonthly, Monthly), anchor date, week start, overtime week definition |
| `TimePunch` (append-only) | WorkerId, Kind (`In`, `Out`, `MealStart`, `MealEnd`, `BreakStart`, `BreakEnd`, `ActivityChange`, `JobStart`, `JobEnd`), Utc, Source, device id, geo (if allowed), within-geofence flag, activity code or work order or visit, `IdempotencyKey` (offline punches), edits are new punches that point to the replaced one, with reason and actor |
| `Timesheet` | WorkerId, PayPeriodId, Status (`Open`, `Submitted`, `Approved`, `Rejected`, `Locked`, `Reopened`), daily lines (pay code, hours, activity code, job or cost allocation, source punches), totals, exceptions, worker attestation (text plus UTC), approver |
| `PayCode` | Regular, Overtime, DoubleTime, Holiday, PTO (from time-off types), PredictabilityPay, CallBack, ShiftDifferential (rules: hours window, weekday, amount or multiplier), Unpaid |
| `AttendanceException` | Kind (Late, EarlyOut, MissedPunch, NoShow, UnscheduledAbsence, LongMeal, OutsideGeofence), worker, date, minutes, status (Open, Excused, Confirmed), linked shift and punch |
| `AttendancePointsPolicy` (optional slice) | Points per exception kind, rolling window, thresholds that raise a workflow event (coaching, warning) |
| Pay rates | In `WorkerProfile.Properties` (`WorkerPayRates`: base rate, effective-dated, per activity code or job overrides). Permission `ViewPayRates` |

Sources of punches:

- **Web and mobile** through the Portal, which is offline-capable for technicians through the outbox (§16).
- **Kiosk** on a shared device page (`/workforce/kiosk/{token}`). Workers identify with a badge number plus PIN (PIN stored as a salted hash, lockout after N failures) or a sign-in.
- **Contact Center.** When `TimeClockPolicy` allows the Contact Center source, agent sign-in and sign-out events from the `InteractionEvent` log become `In` and `Out` punches through a mapper (idempotency key = event id), so agents never punch twice. Lines can also be taken from `ReconciledPayrollTimecardReportProvider` for the reconcile flag.
- **Dispatch.** Visit status changes (En route, On site, Completed) become `ActivityChange`, `JobStart` and `JobEnd` punches, which give job costing.

Timesheet calculation (`ITimesheetCalculator`):

1. Pair punches into spans.
2. Apply rounding and grace.
3. Split at the day boundary in the worker's zone (DST-safe).
4. Classify through the pay-code rules and overtime thresholds (taken from rule sets).
5. Add PTO lines from approved time off and predictability pay entries.
6. Compute exceptions against the published schedule.

The calculator is pure and unit-tested.

Payroll export: the `IPayrollExportProvider` seam, plus a built-in CSV provider with a column mapping editor (worker external id, pay code mapping, hours, rate, amount, period). Locked timesheets are exported once per provider run, recorded with an export batch id, and can be re-exported on demand. The report export formats (Excel, PDF) are reused for timesheet printouts.

## 10. Self-service portal (PWA)

Front-end area at `/my-work`:

- Its own layout so it looks the same in any theme, Bootstrap 5.3 (§20 front-end note).
- `manifest.webmanifest` and a service worker, installable.
- No jQuery, following the repository convention.

**Worker pages:**

- **Home**: next shift with a countdown, today's segments, the punch button (Time Clock), and notices.
- **My schedule**: a list and calendar (FullCalendar list and week views), with an "Add to my calendar" link to a private iCal feed per worker (the Calendars Feeds token mechanism, limited to the worker's own occurrences).
- **Availability**: the weekly pattern editor, which submits for approval when that is required.
- **Time off**: balances (current and projected), request (with allotment and coverage hints, and attachments), history, and cancel.
- **Shift marketplace**: open shifts to claim, swap or give away my shift, pick up offered shifts, and VOT/VTO offers from intraday. Claims and trades show any rule violations before submitting.
- **Bids**: schedule bidding (later slice).
- **Timesheet**: the current period, attest and submit, and missed-punch corrections (which create a correction request).
- **Adherence** (Contact Center): my adherence and conformance by day.
- **Notifications** and **Settings**: channels (push, email, SMS), quiet hours, language and time zone.

**Manager pages:**

- **Approvals inbox**: time off, trades, open-shift claims, availability, timesheets, and attendance exceptions (with bulk actions).
- **Who's working now**: by team, with live status (Contact Center presence or visit status).

Notifications go through `IWorkforceNotifier`. It fans out to Orchard in-app notifications, email, SMS (Messaging, which respects `MessagingQuietHoursGuard`) and **Web Push**:

- Web Push uses VAPID keys in site settings, push subscriptions per device, and the MIT `Lib.Net.Http.WebPush` library, whose license is confirmed when it's added.
- Each notification kind has a default channel set, and workers can override it.
- The seam is `IWorkforceNotificationChannel`, so other modules can add channels.

Accessibility and localization: WCAG 2.1 AA, all strings localized, and every time shown in the worker's zone.

## 11. Contact center workforce management

### 11.1 Interval statistics (in Contact Center, WD18)

New records in Contact Center Analytics:

| Record | Fields |
|---|---|
| `QueueIntervalStatistic` | QueueId, Channel, IntervalStartUtc (15 minutes), Offered, Handled, Abandoned, ShortAbandoned (below threshold), AnsweredWithinServiceLevel, Transferred, sum and max wait seconds, handle, talk, hold and wrap-up seconds, backlog at interval end (async channels), ServiceLevelThresholdSeconds (snapshot) |
| `AgentIntervalStatistic` | UserId, IntervalStartUtc, signed-in seconds, seconds per `AgentPresenceStatus` and reason code, handled count and handle seconds per queue, concurrency-weighted seconds (chat and SMS) |

- `IntervalStatisticsAggregator` is a background task. It reads `InteractionEvent` incrementally with a durable cursor (`RecordedUtc` + id, the pattern from the activity-load resume cursors) and upserts the intervals it touched. Late events recompute only their intervals.
- A **Rebuild** command (admin and recipe) backfills a date range in batches, with the stall watchdog pattern.
- Both are indexed by (QueueId, IntervalStartUtc) and (UserId, IntervalStartUtc). There are no full scans, so a production S2 database is never scanned per render.
- Retention is set by `IntervalStatisticsOptions.RetentionDays`, default 760 (two years plus a buffer for year-over-year forecasting).
- Report data sets: Queue intervals and Agent intervals, with 15, 30 and 60-minute rollups at query time.

### 11.2 Planning groups and forecasting

| Record | Fields |
|---|---|
| `PlanningGroup` | Name, queue ids plus channel(s), required skill ids, service goal (SL % in N seconds, or ASA seconds, or response time for async), max occupancy %, concurrency (chat and SMS), patience (for Erlang A, learned from abandon data or entered), business hours calendar, teams that staff it |
| `ForecastEvent` | Name, date range, kind (Holiday, Closure, Promotion, Outage, Custom), planning groups, effect (volume multiplier or "like day" reference date), recurring yearly flag |
| `Forecast` | PlanningGroupId, horizon range, model used, created by and when, Status (Draft, Active, Superseded), interval rows (volume, AHT seconds), daily totals, manual adjustments (percent or absolute per range, with note), backtest metrics |
| `StaffingPlan` | ForecastId, per interval: required agents (raw), required scheduled (with shrinkage), occupancy, expected SL and ASA, and the shrinkage profile used |
| `ShrinkageProfile` | Planned components (breaks, meals, meetings, training, time off) derived from schedules, and unplanned components (absence, late, unplanned) from history or entered, as a percent per interval |
| `CapacityPlan` (later slice) | Weekly horizon (up to 18 months): required FTE, available FTE, attrition %, hiring classes (start date, size, training weeks, ramp curve), overtime, gap |

Models (`IForecastModel` seam, `services.AddForecastModel<T>()`):

- `WeightedHistory`: the same weekday from the last N weeks, with weights.
- `HoltWinters`: triple exponential smoothing on daily totals with weekly seasonality, plus yearly seasonality when there are two or more years of history.
- `IntervalCurve`: splits daily totals into intervals by the average interval share per weekday, from the last N weeks.
- AHT: the interval mean over N weeks, trimmed of outliers.
- Events apply after the base forecast. Closed intervals (from business hours) are zeroed.
- **Backtest**: hold out the last K weeks, forecast them, and report MAPE and WAPE per model. "Auto" picks the best.
- An AI model from another module implements the same seam and is measured the same way (WD11).

Staffing (`IStaffingCalculator`, pure and unit-tested):

- **Voice:** Erlang C solves for the minimum agents that meet SL or ASA under the max occupancy. Erlang A is used when patience is set. The calculation is done in log space to stay numerically stable for large traffic.
- **Chat and SMS:** Erlang with effective AHT = AHT / concurrency.
- **Email and tasks (deferred):** workload = volume × AHT, plus a backlog carried to meet the response-time goal across intervals.
- **Multi-skill (v1):** each planning group is staffed independently, and an agent's scheduled time counts fractionally toward each group they are skilled for, split by the share of their history. The docs state this approximation. A simulation engine is out of scope (§23).
- Output: required scheduled headcount per interval, written as `CoverageRequirement` rows with Source = Forecast for the teams that staff the group. The auto-scheduler (§6) uses it unchanged.

### 11.3 Intraday

The page shows one planning group and one day per interval:

- forecast vs actual volume and AHT
- required vs scheduled vs actually available agents (actual from `AgentIntervalStatistic` and live presence)
- SL and ASA actual vs goal
- the gap

It also offers these actions:

- **Reforecast the rest of the day**: scales the remaining intervals by the actual/forecast ratio so far (with a damping factor), or by a manual percent.
- **VOT/VTO offers**: pick intervals, count and eligible workers (skills and rules). Offers go to the Portal (first-come or by rule). Accepted offers update shifts, flagged voluntary, so no predictability pay.
- **Move breaks and meals** inside their template windows to fill gaps: a suggested list that the scheduler accepts.
- **Activity scheduler**: place a meeting, training or coaching session for N workers of length L in the lowest-impact intervals within a range, respecting rules. It can recur through the RRULE engine.
- Changes publish immediately through `IShiftService` and appear on the agents' schedules and on the adherence board.

### 11.4 Adherence

| Record | Fields |
|---|---|
| `AdherenceRuleSet` | Per activity code: allowed presence statuses and reason codes (for example Work → Available, Reserved, Busy, WrapUp; Meal → Break with reason "Meal"; Training → Training), start and end grace minutes, minimum out-of-adherence minutes to count, alert after N minutes |
| `AdherenceSegment` (history) | UserId, date, start and end UTC, scheduled activity, actual status and reason, InAdherence (bool), ExceptionId |
| `AdherenceException` | Worker, range, reason (System outage, Coaching, Approved schedule change, Other), approved by; excluded from adherence % |
| `AdherenceDaily` | UserId, date, scheduled seconds, in-adherence seconds, adherence %, scheduled work seconds, actual work seconds, conformance %, exception seconds |

- **Real-time engine.** `IRealTimeAdherenceService` keeps current adherence per signed-in agent. It listens to presence changes (the existing Contact Center after-commit observers or event log) and a timer at segment boundaries, compares them with the published schedule segment (cached per day), and pushes changes over SignalR to the board.
- Out-of-adherence beyond the alert threshold raises **Agent Out Of Adherence** once per episode (a ledger keyed by agent, segment and episode start).
- **Board.** `Workforce > Real-time adherence` shows agents by team: scheduled activity, actual state, time in state and in or out of adherence. It has filters and sorting, and its actions are change presence (existing `IAgentPresenceManager`, permission-gated) and add an exception.
- **History.** A nightly (and on-demand) job builds `AdherenceSegment` and `AdherenceDaily` from `AgentStateTimeline` and the published schedule. Report data sets are Adherence daily and Adherence segments.

## 12. Geo, maps and territories

| Contract | Does |
|---|---|
| `GeoPoint` (record struct) | Latitude, Longitude (WGS84), validated ranges |
| `IGeocodingProvider` | `GeocodeAsync(Address)` returns candidates with confidence, plus `ReverseGeocodeAsync(GeoPoint)` |
| `ITravelTimeProvider` | `GetMatrixAsync(origins, destinations, departureUtc)` returns durations and distances. The default **Haversine** provider applies a road factor (1.3) and an average speed per territory or setting (40 km/h) |
| `IRouteProvider` | Polyline and turn list for a sequence of points (map display only) |
| `IGeoService` | Picks the configured provider per capability, caches matrix results (distributed cache, TTL, keyed by rounded points and time bucket), rate-limits, falls back to Haversine when a provider fails, and logs with `SanitizeLogValue()` |
| `ServiceTerritory` | Name, ParentId, shape (GeoJSON polygon or multipolygon) or postal-code list, TimeZoneId, assigned teams and workers (Primary or Secondary), travel speed override |
| `ITerritoryResolver` | Point or address to territory (point-in-polygon, then postal-code match) |
| `WorkerLocationPing` | WorkerId, Utc, GeoPoint, accuracy, speed, heading, visit id, source. Stored only when consent is given and the worker is on shift (WD12). Deleted after `GeoOptions.LocationRetentionDays` (default 30). The latest position is cached for the board |

- **Providers.** Azure Maps (search, route matrix, route) and Google Maps (geocoding, distance matrix, directions; caching limited to what its terms allow, with the TTL capped by the provider) are separate modules, each with its own settings and keys. OSRM (a self-hosted URL) covers matrix and route.
- **Address change (§19).** Address gains an optional `Latitude`, `Longitude`, `GeocodedUtc` and `GeocodeSource`. When Geo is on, an address handler geocodes service addresses, home bases and team locations on save (in the background, with retries), and editors show a "pin on map" control to correct the result.
- **Map component.** Leaflet, Leaflet.markercluster (MIT) and Leaflet-Geoman free (MIT) are vendored in Resources (copy assets in `Assets.json`) as an `crestapps-map` resource with a small wrapper (markers, clusters, polylines, polygon draw for territories, a live marker layer). The tile URL and attribution are settings, defaulting to the OpenStreetMap tile server with a note that production sites must use their own or a commercial tile source, per the OSM tile usage policy.

## 13. Assets, fleet and equipment

| Record | Fields |
|---|---|
| `AssetType` | Name, category (Equipment, Vehicle, Tool, CustomerUnit, Component, Facility), attribute schema (simple typed fields), meter definitions, default checklist template, icon |
| `Asset` | Number, Name, AssetTypeId, ProductId (optional, Products), SerialNumber, Manufacturer, Model, OwnerKind (`Internal`, `Customer`, `Company`), OwnerId, Location (address with geo, or a team or site id, or a parent asset), ParentAssetId (hierarchy), Status (`Active`, `InService`, `OutOfService`, `Retired`), InstallDate, Warranty (start, end, provider, terms), Attributes (`Properties`), Documents (Media) |
| `AssetMeterReading` (append-only) | AssetId, meter, value, Utc, source (work order, manual, API) |

The **Fleet** feature adds these to internal Vehicle, Equipment and Tool assets:

- `Schedulable`. A schedulable asset gets an **Equipment calendar** (Calendars Resources type, `LimitConcurrent(1)`) created and linked, so dispatch visits book it and the calendar rejects double-booking.
- Vehicle data: plate, VIN, capacity, fuel type, home base, default driver worker id.
- `StockLocationId`: an Inventory location for van stock (§19 Inventory change).
- Maintenance: internal service agreements generate work orders against the asset (§14.3). Pre-trip inspection is a checklist template.

Service history is the work orders and visits for an asset, through `WorkOrderIndex.AssetIds`. A customer's installed base is the assets with OwnerKind Customer or Company. CRM contact and Company pages get an **Assets** tab through a driver in a startup.

## 14. Work orders

### 14.1 Records

| Record | Fields |
|---|---|
| `WorkOrderType` (incident type) | Name, Color, default duration, required skill ids (with min proficiency), required certification ids, required crew size, required equipment asset types, checklist template ids, default products and services lines, SLA policy id, billing mode (`NotBillable`, `FixedPrice`, `TimeAndMaterials`), priority default, "requires signature", "requires photos (N)" |
| `WorkOrder` | Number (sequence `WO-{000000}`, settings), TypeId, Priority (`Low`, `Normal`, `High`, `Urgent`), Status (`New`, `Ready`, `Scheduled`, `InProgress`, `OnHold`, `Completed`, `Closed`, `Canceled`), SubStatusId (site-defined), Summary, Description, Customer (CustomerId, or CompanyId + LocationId, or CRM contact content item id), Contact person (name, phone, email), ServiceAddress (Address with geo, access notes, site hours), TerritoryId (resolved), AssetIds, requested window (earliest, latest, preferred arrival window), SLA (policy, RespondByUtc, ResolveByUtc, first response UTC, breached flags, paused seconds), required skills (type plus overrides), estimated duration, preferred and excluded worker ids, Lines (`WorkOrderLine`: kind Product, Service, Labor or Expense; product id, description, quantity estimated and used, unit price, inventory location used, billable), Checklists (instances with answers), Attachments, Signatures (name, image path, UTC, visit id), Notes timeline, Source (`Manual`, `Appointment`, `Agreement`, `Activity`, `Api`, `AI`, `Workflow`) with source id, ParentWorkOrderId (follow-ups), InvoiceTransactionId, closed by and when |
| `ChecklistTemplate` | Name, questions (kinds: YesNo, PassFailNA, Text, Number with range and unit, Choice or MultiChoice, Photo (min count), Signature, MeterReading (asset meter), Date), required, help text, show-when (a simple condition on a previous answer), section grouping |
| `ServiceLevelPolicy` | Per priority: response target and resolution target (minutes), measured in a business hours calendar (`IBusinessHoursGate`) or 24/7, pause statuses (for example OnHold waiting for the customer), warning thresholds (percent of target) |
| `WorkOrderSubStatus` | Name, parent status, color, pauses SLA (bool) |

Indexes:

- `WorkOrderIndex`: Number, TypeId, Status, Priority, CustomerId, CompanyId, ContactId, TerritoryId, RespondByUtc, ResolveByUtc, CreatedUtc, Source, SourceId.
- `WorkOrderAssetIndex`: one row per asset.

### 14.2 Services and behavior

- `IWorkOrderService` handles create (from a type, applying defaults), update, change status (validated transitions), add and remove lines, record parts used, complete checklist answers, sign, complete, close, cancel and create follow-up. `IWorkOrderHandler` provides hooks, and `IWorkOrderObserver` runs after commit.
- **SLA clock.** `ISlaClock` computes the due dates and remaining business minutes. A background task raises **SLA Warning** and **SLA Breached** once each (through a ledger).
- **Parts.** Recording a part as used calls `IInventoryService.AdjustAsync` on the visit's vehicle stock location (or a chosen location) with a reference to the work order (§19 Inventory change).
- **Billing.** Completing a billable work order builds invoice lines:
  - labor from Time Clock job punches or line hours × rate, or the fixed price
  - products at Products pricing
  - expenses
  
  It then creates an invoice with `ITransactionInvoiceService` (ReferenceType `WorkOrder`) and optionally a pay link (`ITransactionPayLinkService`) sent to the customer through Messaging.
- **Creation sources:**
  - admin
  - API
  - workflows (task)
  - AI tool
  - Omnichannel activity action "Create work order" (startup with `RequireFeatures` Omnichannel)
  - a **Scheduling appointment type with "Creates work order"**: an `IAppointmentObserver` creates the work order with the appointment's time as the requested window and keeps both in sync
  - agreements (§14.3)

### 14.3 Service agreements (feature)

`ServiceAgreement` holds:

- Customer or company, assets covered, start and end, Status (Draft, Active, Expired, Canceled).
- `Schedules`: each has a work order type, an RRULE (reuse `RecurrenceRule` and `RecurrenceExpander` from Calendars Core), a lead time in days, and the assets it covers.
- Pricing (included, or billed per visit), with optional billing through Subscriptions (startup with `RequireFeatures`).

A daily task generates work orders `LeadDays` ahead, idempotent by agreement, schedule and occurrence date. It raises **Agreement Expiring** N days before the end date.

## 15. Dispatch

### 15.1 Records

| Record | Fields |
|---|---|
| `ServiceVisit` (WD20) | VisitNumber, WorkOrderId, Status (`Unscheduled`, `Scheduled`, `Dispatched`, `EnRoute`, `OnSite`, `Paused`, `Completed`, `Incomplete` (reason), `Canceled`), scheduled start and end (wall-clock + zone, plus UTC), promised arrival window (start and end), duration, assigned: LeadWorkerId, WorkerIds, CrewId, EquipmentAssetIds, `Pinned` (the optimizer must not move it), travel estimate (minutes, km, from the previous stop), status history (status, UTC, actor, geo), ETA (UTC, computed, last updated), CalendarEventIds per resource (Source = Dispatch, WD17), customer notifications sent |
| `DispatchObjective` | Name, weights: travel time, schedule priority and SLA risk, skill proficiency match, preferred technician, workload balance, overtime cost, same-technician-as-last-visit; hard-constraint toggles (territory strict or soft, overtime allowed) |
| `DispatchSettings` | Default arrival window length, schedule horizon, auto-dispatch (Scheduled to Dispatched at time T before start), ETA refresh interval, notification templates, tracking page on or off, optimization schedule (nightly time, intraday on events) |

Each assigned resource gets a calendar event:

- a worker: an event on their team's Workforce calendar with a `Job` segment, so their schedule shows it and Scheduling sees them as busy (through free/busy)
- equipment: an event on the asset's Equipment calendar

Travel shows as a `Travel` segment.

### 15.2 Candidate matching and assignment

`IDispatchCandidateFinder.FindAsync(visit, range, objective)` returns ranked candidates (resource set, suggested start, travel, score breakdown, warnings).

Hard constraints:

- Skills at min proficiency on the date (Skills), valid certifications on the date.
- Within the territory (or allowed secondary).
- On shift (Workforce published shifts), not on time off.
- Free in their calendars including travel buffers.
- Required equipment free.
- Not excluded.
- Labor rules (max hours, rest; overtime allowed or not).
- Crew size met (crew or multiple workers).

Soft score: the weighted `DispatchObjective`.

`IDispatchService` handles assign, reassign, unassign, move, pin, split a visit (multi-day), and status changes. It runs under locks per resource (`DISPATCH_RESOURCE_{kind}_{id}`, sorted) and re-checks availability inside the lock, exactly as Scheduling does for bookings.

### 15.3 Dispatch board

`Dispatch > Board` (vis-timeline, WD19):

- **Resource rows**: technicians, crews and equipment, grouped by territory or team. Each row shows its shift band, time off, breaks and visits colored by status, with travel bars.
- **Unscheduled panel**: an AdminList-style filtered queue of visits (priority, SLA due, type, territory, skills) to drag onto a row and time.
- **Map panel** (Leaflet): visits, technician live positions (§12, when consented), routes for the selected resource, and territory overlays. Clicking syncs with the timeline.
- **Find best slot**: candidates from §15.2 with a score explanation.
- **Optimize**: for selected resources and days (respecting pinned visits). It runs `IDispatchOptimizer` and shows a **proposal diff** (moves, travel saved, SLA risk change), which the dispatcher applies in full or in part.
- **Live updates** over SignalR (`DispatchHub`): status changes, new urgent work and ETA changes. Conflict and warning badges come from rules and SLA.
- **Utilization strip** per resource: booked, travel and idle.

### 15.4 Customer notices and tracking

Message templates (Messaging, with quiet hours respected):

- visit scheduled (window)
- reminder the day before
- technician en route, with ETA and a **tracking link**
- arrived
- completed, with a summary and an optional review request (Reviews module, startup)
- rescheduled
- canceled

The tracking page is `/track/{signed token}`. It shows the technician's first name and photo, the ETA and a map position only while the visit is En route, then a status-only view. The token expires after the visit.

ETA comes from `ITravelTimeProvider`, from the latest ping to the address. It is refreshed at the set interval and on each ping, and a change of more than N minutes sends an updated ETA message.

### 15.5 Customer self-booking with arrival windows

A Scheduling appointment type can take its slots from Dispatch capacity instead of individual working hours. It offers arrival windows per territory that have a technician with the needed skills and enough free time. This needs the Scheduling seam `ISchedulingSlotProvider` (§19). Booking creates the work order and a Scheduled visit (unassigned, or pre-assigned to the chosen candidate) through the observer from §14.2.

### 15.6 Disruption handling (intraday re-optimization)

These events trigger an "at risk" re-check:

- a visit running over its estimate
- a technician calling in sick (time off approved today)
- an urgent work order created
- a vehicle out of service

Affected visits are flagged on the board, and an optional automatic re-optimization proposal is created for the dispatcher (or auto-applied for unpinned visits when `DispatchSettings` allows). An **AI dispatch assistant** tool (§18) explains the proposal in plain language and can apply it on approval.

## 16. Technician mobile (offline)

The Portal in field mode (`Dispatch.Mobile`):

- **Today and upcoming**: route order, a map, a navigation link (opens the device's maps app with the address), and the customer contact (tap to call or text, through Telephony and Messaging when the site prefers masked calling).
- **Visit**: status buttons (En route, which sends the ETA message; Arrived, with an optional geofence check; Paused; Completed; Cannot complete, with a reason), work order details, asset history, checklists, photos (camera, compressed client-side), parts used (search van stock, then quantity), notes, customer signature (canvas), payment link to the customer, create follow-up, and time entries (automatic from status changes).
- **Offline**:
  - The service worker caches the app shell.
  - IndexedDB keeps visits for today plus `MobileOptions.OfflineDays` (default 2), with their work orders, checklists, asset summaries and van stock.
  - Every change is a command in an **outbox** with a client-generated idempotency key, replayed in order when online. Photos upload in chunks and resume.
- **Conflict policy**:
  - The server wins on schedule and assignment. A reassigned visit shows a "moved" notice and its outbox commands are rejected with reasons.
  - Field data merges per item: checklist answers per question with last-writer-by-timestamp, notes appended, and parts as additive movements.
- **GPS** (WD12): pings every `GeoOptions.PingIntervalSeconds` (default 60) while on shift and with an active or next visit, only after consent is recorded (`WorkerLocationConsent`: UTC, version of the notice text). The service worker never tracks in the background off shift.

## 17. Optimization engines

| Seam | Problem | Built-in implementation | OR-Tools add-on |
|---|---|---|---|
| `IShiftScheduleSolver` | Assign templates to workers and days to cover interval requirements under rules, availability, contracted hours and preferences, minimizing a weighted cost (under-coverage ≫ over-coverage, cost, unmet preferences, unfairness) | Greedy construction (largest coverage deficit first, best-fitting worker and template by marginal score), then local search (swap, move, replace template) with a time limit (`SolverOptions.TimeLimitSeconds`, default 30) and a deterministic seed | CP-SAT model (boolean variables per worker, day and template; coverage soft constraints per interval; rules as hard or soft constraints) |
| `IDispatchOptimizer` | Vehicle routing with time windows, skills, multi-resource (crew) visits, pinned visits, shift windows, breaks and depots (home bases) | Cheapest-insertion construction, then relocate, swap and 2-opt within a time limit, using the `ITravelTimeProvider` matrix | OR-Tools Routing (time dimension, disjunctions with penalties for unassigned, vehicle-specific allowed visits from skills and territory) |

- Both seams take an immutable problem snapshot and return a **proposal** (never writes). Applying a proposal goes through the normal services, so locks, rules and notifications all apply.
- Solver runs are background jobs with progress, cancellation and stored run statistics (objective, runtime, solver, gap).
- The OR-Tools module is separate (WD5). The plan checks its native runtime packages on Windows, Linux x64 and Linux arm64 containers before committing to the version.

## 18. Cross-cutting

### 18.1 Workflows

All events are raised after commit, carry stable ids and a `ChangedBy` (loop guard as in Calendars), and appear in the designer with display drivers.

| Module | Events | Tasks |
|---|---|---|
| Skills | Certification Expiring, Certification Expired, Skill Assigned | Assign Skill, Add Certification Record |
| Workforce | Worker Created, Worker Terminated, Team Membership Changed | Create Worker, Update Worker, Add To Team |
| Shift Planning | Schedule Published, Shift Assigned, Shift Changed, Shift Removed, Open Shift Posted, Open Shift Claimed, Shift Trade Requested, Shift Trade Approved, Shift Starting Soon (reminder ledger), Coverage Gap Detected | Create Shift, Move Shift, Remove Shift, Post Open Shift, Find Available Workers, Publish Schedule |
| Time Off | Time Off Requested, Approved, Declined, Canceled, Waitlist Promoted, Balance Low | Request Time Off, Approve Time Off, Decline Time Off, Adjust Balance, Get Balance |
| Labor Rules | Rule Violation Overridden, Predictability Pay Recorded | Evaluate Rules |
| Time Clock | Punched In, Punched Out, Attendance Exception Raised, Timesheet Submitted, Timesheet Approved, Attendance Points Threshold Reached | Record Punch, Approve Timesheet, Excuse Exception |
| Contact Center WFM | Agent Out Of Adherence, Agent Back In Adherence, Forecast Published, Staffing Gap Detected (intraday, threshold) | Offer Voluntary Overtime, Offer Voluntary Time Off, Add Adherence Exception, Reforecast Day |
| Work Orders | Work Order Created, Status Changed, Completed, Closed, SLA Warning, SLA Breached, Agreement Work Order Generated, Agreement Expiring | Create Work Order, Update Status, Add Line, Get Work Order, Create Invoice |
| Dispatch | Visit Scheduled, Assigned, Dispatched, En Route, Arrived, Completed, Incomplete, At Risk, ETA Changed | Find Candidates, Assign Visit, Reschedule Visit, Optimize, Send Customer Notice |

Workflow activities follow `WorkflowActivityViewsArchitectureTests`, and their schemas go in `Recipes.Core/Schemas/Workflows/Activities`.

### 18.2 APIs

Minimal APIs with OpenApi tags. Every endpoint authorizes by permission plus scope (WD22) and accepts an `Idempotency-Key` header on POST.

- `/api/workforce/v1`: workers, teams, skills, availability, shifts (query by range and team or worker; create, move, delete), schedules (generate, publish), open shifts, trades, time-off types, balances, requests, punches, timesheets, rule evaluation, and a `me` group (my schedule, my balances, my requests, punch) for the Portal and native apps.
- `/api/workforce/v1/contact-center`: planning groups, forecasts, staffing, adherence (real-time snapshot, daily).
- `/api/work-orders/v1`: types, work orders, lines, checklists, attachments, signatures, agreements, assets (`/api/assets/v1`).
- `/api/dispatch/v1`: visits, candidates, assign, status, optimize (async job with status endpoint), board snapshot, the mobile sync endpoint (batched outbox commands with per-command results), and location pings.
- `/api/geo/v1`: geocode, territory resolve (admin and internal).

### 18.3 AI tools

`AddCoreAITool<T>`, in startups with `RequireFeatures("CrestApps.OrchardCore.AI")`. Every tool runs as the calling user with the same permission and scope checks.

| Tool | Example |
|---|---|
| `workforce_who_is_working` | "Who's working Saturday in the Phoenix team?" |
| `workforce_my_schedule`, `workforce_request_time_off`, `workforce_time_off_balance` | Self-service through chat or the AI voice agent |
| `workforce_find_cover` | "Find someone to cover Sam's shift tomorrow 9 to 5": candidates with rule checks |
| `workforce_draft_schedule` | Starts a generation run for a period and summarizes coverage and violations |
| `workforce_explain_violations` | Plain-language explanation of rule violations on a shift or schedule |
| `wfm_forecast_insight` | Explains forecast vs actual, suggests adjustments with reasons (the planner accepts them) |
| `wfm_adherence_summary` | "Who's out of adherence right now and why?" |
| `work_order_create`, `work_order_status`, `work_order_summarize` | Create from a customer conversation, get status, summarize history for a technician |
| `dispatch_find_technician`, `dispatch_optimize_and_explain`, `dispatch_reschedule_visit` | The dispatch assistant: proposes, explains and applies on approval |
| `dispatch_where_is_my_technician` | Customer-facing (AI agent or voice): ETA and status for the caller's visit, verified by phone number or token |

### 18.4 Reports and dashboards

Report data sources (`AddDataSource<T>()`, skill `crestapps-report-data-source`), each with record data sets:

- **Workforce**: workers, team memberships, shifts (occurrences with segments), schedule changes, coverage (required, scheduled, gap per interval), open shifts, trades, availability.
- **Time Off**: requests, ledger, balances by date.
- **Labor Rules**: violations and overrides, predictability pay.
- **Time Clock**: punches, timesheet lines, exceptions, attendance points.
- **Contact Center WFM**: forecast vs actual per interval, staffing plan, intraday gap, adherence segments, adherence daily.
- **Work Orders**: work orders, lines, checklist answers, SLA results.
- **Dispatch**: visits, status durations, travel vs on-site time, utilization per resource and day.
- **Assets**: assets, meter readings, service history.

Coded reports (with dashboard widgets) for the standard KPIs:

| Area | KPIs |
|---|---|
| Workforce | Scheduled hours vs contracted, overtime %, schedule changes after publish, predictability pay, open-shift fill time, swap fill rate, absence rate, time-off liability (balance × rate) |
| Contact center | Service level, ASA, abandonment, occupancy, utilization, shrinkage (planned and unplanned), adherence %, conformance %, forecast accuracy (MAPE/WAPE), over and under staffing by interval |
| Field service | First-time fix rate, SLA compliance (response and resolution), mean time to repair, jobs per technician per day, travel time vs on-site time, on-time arrival %, repeat visits within N days, parts consumption by van, revenue per work order |

### 18.5 Permissions

Each feature declares permissions with `LocalizationSource` descriptions and stereotypes. Global permissions are combined with scope (WD22):

- **Workforce**: ManageWorkers, ViewWorkers, ManageTeams, ManageActivityCodes, ViewSensitiveWorkerData (date of birth, pay).
- **Skills**: ManageSkills, ManageCertifications, ViewCertifications.
- **Shift Planning**: ManageSchedules, PublishSchedules, ViewTeamSchedules, ManageOpenShifts, ApproveShiftTrades.
- **Time Off**: ManageTimeOffPolicies, ApproveTimeOff, AdjustTimeOffBalances, RequestTimeOff (self).
- **Labor Rules**: ManageLaborRules, OverrideLaborRules.
- **Time Clock**: ManageTimeClockPolicies, ApproveTimesheets, EditPunches, ExportPayroll, ViewPayRates, UseKiosk.
- **Portal**: UseWorkforcePortal.
- **Contact Center WFM**: ManageForecasts, ViewIntraday, ManageIntraday, ViewAdherence, ManageAdherenceExceptions.
- **Geo**: ManageTerritories, ViewWorkerLocations (audited).
- **Assets**: ManageAssets, ViewAssets.
- **Work Orders**: ManageWorkOrderTypes, CreateWorkOrders, EditWorkOrders, ViewWorkOrders, ManageAgreements, InvoiceWorkOrders.
- **Dispatch**: DispatchWork, ViewDispatchBoard, OptimizeSchedules, UseTechnicianApp.

Default stereotypes: Worker (self-service), Team manager, Scheduler, WFM analyst, Dispatcher, Technician, Payroll admin.

### 18.6 Recipes, deployment, setup

- **Recipe and deployment steps** (each with a JSON schema in `Recipes.Core/Schemas/Steps/` and covered by `DeploymentStepSerializationArchitectureTests`):
  - `Skills`, `Certifications`
  - `WorkforceTeams`, `Workers` (excluding sensitive fields unless the step opts in)
  - `ActivityCodes`, `ShiftTemplates`, `ShiftPatterns`, `CoverageRequirements`
  - `TimeOffTypes`, `TimeOffPolicies`, `TimeOffBalances` (opening), `LaborRuleSets`, `TimeClockPolicies`, `PayPeriodCalendars`, `PayCodes`
  - `PlanningGroups`, `ForecastEvents`, `AdherenceRuleSets`
  - `ServiceTerritories`, `AssetTypes`, `Assets`, `WorkOrderTypes`, `ChecklistTemplates`, `ServiceLevelPolicies`, `ServiceAgreements`, `DispatchObjectives`
- **Sample recipes**:
  - "Contact center workforce": teams, activity codes, templates, a planning group, adherence rules and an example rule set.
  - "Field service": territories, a crew, vehicles, work order types, checklists and an SLA policy.
  
  Each recipe enables only its features.
- **Wizards** (`IWizardDefinitionProvider`): "Set up workforce scheduling" and "Set up field service".

### 18.7 Docs (definition of done for every slice)

User Manual and Technical Manual pages for every feature, cross-linked in front matter, with both sidebars updated:

- `user-manual/workforce/*`, `user-manual/field-service/*`
- `technical-manual/workforce/*`, `technical-manual/field-service/*`

Each slice also includes a feature reference, a changelog entry, module READMEs, the agent skills (`crestapps-workforce`, `crestapps-field-service`, extending `crestapps-calendar-types` with the Workforce type), `npm run check:manuals` and `npm run build`. The docs avoid `{#id}` and bare `{x}` (MDX), use admonition titles `:::note[Title]`, and contain no code in the User Manual.

## 19. Changes to existing modules (all additive)

| Module | Change | Why |
|---|---|---|
| Calendars | `ICalendarAccessContributor` seam (returns an access level for a user and calendar). `CalendarAccessService` takes the highest of its own result and the contributors' | Team-based access to Workforce calendars without syncing shares (§5.4) |
| Calendars | `ICalendarEventManager.ApplyBatchAsync` (create, update and cancel many source-owned events in one validated pass, one lock per calendar), or chunked publishing documented if the batch API isn't needed after profiling | Schedule publish performance |
| Calendars | The Feeds token can be scoped to "occurrences where user X is an attendee across calendars of type T" | The worker's personal iCal feed |
| Scheduling | `ISchedulingSlotProvider` seam: an appointment type can name a slot provider | Arrival-window self-booking from Dispatch capacity (§15.5) |
| Scheduling | "Hours from shifts" reads shifts from **any** Workforce calendar the resource's user attends, not one `ShiftCalendarId` (keep `ShiftCalendarId` as an optional filter) | Workers on several teams |
| Contact Center | Interval statistics aggregator and data sets (§11.1) | WD18 |
| Contact Center | Skills adapter and migration (§5.1); feature dependency on Skills | WD8 |
| Contact Center | An after-commit agent presence observer contract, if the existing observers don't expose presence changes with reason codes in a stable payload | Real-time adherence |
| Addresses | Optional `Latitude`, `Longitude`, `GeocodedUtc`, `GeocodeSource` on `Address`; a "pin on map" editor control when Geo is on | Geo for service addresses and home bases |
| Inventory | Stock locations move from the `InventorySettings` list to a `StockLocation` store (migration copies the existing ones and keeps their ids), with `OwnerKind` and `OwnerId` (for example a vehicle asset) | Hundreds of vans; locations owned by assets |
| Inventory | `StockMovement` gains `ReferenceType` and `ReferenceId` (keeping `OrderId`), and a new movement kind `Consumed` | Parts used on work orders |
| Resources | Vendor Leaflet, Leaflet.markercluster, Leaflet-Geoman free, vis-timeline (copy assets in `Assets.json`, licenses kept) | Maps and timelines |
| Omnichannel | "Create work order" subject action and activity link (startup with `RequireFeatures`) | CRM to field service |
| CRM contact and Company pages | Assets tab, Work orders tab (drivers in startups) | Installed base and service history |

Every migration is additive (`MigrationAdditiveOnly`), and none opens a second connection (the SQLite deadlock rule). Index tables use `IndexTableIndexCreation` (the concurrent-activation rule), and indexes are named within SQL Server limits (Document.Type 255 and the column length rules).

## 20. Non-functional requirements

- **Time:** all wall-clock times are stored with an IANA zone, and UTC is derived (the Calendars rule). Day boundaries use the team's or worker's zone. Overnight shifts belong to the day they start. DST days (23 and 25 hours) are tested in planner, coverage, timesheets and adherence. `IClock` is used everywhere. `DateTime` values in YesSql use the converter (the wall-clock shift trap).
- **Concurrency:** distributed locks per worker, allotment, resource and calendar, always taken in sorted order. Version checks on scenarios and visits (`ConcurrencyException` propagates). Idempotency keys on ledgers, punches, outbox commands and webhook-like inputs.
- **Scale targets (per tenant):**
  - 5,000 workers; 500 per team period
  - 2 years of 15-minute intervals for 200 queues
  - 50,000 visits a month
  - 200 dispatch resources on one board
  - Planner board loads 500 × 7 days in under 2 seconds (paged rows, virtualized).
  - No unbounded loads (`UnboundedCollectionLoad` guard); every list is paged; aggregates come from indexes.
- **Database:** queries are index-backed and tested against a large seeded SQLite database, then reviewed for SQL Server plans. Production is IO-bound, so there are no per-render scans.
- **Privacy:**
  - Date of birth, pay rates and location pings are permission-gated and audited (Audit Trail).
  - Location retention and a consent record are required.
  - There are personal data export and erase hooks for workers. Terminated workers' sensitive data can be purged after a retention period while hours history is kept.
- **Security:**
  - Kiosk PINs are hashed and attempts are rate-limited.
  - Tracking and manage links are signed and expire.
  - Every admin POST has an anti-forgery token (guard), and every API checks scope.
  - Logs pass through `SanitizeLogValue()`.
- **Reliability:**
  - Background jobs resume from cursors and are watched for stalls.
  - A provider outage (geo, push, SMS) never blocks scheduling; it falls back or retries.
  - Every after-commit observer is idempotent.
- **Front end:**
  - Vite assets pipeline, no jQuery, native selectpicker API.
  - Dark-mode-safe classes (`btn-theme` for selectpickers).
  - Decimal-comma-safe number inputs (`AddInvariantNumberInputs`).
  - Admin lists on `AdminList` with `SummaryAdmin` and `SummaryAdminGrid`.
  - Every page declares `<breadcrumb>`.
- **Localization:** all strings are localized and all dates and numbers culture-formatted. Rule and policy messages are localizable.

## 21. Phases and delivery

Three pull requests (WD21), each a commit (or a few) per slice. Each slice is usable and tested on its own, and its definition of done includes tests, docs (§18.7), PublicApi baselines, recipe and deployment schemas, architecture guards and a full UI pass on a fresh site before handing over.

**Prerequisite:** PR #1222 merged into main. Slices that touch Calendars or Scheduling (§19) land in PR A.

### PR A: Workforce core

| Slice | Content | Acceptance |
|---|---|---|
| A1 | Skills module, certifications, expiry task, Contact Center migration and adapter | Existing tenant upgrades with every skill and proficiency kept; routing tests unchanged; certification expiring event fires once per offset |
| A2 | Workforce base: workers, teams, hierarchy, scoping, activity codes, availability, Workforce calendar type, `ICalendarAccessContributor`, segment busy source, Contact Center agent sync | A manager sees only their teams; a Scheduling resource with Shifts mode gets hours from a published shift and loses its break |
| A3 | Shift planning I: templates, patterns, coverage requirements, periods, scenarios, the planner board, copy week, publish (chunked) and republish, change log, notices | Publish 500 workers × 1 week in a background job; republish changes only what changed; shifts read-only in the calendar UI |
| A4 | Shift planning II: built-in `IShiftScheduleSolver`, open shifts, trades, Scheduling multi-calendar shifts | Auto-schedule meets 95% or more of target coverage on the seeded sample in under 30 seconds; trade validated by rules |
| A5 | Time off: types, policies, accrual runs, ledger, requests, approvals queue, allotments with auto-approve, waitlist, blackout, pending event source | Accrual idempotent across reruns; allotment race (two concurrent requests for the last slot): one wins; waitlist promotion on cancel |
| A6 | Labor rules: catalog, rule sets, evaluator at every enforcement point, predictability pay, example rule sets | Block rule stops publish; RequireOverride records reason; change inside notice window records predictability pay |
| A7 | Portal (PWA online): worker and manager pages, web push, iCal feed, notification preferences | Lighthouse PWA installable; push received on desktop Chrome and Android; feed subscribes in a calendar app |
| A8 | Time clock: policies, punches (web, kiosk, Contact Center mapper), pay periods, pay codes, timesheet calculator, approvals, exceptions, payroll CSV export; attendance points | DST and overnight timesheet tests; Contact Center agent gets punches from sign-in without double counting; export locked once |
| A9 | Workforce API, AI tools, reports and dashboards for A1 to A8, sample recipe, wizard | Report Builder lists the Workforce data sources; AI "who's working" respects scope |

### PR B: Contact center workforce management

| Slice | Content | Acceptance |
|---|---|---|
| B1 | Contact Center interval statistics: aggregator, cursor, rebuild, data sets | Interval totals equal existing daily reports for the same range; rebuild resumable; no full scans in the query plans |
| B2 | Adherence: rule sets, real-time engine, board, alerts, history jobs, exceptions, reports, portal page | Live board updates in under 2 seconds after a presence change; adherence % matches hand-computed fixtures |
| B3 | Forecasting: planning groups, events, models, backtest, forecasts, Erlang C, Erlang A, concurrency, shrinkage, staffing plan, forecast-driven coverage | Erlang results match published tables; Holt-Winters fixture tests; coverage from forecast feeds A4's solver |
| B4 | Intraday: dashboard, reforecast, VOT/VTO offers, break moves, activity scheduler | VOT accepted from the portal extends the shift without predictability pay |
| B5 | Capacity plan (long term), AI tools, KPI dashboards, sample recipe updates | — |

### PR C: Field service

| Slice | Content | Acceptance |
|---|---|---|
| C1 | Geo: points, Address change, haversine provider, geocoding seam, territories, map component, Leaflet vendoring | Territory resolve by polygon and postal code; addresses geocoded in the background with retry |
| C2 | Geo providers: Azure Maps, Google Maps, OSRM | Each passes the provider contract test suite against recorded responses |
| C3 | Assets and Fleet: types, assets, hierarchy, meters, Equipment calendars, Inventory `StockLocation` store and movement references, van stock | Existing inventory locations keep their ids after the migration; a vehicle can't be double-booked |
| C4 | Work orders: types, lifecycle, checklists, SLA clock, lines, parts consumption, billing through Transactions, Omnichannel action, Scheduling observer | SLA breach fires once; part used decrements van stock with a work-order reference; invoice created with ReferenceType WorkOrder |
| C5 | Service agreements (preventive maintenance) | Generation idempotent; RRULE occurrences across DST |
| C6 | Dispatch I: visits, candidate finder, assignment with locks, calendar events per resource, board (timeline, unscheduled panel, map), SignalR | Two dispatchers assigning one technician to overlapping visits: one wins; candidate excludes expired certification |
| C7 | Dispatch II: built-in `IDispatchOptimizer`, proposals, crews and equipment, disruption re-check, customer notices, tracking page, ETA, Scheduling slot provider (arrival windows) | Optimizer reduces travel against a seeded naive plan; tracking link expires after completion |
| C8 | Technician mobile: field mode, offline outbox, photos, signatures, parts, GPS with consent, sync endpoint | Airplane-mode test: complete a visit offline, reconnect, everything syncs once; reassigned visit's commands rejected with reasons |
| C9 | OR-Tools add-on module (both solvers) | Same fixtures as the built-ins, better or equal objective; container smoke test on Linux |
| C10 | Field-service API, AI tools (dispatch assistant, where-is-my-technician), reports and KPIs, sample recipe, wizard | — |

## 22. Testing strategy

- **Pure engines first (test-first):**
  - timesheet calculator
  - accrual engine
  - rule evaluator (every rule)
  - Erlang C and Erlang A, Holt-Winters and interval curves
  - adherence segmenting
  - SLA clock
  - both built-in solvers on fixtures with known optima
  - ETA math
- **DST matrix:** America/New_York, Europe/London, Australia/Lord_Howe (30-minute shift), and a zone without DST, across the planner, coverage, publish, time off partial days, timesheets, adherence and agreements.
- **Concurrency on SQLite:** last allotment slot, same technician double-assign, concurrent publish and shift edit, open-shift double claim, offline outbox replay twice.
- **Migrations:** the Contact Center skills migration and the Inventory location migration on a copy of a seeded database with existing data. Upgrade tests keep ids.
- **Architecture guards:** dependency graph (Contact Center never references Workforce), PublicApi baselines, workflow activity views, deployment serialization, site settings permissions, unbounded loads, file size ratchet.
- **Playwright:** planner drag and publish, time-off request to approval, portal install and push (where the CI browser supports it), adherence board live update, dispatch drag-assign and optimize, offline visit completion (network emulation). Waits use real conditions, not timeouts (the flakiness rules).
- **Live tests:** real Contact Center agent sessions for adherence, and a real phone and SMS for the "en route" message and tracking page. The rule is regression tests after the live test passes.

## 23. Out of scope

- Payroll calculation (taxes, deductions). Export only.
- HR: recruiting, onboarding, performance reviews, benefits.
- Quality management and agent coaching scorecards (AI conversation quality already covers part of this).
- Gamification.
- Multi-skill simulation forecasting (Erlang approximation only in v1).
- Native mobile apps (the API allows them later).
- Two-way sync of worker schedules to Google or Microsoft 365 (that waits for the Calendars providers, plan phase 4.4).
- A crowd or marketplace of external technicians.
- IoT-triggered work orders (possible today through the API and workflows; no device integration).
- Fleet telematics integration (the GPS comes from the technician's device only).

## 24. Risks

| Risk | Mitigation |
|---|---|
| Contact Center skills migration on production tenants | Same ids, read model kept, migration tested on a copy, routing untouched, feature dependency auto-enabled |
| Interval aggregation load on an IO-bound production database | Incremental cursor, batch sizes, off-peak rebuild, index-only reads, measured on a production-size copy before release |
| Offline sync complexity | Commands with idempotency keys, server-authoritative schedule, per-item merge rules, an extensive replay test suite |
| OR-Tools native packaging | Separate add-on module; built-in solvers always work; container tests before release |
| Map and geo provider terms and costs | Haversine default, provider-specific caching limits, rate limits, keys per tenant |
| Legal accuracy of labor rules | No presets shipped, examples labeled; rules are configurable building blocks |
| Timeline library fit (vis-timeline) | Spike in A3 against 500 rows × 7 days virtualized; fallback is a custom CSS grid component |
| Scope size | Three PRs, each slice shippable; stop points after A5, B2 and C6 are each useful products |

## 25. Glossary

| Term | Meaning |
|---|---|
| Activity code | What a worker does during a segment (work, break, meal, training …) |
| Adherence | Share of scheduled time a worker's actual state matched the scheduled activity |
| Allotment | How many people (or hours) may be off in a scope on a date or interval |
| AHT | Average handle time (talk + hold + wrap-up) |
| ASA | Average speed of answer |
| Conformance | Actual worked time compared with scheduled worked time, regardless of when |
| Coverage requirement | Required headcount per interval for a scope |
| Crew | A team scheduled as one unit for field work |
| Erlang C / Erlang A | Queueing models that give required agents for a traffic load and service goal (A includes abandonment) |
| First-time fix rate | Share of work orders completed on the first visit |
| Open shift | A shift with no worker, offered to eligible workers |
| Planning group | Queues and channels forecast and staffed together |
| Predictability pay | Premium owed when an employer changes a published schedule inside the notice window |
| Service level | Share of contacts answered within a threshold (for example 80% in 20 seconds) |
| Shrinkage | Share of paid time not available for work (breaks, training, absence) |
| Visit | A scheduled trip by one or more resources to do (part of) a work order |
| VOT / VTO | Voluntary overtime and voluntary time off offered intraday |

## 26. Progress

- 2026-10-30: plan written. Decisions WD1 to WD12 agreed with the product owner. Nothing built yet; waiting on PR #1222.
- 2026-10-30: PR #1222 merged into main. PR A is built on the plan's branch (PR #1229).
- A1 built (Skills module, `CrestApps.OrchardCore.Skills`, with Abstractions and Core). It differs from §5.1 in these ways:
  - The proficiency scale is fixed at 1 to 5 (`SkillProficiency`), the scale Contact Center already used, instead of an admin-defined `ProficiencyScale`.
  - A person holds each skill once. An assignment has one effective date range, not a history of ranges.
  - Contact Center does **not** depend on Skills, and there is no migration. Two startups with `[RequireFeatures(Skills)]` share the catalog instead:
    - `SharedSkillsStartup` on the Queues feature replaces `IContactCenterSkillStore` with a store over the shared catalog. Missing Contact Center skills are copied on first read, with the same ids. `ContactCenterSkillMirrorHandler` mirrors every catalog change back into the Contact Center store, so disabling Skills loses nothing.
    - `SharedAgentSkillsStartup` on the Agents feature keeps `AgentProfile.SkillProficiencies` (keyed by skill **name**, as Contact Center routing uses) and the user's assignments in step both ways. A fingerprint on the profile keeps presence-only saves free. A background task backfills agents saved earlier.
    - A migration could not be used, because it could run before the Skills tables exist: Queues doesn't depend on Skills.
  - `SharedAgentSkillsHandler` resolves its collaborators lazily, which breaks an assignment-service-to-handler construction cycle. It is listed in `DependencyInjectionArchitectureTests`.
  - Skills depends on CrestApps Resources, which the user picker needs.
  - "Create a worker profile for every agent" (§5.2) moves to `Workforce.ContactCenter` in PR B, because Workforce must not reference Contact Center.
- A2 built (Workforce module, `CrestApps.OrchardCore.Workforce`, with Abstractions and Core): workers, the team tree, manager scoping, activity codes (nine seeded by the migration), availability as text lines, workers' skills and certificates, a Workforce calendar per team, `ICalendarAccessContributor` in Calendars, `ShiftSegmentBusyTimeSource`, and the `Workforce` recipe and deployment step. It differs from §5 in these ways:
  - `Shift` is a **free** event type, not busy. Scheduling's "hours from shifts" asks free/busy for the resource's user, so a busy shift would block every slot. The non-work segments are reported as busy through the busy-time source instead (acceptance test: a staff member is offered 9:00 to 16:00 but not the 12:00 meal).
  - Team defaults (rule set, time-off policies, time clock policy, pay period) are not fields yet. Each slice adds the one it needs.
  - `WorkerAvailability` approval (§5.6) is left to the Portal slice. Availability is edited by managers.
  - The team list shows each team's parent as a badge instead of indenting, so it pages correctly.
  - Worker workflow events (§18.1) move to A9, with the API and AI tools.
  - Not verified locally: the docs site build. The `@swc/html` native binding can't load, because of an ACL on `%LOCALAPPDATA%`; `npm run check:manuals` passes. The Contact Center side was covered by tests, not exercised live.
- A3 built (the **Workforce Shift Planning** feature, `CrestApps.OrchardCore.Workforce.ShiftPlanning`): shift templates with segments, rotations, coverage requirements, schedule periods with a primary scenario, draft shifts, apply rotations, copy from another period, coverage by interval against a draft or the published shifts, publish and republish by shift key, and the change log. It differs from §6 in these ways:
  - The planner board is a server-rendered week grid (workers by days, with a coverage row), not a vis-timeline board. Shifts are edited on their own page. A drag-and-drop board can come later without changing the services.
  - Publishing runs in the request, not as a chunked background job, and the 500-workers-by-a-week acceptance test isn't written yet. It touches only the shifts that changed, so a republish is cheap.
  - Notifying workers when a schedule is published is left to the Portal slice (A7). `ISchedulePublishHandler` is the seam it will use.
  - Only the primary scenario has screens. The model and services already take a scenario id for what-if drafts.
  - `Locked` exists but nothing sets it yet; the time clock slice (A8) locks periods with approved timesheets.
  - A coverage requirement for a skill only narrows the requirements; the scheduled count doesn't yet check the workers' skills. The solver slice (A4) adds that.
  - `Shift` events no longer require an attendee, because a worker without a user account or an email can't be one. The worker is named in `ShiftData`, and is the attendee when they have an account or an email. The UI pass found this: every shift of such a worker was refused.
  - A publish where every shift fails still raises the version. With the attendee fix, nothing is known to fail this way.
