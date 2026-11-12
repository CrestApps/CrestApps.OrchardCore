# Staff: one building block for the people who work for the business

Status: proposed 2026-11-11, waiting for decisions (section 9).

## 1. Why

The solution has no single record for "a person who works for us". Every module that needs one built its own, or borrowed Workforce's:

- **Workforce** has `WorkerProfile` (`src/Abstractions/CrestApps.OrchardCore.Workforce.Abstractions/Models/WorkerProfile.cs`). It is already a full employee record: employee number, external HR id, job title, employment type and status, hire, seniority and termination dates, date of birth, manager, dated team memberships, time zone, home base, and, in its `Properties`, dated pay rates, a kiosk PIN and portal settings. But it lives inside a scheduling module, so a site that wants employees without shift planning has none, and every other module that needs a staff member has to depend on Workforce.
- **Contact Center** has `AgentProfile` (`src/Core/CrestApps.OrchardCore.ContactCenter.Core/Models/AgentProfile.cs`). It is routing state (presence, queues, reservations, skills, concurrency), but it also copies `UserName` and `DisplayName`. It has no manager, no team and no employment data. `Workforce.ContactCenter` creates a `WorkerProfile` for every agent with the job title "Agent" just to give the agent an employee record.
- **Field service** (Dispatch, WorkOrders, Assets/Fleet, Geo) stores `WorkerId`s everywhere and reads names, home base and time zone straight from `WorkerProfile`.
- **Scheduling** has its own people record: `SchedulingResource` with `Kind = Staff`, a copied name, its own time zone and its own working hours.
- **Telephony** has `TelephonyExtension` per user with a copied user name and a typed display name. Outbound lines, carrier credentials and the voicemail greeting are three more per-person records in three more modules.
- **Teams** has no roster. Its only member provider is Workforce, and it drops every worker without a login (`WorkerTeamMemberProvider.cs`).
- **Skills** knows one person under two ids (`SkillSubjectKind.User` and `SkillSubjectKind.Worker`). The audit's defect 6, Contact Center skills that Workforce and Dispatch never see, comes from this.
- **Geo** stores territory members as `Kind = "Worker"`.
- **Point of Sale, Orders, Inventory, Marketplace, TimeTracker, Calendars, Omnichannel** record the signed-in user and copy their name (`Order.StaffName`, `RegisterShift.OpenedByName`, `StockMovement.ActorName`, `Vendor.UserNames`, `TimeEntry.UserName`, `CalendarAttendee.Name`, `OmnichannelActivity.AssignedToUsername`).
- **Every people picker lists every user, customers included**: lead and opportunity owner (`UserPickerField` with `DisplayAllUsers = true`), activity assignee, bulk assign, Scheduling staff, calendar owner and attendees, extension owner, time entry user, certificate holder. `IUserSearchService` and the `UserPicker` view component accept a role filter, but no caller passes one.
- **"Staff" has no definition.** It is a commerce role list (`CommerceConstants.StaffRoles`), a POS register's roles, a permission, or the existence of one of the records above, depending on the module. There is no manager or reporting line outside Workforce, and no account manager or sales rep anywhere.

By the reuse ladder in `AGENTS.md` this is step 4: a second module (and by now a sixth) needs a concept that only one module has. We extract it into a building block and migrate the copies in the same plan.

## 2. What Staff owns and what it does not

**Staff owns** the person as an employee: who they are at work, how they are employed, who they report to, where they work, when they joined and left, and the lifecycle events around that (hired, changed jobs, changed manager, went on leave, left).

**Staff does not own**, and keeps referring to the owner:

| Concept | Owner (unchanged) | How Staff uses it |
| --- | --- | --- |
| The login, name and avatar | Users (`IDisplayNameProvider`, `UserFullNamePart`) | A linked staff member's name and e-mail come from the user. Staff stores a name only for people without a login. |
| Groups of people | Teams | Staff becomes the `ITeamMemberProvider` (it holds the dated memberships Workforce holds today). |
| Skills and certifications | Skills | Staff is one skill subject kind; a staff member and their user share skills. |
| Addresses, phones, coordinates | Addresses, PhoneNumbers, Geo | `NamedAddress`/`Address` for work location and home base, `PhoneNumber` for work phone, `GeoPoint` through Geo. No raw latitude/longitude. |
| Business hours, time zones | BusinessHours, Calendars (`CalendarTime`) | Standard working hours are an `HoursSchedule`; the time zone feeds the shared time-zone chain (person, team, site). |
| Shifts, time off, timesheets, labor rules | Workforce | Workforce extends the staff member with its scheduling data. |
| Time entries | TimeTracker | Unchanged; keyed by user. |
| Routing state of an agent | Contact Center | `AgentProfile` stays the routing record. It is high-churn and must never be written on the staff document. |
| Personal data export and erasure | PersonalData | Staff ships the contributor for the staff record. |

## 3. The building block

Three projects, as `AGENTS.md` requires:

- `src/Abstractions/CrestApps.OrchardCore.Staff.Abstractions`: `StaffMember`, value types, contracts, feature IDs, permissions names, events.
- `src/Core/CrestApps.OrchardCore.Staff.Core`: store, indexes, manager, directory, hierarchy, the base display driver for "a staff section on another editor", the staff picker view component.
- `src/Modules/CrestApps.OrchardCore.Staff`: manifest, admin list and editor, user editor section, endpoints, migrations, recipe and deployment steps, integrations.

### 3.1 The record

`StaffMember : CatalogItem, IModifiedUtcAwareModel` (a CrestApps.Core catalog record, like `WorkerProfile` today; see decision 1).

| Field | Notes |
| --- | --- |
| `UserId` | Optional. Unique when set. Kiosk-only and seasonal people have none. |
| `DisplayName` | Used only when `UserId` is empty. Otherwise resolved through `IDisplayNameProvider` at read time and never copied. |
| `WorkEmail`, `WorkPhone` | `WorkPhone` is a `PhoneNumber`. When linked, the user's e-mail is the default. |
| `EmployeeNumber` | Unique when set. Also the badge number. |
| `ExternalId` | The id in an outside HR or payroll system. |
| `JobTitle` | Free text, plus an optional `PositionId` later (section 8). |
| `EmploymentType` | Open string kind with the built-ins FullTime, PartTime, Seasonal, Temporary, Contractor, Subcontractor (today an enum in Workforce). |
| `Status` | Active, OnLeave, Suspended, Terminated. |
| `HireDate`, `SeniorityDate`, `TerminationDate` | `DateOnly`. Pure dates, never converted to UTC. |
| `DateOfBirth` | Sensitive. Behind `ViewSensitiveStaffData`. |
| `ManagerId` | Another staff member. The reporting line. |
| `PrimaryTeamId` + dated `TeamMemberships` | Moved from `WorkerProfile` as they are (`TeamId`, `Role`, `EffectiveFrom`, `EffectiveTo`). |
| `WorkLocation` | A `NamedAddress` (the office, store or depot). |
| `HomeBase` | An `Address` with a `GeoPoint` (start of the day for field staff), geocoded through Geo's queue. |
| `TimeZoneId` | Empty means the team's, then the site's. |
| `Properties` | Extension data other modules put on the person (`Put<T>`/`TryGet<T>`), for example Workforce's hours and availability. |

Every change to job title, status, employment type, manager, team or work location appends a `StaffHistoryEntry` (who, when, from, to, reason) to an append-only log, so payroll and compliance can answer "who was their manager on 3 March". Dated team memberships and dated pay rates already cover the two most common questions; the log covers the rest without making every field effective-dated.

### 3.2 Extension points (from day one)

- **Driver-based editor**: the staff editor renders through `IDisplayManager<StaffMember>`. Each consumer adds its card with a `DisplayDriver<StaffMember>` in its own `[RequireFeatures]` startup: Workforce adds hours and availability, Contact Center adds the agent card, Telephony adds extension and outbound line, Skills adds skills, Compensation adds pay rates. Staff never names them.
- **User editor section**: one **Staff** section on the user editor (generalizing today's `UserWorkforceDisplayDriver`) that creates or edits that person's staff record in place. Consumer cards render inside it through the same drivers.
- **Life-cycle handlers**: `IStaffMemberHandler` (validating, creating, updating, deleting) and `IStaffLifecycleObserver` with events after save: `HiredAsync`, `StatusChangedAsync`, `ManagerChangedAsync`, `TeamsChangedAsync`, `TerminatedAsync`. These drive offboarding (section 3.4) and workflow events.
- **Indexes others can add**: a consumer adds its own YesSql index provider on `StaffMember` documents when it needs to query its extension data (the way Contact Center adds `AgentQueueMembershipIndexProvider` on agent profiles).
- **Capabilities**: `AddStaffCapability("Technician", ...)`, an open string a module registers to say "this staff member does this kind of work" (agent, technician, cashier, scheduled worker, bookable staff). Pickers and lists filter on it ("pick a technician"). A capability is set by the module that owns it, for example Contact Center marks agents.

### 3.3 Services

- `IStaffMemberManager` (catalog manager): find by id, by user id, by employee number; `EnsureForUserAsync(userId, defaults)` for modules that need a staff record behind a user (Contact Center, POS).
- `IStaffDirectory`: the read side every consumer uses. `SearchAsync(StaffQuery { Text, Status, TeamIds, ManagerId, Capability, HasUser, ActiveOn })`, `GetNamesAsync(ids)`, `IsStaffAsync(userId)`, `GetByUserIdsAsync`. Names come from users for linked staff.
- `IStaffHierarchy`: manager chain, direct and indirect reports, `ManagesAsync(managerUserId, staffId)`. Workforce's `IWorkforceScopeService` and Contact Center supervision build on it.
- `IStaffTimeZoneResolver`: person, then primary team (with parent teams), then site, through `CalendarTime`. This is the person part of the audit's missing `ITimeZoneResolver` (D7), and it ends "the same worker resolves to different zones in Workforce and Dispatch".
- `IStaffBadgeService`: badge (employee number) plus PIN, hashed with PBKDF2, with lockout. Moved from Workforce's time clock so Point of Sale can use the same badge to switch cashiers.

### 3.4 Built-in behavior

- **Staff role (decision 4)**: every active linked staff member is put in a configurable role (default `Staff`, created with no permissions) through `IRoleGrantService`, and taken out when they are terminated. Every role-filtered picker, including Orchard's `UserPickerField`, then shows only staff without code changes, and permission stereotypes can target staff.
- **Offboarding**: on `TerminatedAsync` (on the termination date, by a daily task when it is in the future) consumers clean up through their own observers: Contact Center signs the agent out and clears entitlements, Telephony releases the extension and carrier credentials, Dispatch unassigns future visits, Workforce removes future shifts, the Staff role is revoked, and optionally the user account is disabled (a setting).
- **Pickers and search**: a `StaffPicker` view component and a `Staff.Search` admin endpoint returning `{ value, text }` (the shape `UserPicker` and `ItemSelector` already use), with `valueKind = staff | user` so a field that stores user ids (lead owner, activity assignee) can still restrict the list to staff. The Users search endpoint gains a `staffOnly` flag backed by `IStaffDirectory` for callers that keep `UserPicker`.
- **Parties**: a `Staff` party kind and `StaffPartySource`, so `PartyRef` owners can be staff (`AssetOwnerKind.Internal` gets a person behind it).
- **Admin**: Staff menu with the list on `<admin-list>` (row driver with `SummaryAdmin` and `SummaryAdminGrid`), filters for team, manager, status and capability, the editor with `action-bar-bottom`, breadcrumbs; an optional org chart page.
- **Permissions**: `ManageStaff`, `ViewStaff`, `ViewSensitiveStaffData`, scoped to managed teams and reports through `IStaffHierarchy`.
- **Portability**: `Staff` recipe step and deployment step (matching on id, then employee number, then user), CrestApps `IRecipeStep` schema, `ConfigurationSchema.json` for options.
- **Integrations** as `[RequireFeatures]` startups in Staff: Teams (member provider), Skills (subject kind and skills card), Geo (territory member source, home-base geocoding), Parties, Reports (Staff data set, headcount and turnover), Workflows (Hired, Terminated, Manager changed events), Audit Trail, AI tools (who manages X, who is on team Y, find staff with skill Z).

### 3.5 Features

| Feature | Contents |
| --- | --- |
| `CrestApps.OrchardCore.Staff` | Record, list, editor, user section, directory, hierarchy, picker, search, Staff role, history log, permissions, recipe and deployment, personal data. Depends on Users. |
| `CrestApps.OrchardCore.Staff.Compensation` | Dated pay rates (hourly per activity, salary, cost rate), pay group, `ViewPayRates`. Moved from `WorkerPayRates`. Read by Workforce payroll export and by labor costing (section 5). |
| `CrestApps.OrchardCore.Staff.Badges` | Badge and PIN, kiosk sign-in contract. Used by Workforce Time Clock and Point of Sale. |
| `CrestApps.OrchardCore.Staff.OrgChart` | Org chart page and the AI hierarchy tools. |

## 4. Migrating Workforce onto Staff (keep every id)

The key move: **a `StaffMember` keeps the `ItemId` of the `WorkerProfile` it comes from.** Every stored `WorkerId` (punches, timesheets, shifts, time-off ledgers, visits, `PreferredWorkerIds`, `Fleet.DefaultDriverWorkerId`, territory members, skill subjects, location pings) stays valid without rewriting a single dependent document. Property names like `WorkerId` stay in Workforce and Dispatch models, documented as "the staff member's id", so their public APIs do not churn.

- A data migration in Staff copies each `WorkerProfile` into a `StaffMember` with the same id. Core fields map one to one; `WorkerPayRates` moves to Compensation; the PIN moves to Badges; Workforce's own fields (contracted, minimum and maximum hours, availability, portal settings) move to `WorkforceStaffSettings` in `Properties`. The migration is idempotent and batched, and runs on tenant start like the Teams migration did.
- `IWorkerManager` stays as a thin Workforce service: "workers" are staff members with the Workforce scheduled-worker capability. It is implemented on `IStaffMemberManager`, so Workforce's internal callers keep working while they move over.
- Workforce's drivers become `DisplayDriver<StaffMember>` cards. The Workers list stays as a filtered view of staff ("staff who are scheduled"), for planners.
- `WorkerTeamMemberProvider`, `WorkerSkillSubjectLinkProvider`, `WorkerTerritoryMemberSource`, `WorkerProfilePersonalDataContributor`, `WorkersReportDataSet` and `UserWorkforceDisplayDriver` move to Staff and lose the Workforce dependency.
- Teams' `TeamMember` gains `StaffMemberId` (`UserId` stays for user-based callers), so kiosk-only staff are no longer dropped from team rosters.
- `SkillSubjectKind.Worker` becomes `Staff` with the same stored value (1), so existing assignments keep working; user subjects of people who have a staff record are merged into it. Geo's `"Worker"` member kind is read as `"Staff"`.

## 5. Where the existing modules reuse Staff

This is the list the request asked for: every place in today's solution that gets better on top of Staff.

### Workforce and time
| Module | Today | With Staff |
| --- | --- | --- |
| Workforce | Owns the employee record | Owns only scheduling data on it (section 4). A site can have employees without enabling Workforce. |
| Workforce Time Clock | Badge and PIN on the worker | Uses `Staff.Badges`. Payroll export reads Compensation and `ExternalId`. |
| Workforce manager scope | Own `ManagerWorkerId` walk | `IStaffHierarchy.ManagesAsync`. |
| Workforce notifications | `worker.Email` | Work e-mail from the user or the staff record. |
| TimeTracker | `TimeEntry.UserName` copy; report by user only | Names resolved; time report filters by team, manager and capability through `IStaffDirectory`; entry user picker is staff-only. |
| My Work | Workforce's portal, found from the user | Becomes the staff home page owned by Staff, with `IMyWorkLinkProvider` moved to Staff: Workforce (schedule, time off), TimeTracker (my time), Dispatch (field page), Contact Center (agent workspace) all add their links. |

### Contact Center, Omnichannel and Telephony
| Module | Today | With Staff |
| --- | --- | --- |
| Contact Center agents | `AgentProfile` copies `UserName`/`DisplayName`; `AgentSession` copies them again; no manager or team | Names resolved through `IStaffDirectory`; `EnsureForUserAsync` gives every agent a staff record with the Agent capability (replacing `AgentWorkerService`, which creates a worker titled "Agent"). The routing hot path keeps reading `AgentProfile` only. |
| Agent entitlements | A stand-alone controller form | An **Agent** card on the staff editor (allowed queues and campaigns, queue preferences, skills, voice and messaging concurrency, voicemail greeting). The entitlements list stays as an admin list of agents. |
| Supervision | A supervisor sees queues on their own `AllowedQueueIds` | Optional extra scope: a supervisor also sees the agents they manage (reports and managed teams through `IStaffHierarchy`). |
| Telephony | `TelephonyExtension` copies the user name; outbound lines, Telnyx and Asterisk credentials each keyed by user in different modules | A **Phone** card on the staff editor showing the extension, outbound line and credential status; copied names dropped; released on termination. |
| Omnichannel (CRM) | Lead and opportunity owner, activity assignee, bulk assign, import lead owner, batch filter all list every user | Staff-only pickers (`staffOnly` or the Staff role), and names resolved instead of `AssignedToUsername`. |
| Messaging | Transfer targets come from agent profiles | Agent names from the directory; transfer to a manager or a team through Staff. |
| AI handoff | Hands off to agents only | Can hand off to a named staff member or team when no queue fits. |

### Field service
| Module | Today | With Staff |
| --- | --- | --- |
| Dispatch | Reads `WorkerProfile` for name, home base, time zone, teams; its own `WorkerAvailability` time-zone chain | Reads `IStaffDirectory` and `IStaffTimeZoneResolver`; technicians are staff with the Technician capability. |
| WorkOrders | `PreferredWorkerIds`/`ExcludedWorkerIds` have no editor; `ClosedBy` and checklist `By` store a user name | Staff picker for preferred and excluded technicians; actors stored as user ids and resolved. Labor lines priced from Compensation's cost rate. |
| Assets / Fleet | `Fleet.DefaultDriverWorkerId` has no editor; `AssetOwnerKind.Internal` has no person | Staff picker for the default driver; internal assets can be assigned to a staff member through the `Staff` party kind. |
| Geo | Territory members of kind `"Worker"` from Dispatch | Staff is the member source (kind `"Staff"`). |
| Scheduling | `SchedulingResource` with `Kind = Staff` keeps its own name, time zone and working hours; picker lists every user | A staff resource points at a `StaffMemberId`; name and time zone come from Staff, working hours default to the staff member's standard hours with resource-level overrides; staff picker with the Bookable capability. |
| Bookings | No host or staff at all | Can offer "with whom" on a booking through Scheduling's staff resources. |
| Calendars | Attendee picker lists every user and copies names; "Whose schedule" lists every user | Staff picker for internal attendees and the schedule viewer; names resolved. |

### Commerce and accounts
| Module | Today | With Staff |
| --- | --- | --- |
| Point of Sale | Records the signed-in user and copies their name; no cashier PIN | Cashiers are staff with the Cashier capability; quick cashier switch with `Staff.Badges`; register roles can be "staff with capability". |
| Orders | `StaffUserId` + `StaffName`; never set on draft orders | Set on drafts too. `StaffName` stays as the receipt's snapshot (a financial document must not change), but lists resolve the live name. |
| Inventory | `StockMovement.ActorName` copy | Actor resolved from the user; counts and transfers can be assigned to staff. |
| Accounts / Customers / B2B | No account manager or sales rep anywhere | New **Account team** part on the Account type (staff and their role on the account: owner, account manager, support), with a staff picker; the account page shows it; lead conversion carries the owner over. |
| Marketplace | `Vendor.UserNames` free-text copy | Out of scope: vendor users are the vendor's people, not ours. Use the user picker and drop the copy (audit item). |
| Reviews, TenantHierarchy, audit actors | Copied user names | Out of scope for Staff: these are audit actors, handled by the audit-stamping work. |

### Reports and AI
- **Reports**: the Staff data set replaces `WorkersReportDataSet`; headcount, hires and terminations, and turnover by team and manager; every person-based report (Contact Center agent reports, Time report, Dispatch KPIs) can group by team, manager or job title.
- **AI tools**: "who is Ana's manager", "who is on the Boston team today", "find a technician with skill X near Y", built on the directory instead of each module's own lookup.

## 6. Order of work

One branch and one PR, a commit per slice, each test-first (see *Fewer PRs: one per plan*):

1. **S1 Staff building block**: projects, record, store and indexes (`IDX_Staff_*` names, unique across the database), manager, validation handler, directory, hierarchy, admin list and driver-based editor, user editor section, permissions, recipe and deployment steps, personal data contributor, docs page, catalog entry in `building-blocks.md`, a skill under `.agents/skills/crestapps-staff`.
2. **S2 Workforce onto Staff**: id-preserving migration, `IWorkerManager` on Staff, Workforce cards as `DisplayDriver<StaffMember>`, Teams provider and `TeamMember.StaffMemberId`, Skills subject rename and merge, Geo member kind, Dispatch and WorkOrders reading the directory.
3. **S3 Directory everywhere**: picker and search endpoint, `staffOnly` on Users search, Staff party kind, Staff role grant, time-zone resolver; switch the pickers listed in section 5.
4. **S4 Compensation and Badges**: move pay rates and PIN; payroll export reads them; POS cashier switch; work order labor cost.
5. **S5 Lifecycle**: history log, observers, workflow events, Audit Trail, offboarding handlers in Contact Center, Telephony, Dispatch, Workforce and Point of Sale.
6. **S6 Contact Center and Telephony**: agent and phone cards, name copies removed from `AgentProfile`, `AgentSession` and `TelephonyExtension`, `EnsureForUserAsync` replacing `AgentWorkerService`, optional supervisor scope. Needs a live-call test before merge: it touches sign-in and supervision.
7. **S7 Scheduling, Calendars, Commerce, Accounts**: staff resources, staff attendees, account team part, POS capability.
8. **S8 Staff home, reports, AI, org chart**: My Work moves to Staff, Staff data sets, AI tools, org chart.

## 7. Tests and checks

- Unit tests for the manager's validation (unique user, unique employee number, manager cycles, team dates), the directory's name resolution, the hierarchy (cycles, depth), the time-zone chain, and the badge lockout.
- Migration tests: a seeded `WorkerProfile` set becomes staff with the same ids and the same Workforce, pay and PIN data; running twice changes nothing; punches, visits and skill assignments still resolve.
- `ModuleLayeringTests` (consumers reference `Staff.Abstractions` only), `MigrationAdditiveOnlyGuardTests`, Public API baselines (the `WorkerProfile` removal is a breaking change for packages; note it in the changelog), Contact Center architecture tests (`CallTopologyAuthorityTests`, configuration coverage).
- A full UI pass on a fresh test site (`admin` / `Password1!`): staff list and editor with Workforce, Agent, Phone and Skills cards; user editor section; pickers showing staff only; termination offboarding; then a live agent sign-in and call for S6.

## 8. Not in this plan

Positions and a job catalog (`PositionId`, headcount budgets), onboarding checklists, employee documents, emergency contacts, benefits, and a full Payroll module (gross-to-net). Each can be a feature on Staff later, through the same extension points. The Contact Center skills mirror (`AgentProfile.SkillProficiencies` and `AgentSkillsSync`) gets easier once one person is one skill subject, but removing it changes routing and is its own audit item.

## 9. Decisions needed

1. **Catalog record or content type.** Recommended: a catalog record, as `WorkerProfile` is today. It keeps the ids (no rewrite of punches, visits and timesheets), supports people without a login, keeps sensitive fields out of content versions, and gives the scheduler, dispatcher and payroll indexed rows. Teams became a content type so sites could add fields in the admin; for staff, modules add fields through drivers, and site-specific extras can live in Orchard's custom user settings.
2. **Name.** Recommended: module `CrestApps.OrchardCore.Staff`, record `StaffMember`. "Employee" would wrongly exclude contractors and seasonal crew, which Workforce supports today.
3. **Does every agent and cashier get a staff record?** Recommended: yes, created on demand with `EnsureForUserAsync`, but sign-in never fails because a staff record is missing.
4. **Staff role.** Recommended: yes, a permissionless `Staff` role granted to active linked staff through `IRoleGrantService`, so existing role-filtered pickers and stereotypes work.
5. **Compensation in Staff.** Recommended: a `Staff.Compensation` feature now, so labor costing in WorkOrders and Contact Center capacity plans can read cost rates without Workforce, and a later Payroll module builds on it.
