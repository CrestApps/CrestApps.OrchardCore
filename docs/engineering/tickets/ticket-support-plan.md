# Tickets: a help desk on the building blocks

Status: approved 2026-11-13 with the four recommended decisions (section 10). Phase 1 is in progress on branch `ma/ticket-support-module-a9418a`.

## 1. Goal

A Tickets module that covers customer support and internal service desks (IT, HR, facilities, maintenance, complaints), from one agent with one inbox to several departments with service levels, escalations, approvals and automation. It is built from the existing building blocks, and it extracts the concepts that tickets would otherwise be the second or third copy of.

## 2. What the leading products have in common

Reviewed from vendor documentation: Zendesk, Freshdesk, Jira Service Management, Zoho Desk, Intercom, Help Scout, ServiceNow, Salesforce Service Cloud and osTicket. They agree on a small model, and this plan adopts it:

| Area | Common model | Adopted here |
| --- | --- | --- |
| Status | A few fixed behaviors (new, open, pending, on hold, solved, closed) with renameable statuses on top; a status says whether it pauses service-level timers and what the customer sees | Status records with a fixed category, a customer label and a "pauses timers" flag |
| Ticket type | One "request type" is the routing hub: it selects the form, the default team, the priority and the service level | A ticket type is a content type; its settings hold the defaults |
| Fields and forms | Custom fields, several forms, fields hidden from or read-only for customers | Orchard content fields, with a per-field customer visibility setting |
| Thread | Public replies, internal notes and system events in one history; comments are immutable apart from redaction | One record timeline with visibility per entry |
| E-mail | Threading by a signed token and by `In-Reply-To`/`References`, never by subject; automated mail is quarantined with a cause | The same, on the e-mail channel |
| Finished tickets | A reply reopens a solved ticket; a reply to a closed ticket creates a linked follow-up | The same, with a reopen window per desk |
| Relationships | Merge (one way, the loser is closed), parent and child, related, problem and incidents | Typed links plus `MergedIntoId` |
| Requesters | A person with several identities who belongs to an organization; organization members can share tickets | Parties and Accounts |
| Agents | Groups or departments; what an agent may do (role) is separate from which tickets they see (all, team, assigned) | Staff, Teams, permissions with a scope |
| Assignment | Views that agents pull from, and push assignment (round robin, least loaded, skills, capacity) | Saved views, team assignment, optional Contact Center routing |
| Service levels | Ordered policies; metrics with start, pause and stop; targets per priority in business hours; a stored timer per ticket and metric; warning and breach events | A shared Service Levels block |
| Automation | Rules on create, on update and on elapsed time, with loop guards; manual multi-action macros | Orchard Workflows events and tasks |
| Reporting | Current state, a history of changes, service-level timers and a daily backlog snapshot; history is never derived from current state | Data sets for each |

Differentiators worth copying later: a reply held back when someone else answered first (collision detection), side conversations with other teams, tracker tickets, and enforced status paths per type.

## 3. Concept map: who owns what

"Step" is the reuse-ladder step from `AGENTS.md`: 1 use, 2 extend, 3 add an extension point to the owner, 4 extract a building block, 5 private to Tickets.

| Concept | Owner | Step | Notes |
| --- | --- | --- | --- |
| Custom fields, forms per type | Orchard content types and fields | 1 | See decision 1 |
| Categories and tags | Orchard Taxonomies (a taxonomy field, the tags editor) | 1 | No tag concept exists on catalog records |
| Requester | Parties (`PartyRef`, `IPartyDirectory`), `ContactInfo` snapshot | 3 | Parties has no picker, no search endpoint and no contact details; the Contact source sits in Assets |
| Organization | Accounts (`AccountPart`, `IAccountMemberService`, `IAccountContactFinder`) | 2 | A Tickets section on the account page |
| Agents | Staff (`AddStaffCapability`, `IStaffDirectory`, `StaffPicker`, `IStaffLifecycleObserver`, `IStaffHomeLinkProvider`) | 2 | Capability `SupportAgent` |
| Groups and departments | Teams (`ITeamManager`, `ITeamMembershipService`) | 3 | Teams has no picker and no assignment service |
| Skills | Skills (`ISkillAssignmentService`) | 3 | A required skill is declared twice already (Work Orders, Contact Center) |
| Links to orders, subscriptions, assets, invoices, work orders, products | none | 4 | `ReferenceType`/`ReferenceId` string pairs and per-module foreign keys today |
| Replies, internal notes, history | none | 4 | `OrderNote`, `WorkOrderNote`, the activity note, the transfer note; the audit lists it as a duplicate |
| Attachments | none | 4 | Messaging, AI Documents, Downloads, Work Orders each have their own |
| Service-level targets and timers | Work Orders (`SlaClock`, `SlaMonitor`), Messaging (first response) | 4 | Tickets would be the third copy |
| Business hours and holidays | BusinessHours (`HoursSchedule`, `IOpenHoursService`) | 3 | No module: schedules can only be edited in a Contact Center feature, and "add business time" lives in Work Orders |
| Notification e-mails | Commerce (`ICommerceEmailSender`) | 4 | Eleven other places build HTML by hand |
| Canned responses | Messaging (`MessageTemplate`) | 4 | Contracts sit in `Messaging.Core` |
| In-app notifications | Orchard Notifications | 1 | |
| Inbound and outbound e-mail | Omnichannel e-mail channel (pull request #817, open) | 1 | Not on `main` yet; see section 6.4 |
| Chat and SMS | Omnichannel Messaging (`IMessagingChannel`, `IMessagingInboundHandler`) | 2 | |
| Follow-up work for an agent (call back, task) | Omnichannel `OmnichannelActivity` | 1 | A ticket creates activities; it is not one |
| Field work | Work Orders | 2 | A ticket converts into a work order |
| Number | `INumberSequence` | 1 | |
| Automation | Orchard Workflows | 2 | Activities only; no engine changes |
| Reports and data | Orchard Data Pipelines Core, Reports (`IndexedReportDataSet`, `AddReport`) | 2 | |
| API, webhooks | Webhooks; `Commerce.Api.Core` | 3 | The idempotency filter and `ApiProblems` are commerce-only |
| Customer portal | Commerce (`CommerceConstants.Account`) | 4 | A help desk must not require Commerce |
| Audit | Orchard Audit Trail | 1 | For security-relevant events; the timeline holds the ticket history |
| Personal data | PersonalData (`IPersonalDataContributor`) | 2 | |
| Knowledge base | none | new | Its own module (section 7) |
| Satisfaction rating | none | 5 | One consumer today; Reviews is about products |
| Saved views | none | 5 | One consumer today |

**Why a ticket is not an `OmnichannelActivity`.** The catalog says not to create another task or work-item record. An activity is one attempt at one piece of work: it has a disposition, attempts and a reservation, and no thread. A ticket is the customer's request over its whole life, with a conversation, participants and service-level timers; it can produce several activities (a callback, a follow-up task). The same reasoning already separates Work Orders. The catalog entry will be reworded to say so.

## 4. Building blocks to extract first

Each block gets `X.Abstractions`, `X.Core` and, where it has pages or records, a module, with recipe and deployment steps, schemas and a catalog entry.

### 4.1 Record links (platform)

`RecordRef { Kind, Id }` in `CrestApps.OrchardCore.Abstractions` beside `Numbering` and `Tabular`, with `IRecordSource` (kind, display name, search, titles, admin and front-end URLs, "may this user see it") and `IRecordDirectory` in `CrestApps.OrchardCore.Core`, plus a `RecordPicker` view component on `ItemSelector`. Each owner registers its source: Orders, Subscriptions, Transactions (invoices), Assets, Work Orders, content items (products, accounts, contacts), and Tickets. The timeline, attachments and service-level timers below all point at their subject with a `RecordRef`.

### 4.2 Timeline: comments, notes and events on any record

`CrestApps.OrchardCore.Timeline`. One `TimelineEntry` document per entry: subject (`RecordRef`), kind (open string: `Comment`, `Note`, `Event`; consumers add their own), visibility (public or internal), author (`PartyRef`), body, field changes (`Field`, `From`, `To`), attachments, channel, `Properties`. `ITimelineService`, `ITimelineEntryHandler`, an access provider per subject kind, mentions through Orchard Notifications, a `Timeline` shape with a composer for any record page, a report data set.

Copies that move onto it in this plan: `WorkOrderNote` and `OrderNote` (data migrations). Accounts, contacts and leads get a Notes section, which they lack. Left as catalog gaps: staff history, subscription, installment and transaction event logs.

### 4.3 Attachments

`CrestApps.OrchardCore.Attachments`. `IAttachmentService` over `AddTenantFileStore` and `EncryptedFileStore`; one upload policy (size, count, type by content signature, lifted from `MessagingAttachmentUploads` and `MessagingFileFormats`); virus scanning through Orchard's file-creation events (lifted from AI Documents' `OrchardUploadedFileScanner`); downloads authorized by an access provider per owner kind, and signed links through `SignedTokenService` for e-mail and guests.

### 4.4 Business Hours module

A `CrestApps.OrchardCore.BusinessHours` module on the existing Abstractions and Core: a schedules list and editor (weekly hours, holidays, time zone) as an `IHoursScheduleSource`, a schedule picker, recipe and deployment steps, and `IOpenHoursService.AddOpenTimeAsync` / `GetOpenTimeBetweenAsync` moved out of Work Orders' `SlaClock`. Contact Center's calendars stay a source.

### 4.5 Service Levels

`CrestApps.OrchardCore.ServiceLevels`, generalized from Work Orders:

- `ServiceLevelPolicy`: metrics (open strings; `FirstResponse`, `NextResponse`, `Resolution` built in), a target per priority, a business-hours schedule or around the clock, a warning percentage.
- `ServiceLevelTimer`: one per subject and metric, with due time, elapsed business time, state (running, paused, met, breached, cancelled) and warning and breach stamps.
- `IServiceLevelTracker` (start, pause, resume, complete, restart, cancel), `IServiceLevelObserver` (warning, breached, met), and `IServiceLevelPolicyResolver` so an account, an agreement or a subscription plan can supply the policy.
- One monitor task that pages by due time.

This fixes three defects found in the Work Orders copy: a resumed pause extends the deadline by wall-clock time instead of business time; the warning point ignores paused time; the monitor loads every open work order. Work Orders moves onto the block in this plan (data migration for `WorkOrderSla`).

### 4.6 Templated e-mails

`ICommerceEmailSender` (a kind, a named model, a default body and a Liquid override per kind) becomes a neutral `ITemplatedEmailSender` in `CrestApps.OrchardCore.Core`, with Commerce as a thin layer over it so its 17 call sites do not change. Tickets registers its kinds (received, reply, solved, rating request, assignment, mention digest).

### 4.7 Reply templates

`MessageTemplate` and its manager contracts move from `Messaging.Core` to `Omnichannel.Messaging.Abstractions`, gain a scope (open string) and Liquid placeholders, and stay one record and one admin page. Tickets stores the optional field changes of a macro (status, priority, team, tags) in the template's `Properties`.

### 4.8 Smaller additions to existing owners

| Owner | Addition |
| --- | --- |
| Parties | A party picker view component and a permission-checked search endpoint; `GetContactInfoAsync` on `IPartySource`; the Contact source moves from Assets to the CRM |
| Teams | A team picker; `ITeamAssignmentService` (round robin, least loaded, skills filter) with `ITeamMemberLoadSource` and `ITeamMemberAvailabilitySource`, so Workforce shifts, calendar absences and Contact Center presence can say who is available |
| Skills | `SkillRequirement { SkillId, MinProficiency }` and a matcher, replacing the Work Orders copy |
| Core | `MapModuleApi`, `ApiProblems` and the idempotency filter out of `Commerce.Api.Core` |
| Users | The "my account" area (`/account` home, menu, page shape) out of Commerce, with the same menu name and route so no theme or module breaks |

## 5. The Tickets module

Projects: `CrestApps.OrchardCore.Tickets.Abstractions`, `.Tickets.Core`, `.Tickets`.

| Feature | Contents |
| --- | --- |
| Tickets | Ticket part and default type, statuses, priorities, desks, list, saved views, ticket page, replies and notes, attachments, assignment, service levels, links, merge, permissions, notifications, recipe and deployment |
| Tickets Portal | Customer pages: submit, my tickets, my organization's tickets, reply, attach, rate |
| Tickets E-mail | Mailboxes, e-mail to ticket, replies by e-mail, quarantine |
| Tickets Messaging | Turn a chat or SMS conversation into a ticket and keep replying on that channel |
| Tickets Satisfaction | Rating request after solve, rating and comment |
| Tickets Approvals | Approval steps and a service catalog (phase 4) |

Optional integrations are `[RequireFeatures]` startups: Workflows, Data Pipelines Core, Reports, Webhooks, Audit Trail, Notifications, Accounts, Skills, Work Orders, Omnichannel, TimeTracker, PersonalData, AI and the Recipes schemas. Contact Center routing is a bridge module, `Tickets.ContactCenter`.

### 5.1 Records

**Ticket.** A content item of any content type that carries `TicketPart` (stereotype `Ticket`); each such type is a ticket type with its own fields and form. Types are not versionable or draftable, so a save updates in place. `TicketPart` holds: number, desk, status and its category, priority, requester (`PartyRef`) with a `ContactInfo` snapshot, account, team, assignee (staff member), channel (open string), participants (CC and followers), links (`RecordRef` and kind), parent, merged-into and follow-up-of ids, the time stamps and counters reports need (first response, solved, closed, last customer and agent activity, replies, reopens, reassignments, escalation level). A `TicketIndex` serves the list, the views and the reports. Every change goes through `ITicketService` under a per-ticket lock.

`TicketPartSettings` on the type: default desk, team, priority and service-level policy, required skills, who may submit it from the portal, and its portal description. `TicketFieldSettings` on a field: hidden from, read-only for or editable by customers.

**Settings records** (catalog records, each with a list on `<admin-list>`, `CatalogAdminController<T>`, recipe and deployment steps):

| Record | Fields |
| --- | --- |
| `TicketStatus` | Name, category, customer label, pauses timers, color, order |
| `TicketPriority` | Name, rank, color (seeded: low, normal, high, urgent) |
| `TicketDesk` | Name, audience (customers or internal), default team, hours schedule, service-level policy, ticket types offered, reopen window, auto-close delay, notification switches |
| `TicketView` | Name, filter, sort, owner, shared with (everyone, a team) |

A desk is what lets IT, HR and customer support share one site: each has its own types, team, hours, policy, mailboxes and permissions.

### 5.2 Conversation

Replies, internal notes and events are timeline entries on the ticket (kinds `Reply`, `Note`, `Event`). A channel is an adapter: it turns an inbound message into a reply entry and delivers a public reply. Delivery data (message id, recipients, delivery status) lives in the entry's `Properties`.

### 5.3 Life cycle

Category moves are a `TransitionTable`. Built in, per desk: a customer reply reopens a solved ticket inside the reopen window; a reply to a closed ticket creates a follow-up ticket linked to it; solved tickets close after the auto-close delay; closed and merged tickets are read-only. Merge moves the loser's requester and participants to the winner as participants, adds a linking note to both, and closes the loser.

### 5.4 Assignment and access

- Pull: saved views (my open tickets, unassigned, my teams, breaching soon, recently solved).
- Push: the ticket's team assigns through `ITeamAssignmentService`, with the type's required skills.
- With `Tickets.ContactCenter`: a ticket is offered through a Contact Center queue as an activity, so presence, capacity and skills routing apply; accepting it assigns the ticket.
- Permissions: manage settings, view, edit, reply, add internal notes only, assign, merge, delete, each generated per desk (so HR tickets stay private), with a scope of all tickets, my teams' tickets or my assigned tickets. Lists, the API and the report data sets apply the same scope.
- A staff member who leaves has their open tickets unassigned (`IStaffLifecycleObserver`).

### 5.5 Service levels

A new ticket starts its timers from the policy resolved for the account, then the type, then the desk. The first public agent reply completes `FirstResponse`; each customer reply starts a `NextResponse` cycle; solving completes `Resolution`; a status that pauses timers pauses them. The ticket page and list show the next due time.

### 5.6 Automation (Orchard Workflows)

| Kind | Activities |
| --- | --- |
| Events | Ticket created, updated (with a changed-field filter), status changed, assigned, customer replied, agent replied, note added, service-level warning, service-level breached, solved, reopened, closed, merged, rating received, time elapsed (a duration in clock or business hours since created, last customer reply, last agent reply or last status change, for chosen status categories), action requested (a named manual action shown on the ticket) |
| Tasks | Create ticket, update ticket (status, priority, type, team, tags, a field), assign (team, agent or strategy), add reply, add note, send notification (requester, assignee, team, followers; a template), escalate (level, team, priority), get ticket, find tickets, check service level (on track, warning, breached, paused, none), link tickets, create work order |

A "check business hours" task (open, closed) goes to the Business Hours module. Changes made by a workflow are marked, and a chain of workflow-caused updates stops at a fixed depth, so rules cannot loop. A "help desk starter" recipe ships statuses, priorities, a desk, a schedule, a policy and example workflows.

### 5.7 Agent pages

The Tickets menu opens the list on `<admin-list>` (row driver with `SummaryAdmin` and `SummaryAdminGrid`, search, pickers for status, priority, team, assignee, desk, type and service-level state, bulk assign, status, merge and delete). The ticket page shows the thread and composer (reply or note, reply templates, attachments, "send and set status") beside panels rendered by display drivers: properties and custom fields, requester and account with previous tickets, service levels, links, participants. Other modules add panels with their own drivers (orders, subscriptions, assets).

### 5.8 Data, reports, API

- **Data source `Tickets`** (registered for Data Pipelines Core, so pipelines and the Report Builder both get it): `Tickets`, `TicketEntries` (replies and notes), `TicketChanges`, `TicketLinks`, `TicketRatings`, `TicketBacklog` (a daily snapshot). Fields reference Users, Staff, Teams and the ticket's content type data set in `Contents` for custom fields. `ServiceLevelTimers` and `TimelineEntries` come from their own blocks. Row access follows the permission scope.
- **Reports** (under Reports): volume and backlog, age, first response and resolution times, service-level compliance, agent and team performance, reopen and escalation rates, first-contact resolution, satisfaction, distribution by channel, type and category.
- **API** `api/tickets/v1` (list, get, create, update, reply, note, attachments) with the Api scheme, a permission check per endpoint and idempotency keys.
- **Webhook events**: created, updated, assigned, reply added, solved, closed.
- One `ITicketObserver` feeds workflows, webhooks, notifications and the audit trail through thin adapters.

## 6. Channels and self-service

### 6.1 Portal

On the "my account" area: the service catalog (ticket types the visitor may submit, by desk), the form (the type's customer-visible fields), my tickets, my organization's tickets for account administrators, the thread with reply and attachments, the rating. A requester who has no login follows a signed link from the e-mail.

### 6.2 Web form and API

A widget that renders a ticket type's form on any page, with reCAPTCHA and rate limiting. The API covers integrations and live-chat hand-offs.

### 6.3 Agents

"New ticket" on behalf of a requester, from the list, an account, a contact or a messaging conversation.

### 6.4 E-mail

Built on the e-mail messaging channel of pull request #817 (`IEmailInboundReceiver`, webhook parsers, IMAP, `IEmailTransport`, the sending governor). A mailbox maps to a desk and a type. Threading: the signed token in the reply address or body, then `In-Reply-To`/`References` against stored message ids, otherwise a new ticket. Automated, looping and blocked mail goes to a quarantine list with its cause. Requesters are resolved by e-mail to a user, a contact or a customer, otherwise kept as a guest snapshot. This slice starts when #817 is merged; nothing else waits for it.

### 6.5 Satisfaction

A rating request after solve (delay per desk), one rating and comment per ticket, a workflow event and a data set.

## 7. Knowledge Base module

`CrestApps.OrchardCore.KnowledgeBase`: a `KnowledgeArticle` content type (body, category taxonomy, audience: public, signed-in or staff, with role rules through Content Access Control), help-center pages (categories, article, search on Orchard Search), "was this helpful" feedback, drafts, versions and translations from Orchard. Articles are suggested while a customer types a request and in the agent's composer, link to tickets through `RecordRef`, and are indexed for AI answers through AI Data Sources. It has no dependency on Tickets.

## 8. Order of work

One pull request per phase, a commit per slice.

| Phase | Contents |
| --- | --- |
| 1. Foundations | Section 4: record links, Parties and Teams additions, Timeline, Attachments, Business Hours module, Service Levels with Work Orders moved onto it, templated e-mails, reply templates, skill requirement, API helpers. Order and work-order notes move to the timeline |
| 2. Tickets | Section 5: records, agent pages, assignment, service levels, life cycle, links and merge, permissions, notifications, workflows, data source and reports, API and webhooks, recipes and deployment, Accounts section, Staff integration, personal data, AI tools |
| 3. Self-service and channels | The account area out of Commerce, portal, web form, e-mail (after #817), messaging conversion, satisfaction, Knowledge Base |
| 4. Advanced | Approvals and service catalog, problem and incident links, Contact Center routing bridge, live updates and collision detection, AI triage and suggested replies, account e-mail domains and service-level entitlements, time on tickets (TimeTracker), work-order conversion |

## 9. Tests and checks

Each slice starts with failing tests. Each phase ends with a Release build with warnings as errors, the unit and architecture tests (layering, recipe schema coverage, workflow activity views, deployment step serialization, additive-only migrations, feature id literals, unbounded loads), the JavaScript lint tests, a browser pass of every new page in List and Grid layouts and in Buttons and Menu action modes, and the docs build. Service-level arithmetic gets property tests across daylight-saving changes and holidays. Docs: a technical and a user-manual page per module, the feature reference, the data sources pages, the AI tools page, the changelog, and the building-blocks catalog.

## 10. Decisions

Decided 2026-11-13: the recommended option in each of the four.

1. **How a ticket is stored.** Recommended: a content item with `TicketPart`, so custom fields, forms per type, taxonomies and the `Contents` data source come from Orchard. Alternative: a catalog record like `WorkOrder`, which needs its own custom-field engine (Assets already has a small private one).
2. **Where the conversation lives.** Recommended: the Timeline block, with channels as adapters, so a help desk works without Messaging and Contact Center. Alternative: make a ticket a `MessagingConversation`, which needs thread-aware conversation keys and an internal flag on `OmnichannelMessage`, and ties tickets to the workspace.
3. **How many existing copies move in phase 1.** Recommended: Work Orders (service levels, notes) and Orders (notes), which have no live traffic. Messaging attachments, the Messaging first-response timer and Contact Center routing carry live calls and messages; they are recorded as gaps in the catalog and move in a follow-up with live testing. Alternatives: move everything now, or move nothing and only add the blocks.
4. **Pull requests.** Recommended: four, one per phase. Alternative: one.

Taken as recommended unless you say otherwise: push assignment through a new Teams assignment service with the Contact Center bridge as an option; the "my account" area moves from Commerce to Users with unchanged names; the e-mail slice waits for #817; satisfaction ratings and saved views stay private to Tickets until a second module needs them.

## 11. Progress

Keep this section current: a row per slice, with what was built, what differs from the plan and what is left.

### Phase 1 (done)

End checks on 2026-11-13: Release build of the solution with warnings as errors clean; the full unit test project (15,795 tests; the five failures it found were fixed in their own commit and rerun); `npx vitest run` 1,475 tests; `npm run check:manuals`; the docs build (links and anchors checked); public API baselines approved. Not done in the browser: a pass as a non-administrator, because test sites here keep a single `admin` account; access rules are covered by unit tests.

| Slice | State | Notes |
| --- | --- | --- |
| Record links (4.1) | Built, unit tested | `Records/` in `CrestApps.OrchardCore.Abstractions` (`RecordRef` stored as `Kind:Id`, `IRecordSource`, `IRecordDirectory`); `RecordPicker` view component and `Admin/api/crestapps/records/search` in Resources. Sources registered for orders, subscriptions, transactions, assets, work orders and content items (`Content`, in Resources behind the Contents feature, items filtered by View permission). Left: browser check of the picker (with the ticket links in phase 2) |
| Parties (4.8) | Built, unit tested | `PartySourceOptions` describes each kind (name, search permission) through `AddPartySource<T>(kind, displayName, permission)`; `GetContactInfoAsync`; `ContactPartySource` moved from Assets to `Omnichannel.Core` and registered by the CRM; `PartyPicker` view component and `Admin/api/crestapps/parties/search` in Resources. The search endpoint's permission gate is unit tested (only kinds the user may search, none refused, a kind without a permission or source never searched). Left: browser check of the picker (with the ticket pages in phase 2) |
| Teams (4.8) | Built, unit tested | `ITeamAssignmentService` (round robin, least loaded; stateless, the turn comes from `ITeamMemberLoadSource`), `ITeamMemberAvailabilitySource`, `TeamPicker` view component. Moved to phase 2 (the Tickets assignment slice, where they are first used and can be checked end to end): availability sources from Workforce shifts, calendar absences (Calendars has no out-of-office marker yet, so an event type will declare that it marks an absence) and Contact Center presence, and the browser check of the picker |
| Skills (4.8) | Built, unit tested | `SkillRequirement` and `HoldsAllAsync` in Skills.Abstractions; Work Orders and Dispatch use it instead of `RequiredSkill`. `HoldsAllAsync` is unit tested; a requirement with a blank skill id is now ignored like one without |
| Business hours arithmetic (4.4) | Built, unit tested | `HoursScheduleEvaluator.AddOpenTime` / `GetOpenTime`, `IOpenHoursService.AddOpenTimeAsync` / `GetOpenTimeBetweenAsync` |
| Business Hours module (4.4) | Done, unit tested, browser checked | `CrestApps.OrchardCore.BusinessHours`: `BusinessHoursSchedule` (named catalog), **Configuration > Business Hours** list and editor (weekly rows with up to two windows a day, special-days text), `HoursSchedulePicker` view component (lists every source), `BusinessHoursSchedules` recipe and deployment steps with schemas, **Check Business Hours** workflow task (Open, Closed, Unknown). Checked in the browser: create, validate, edit round-trip, copy, both list layouts and both action modes, JSON import. Choice: Contact Center calendars keep their own editor and stay a source (gap in the catalog) |
| Service Levels (4.5) | Done, unit and SQLite tested, browser checked | **Configuration > Service Levels** list and driver-based editor (scope fixed at creation through `?scope=`, `ServiceLevelTargetGrid` of minutes per metric and priority plus "any other priority"), `ServiceLevelPolicies` recipe and deployment steps with schemas, `IServiceLevelPolicyUsageProvider` (a policy in use can't be deleted), `ServiceLevelStart.AlreadyWarned/AlreadyBreached`, policy lookup by scope and name, SQLite store test. The feature depends on Business Hours and Resources. Left for phase 2: the `ServiceLevelTimers` data set (built with the Tickets data sets so both use the same report API) |
| Work Orders on Service Levels (4.5) | Done, unit and SQLite tested, browser checked | `SlaClock`, `SlaMonitor`, the Work Orders `ServiceLevelPolicy`, `IServiceHoursProvider` and their pages are gone. Work Orders registers the `WorkOrders` scope, a **Work orders** policy card (pause statuses in `Properties`), an observer that raises the existing warning and breach events, and a usage provider; `WorkOrderService` drives the tracker through `WorkOrderServiceLevels` and keeps `WorkOrder.Sla` as the read model. Migration v3 (`WorkOrderServiceLevelUpgrade`) copies policies with the same id (weekly hours become a schedule) and gives open work orders timers without raising again what was raised; the `ServiceLevels` section of old `WorkOrders` recipe steps is still read. Checked in the browser: type with a shared policy, new work order due times on a schedule with a lunch break, on hold and back (business-time shift), response met, refused delete. Note: `WorkOrderService` has no per-work-order lock today; timers are changed in the same session as the work order |
| Timeline (4.2) | Done, unit and SQLite tested, browser checked | `CrestApps.OrchardCore.Timeline`: `TimelineEntry`, `ITimelineService`, `ITimelineEntryHandler`, `ITimelineAccessProvider` per record kind (a kind without one is refused), the `Timeline` view component (card with note/comment composer, internal badge, field changes, redaction), mentions through Orchard notifications (only to users who may read the entry), `TimelineEntries` recipe and deployment steps. Work order notes and order notes moved onto it (WO migration v4, Orders migration v4; `WorkOrder.Notes`/`Order.Notes` obsolete, kept for the upgrade; `IOrderService.AddNoteAsync` keeps its signature). Accounts got a Notes tab, contacts and leads a Notes card (kind `Content`, access from the item's View/Edit permissions). Choices: the body is plain text (no Markdown rendering); callers that only have a name keep it in `AuthorName`, as work orders and orders pass names today. Checked in the browser: migrated work order notes, a note on a work order, an order (add, redact), an account's Notes tab (comes back to the tab) and a contact, and a `TimelineEntries` JSON import. Fixed on the way: an entry added from the card keeps the user's name, for sites where no party source knows users. Left: the `TimelineEntries` report data set (with the phase 2 data sets), a browser check of the deployment export |
| Attachments (4.3) | Done, unit and SQLite tested, browser checked | `CrestApps.OrchardCore.Attachments`: `IAttachmentService` with one upload policy (`AttachmentOptions` from `CrestApps:Attachments`: size, count, formats), `FileFormats` matched by bytes (moved from Messaging, which now uses them), `IAttachmentScanner` with Orchard's file events as the default scanner, an encrypted tenant file store (`AddTenantFileStore`, Azure Blob through `CrestApps:Attachments:AzureBlob`), `IAttachmentAccessProvider` per owner kind, `/attachments/{id}` (signed-in users the owner allows, or a `SignedTokenService` link; only pictures inline; nosniff, no-store, sandbox CSP), ConfigurationSchema.json. The timeline card takes files when the feature is on; they belong to the entry (owner kind `TimelineEntry`), so they are as visible as it, and redacting the entry deletes them. Checked in the browser: card without the feature, upload of a picture and a PDF, inline and forced download, encrypted hashed files on disk, an SVG named `.png` refused (note not added), files-only comment, redaction removes files, anonymous download refused. Fixed on the way: a content item's internal timeline entries were readable by anyone with View Content (Anonymous by default); internal now needs Edit Content and public a signed-in viewer. Choices: no recipe or deployment step (binary files encrypted with the tenant's keys); Messaging's attachment store stays (gap in the catalog). Left: the typed text is lost when an upload is refused |
| Templated e-mails (4.6) | Done, unit tested | `CrestApps.OrchardCore.Emails` in Abstractions and Core: `ITemplatedEmailSender`, `ITemplatedEmailTemplates` (`LiquidTemplatedEmailTemplates`), `ITemplatedEmailTemplateSource` per module, kinds grouped by module (`AddTemplatedEmailKind(group, ...)`), `TemplatedEmailCulture` and `LiquidModelValues`. Commerce is a thin layer: `CommerceEmailSender` sends through `TemplatedEmailSender`, `LiquidCommerceEmailTemplates` renders through `LiquidTemplatedEmailTemplates` with `CommerceEmailTemplateSource`; the 17 call sites and the store's settings are unchanged and the commerce e-mail tests pass as they were. Choice: a template source per module instead of one settings record, so Tickets keeps its templates in its own settings and the store's layout never wraps a ticket e-mail. Not browser checked: no page of its own; Tickets registers its kinds and settings page in phase 2 |
| Reply templates (4.7) | Done, unit and SQLite tested, browser checked | `MessageTemplate.Scope` (index column, migration v7, `MessageTemplateScopeOptions` through `AddMessageTemplateScope`), a **Used for** choice in the editor once a scope is registered, `IMessageTemplateRenderer` (Liquid, through the shared `LiquidModelValues`; a plain body never changes), and the workspace offers only templates without a scope, with `{{ Contact.DisplayName }}` filled in. Deviation: `MessageTemplate` and its managers stay in `Messaging.Core` instead of moving to `Messaging.Abstractions`, because moving the type would rewrite the stored `Document.Type` of live messaging data; Tickets reaches them through `Messaging.Core`. Checked in the browser: the editor with the Liquid hint, saving a template with a placeholder. Left for phase 2: Tickets registers its scope and stores a macro's field changes in `Properties`; the workspace rendering is unit tested only (no live conversation on the test site) |
| API helpers and the account area (4.8) | API helpers done, unit tested, checked live; the account area moves in phase 3 | `CrestApps.OrchardCore.Api` in Core: `MapModuleApi` (group, `Api` scheme, no antiforgery), `ApiProblems` (shared codes), `IdempotencyEndpointFilter`, `IdempotencyPolicy`, `IdempotentOperation`, `WithIdempotency`, and `IdempotencyGuestHeaders` group metadata naming the headers a guest is known by. Commerce keeps `MapCommerceApi` (on `MapModuleApi`, with `CommerceApi.GuestHeaders`) and `CommerceProblemCodes` for its own conflict codes; the cache key prefixes are unchanged so keys kept before an upgrade still replay. Checked live: a guest starting a checkout twice with one key gets the same `201`, `Location` and checkout token back. The "my account" area moves from Commerce to Users in phase 3, as decided |
| Catalog, docs, changelog | Done | The catalog has rows for record links, parties and the party picker, teams and team assignment, Service Levels, Business Hours, Timeline, Attachments, templated e-mails and the module API helpers; each module has a technical and a user-manual page (templated e-mails and the API helpers have no pages of their own to show) and a changelog entry |

Open items left from phase 1: files attached to a timeline entry are removed when the action fails, but not when the request's session commit fails after the action returned (an orphan sweep would catch them); a content item's timeline is deleted in a deferred task, so a failure there is logged and leaves the entries and files behind; a business-hours schedule that never opens, set after a timer started, is not handled.

### Phase 2 (in progress)

| Slice | State | Notes |
| --- | --- | --- |
| Records, service, access, settings pages (5.1, 5.3, 5.5) | Done, unit tested, browser checked | Projects `Tickets.Abstractions`, `.Core` and the `CrestApps.OrchardCore.Tickets` module. `TicketPart` (stereotype `Ticket`, default `Ticket` type, not versionable), `TicketIndex`, `ITicketService` (create, update, reply and note, auto-assign through `ITeamAssignmentService`, merge, link, scoped list) under a per-ticket lock, `TicketLifecycle` (category moves, reopen window, follow-up after close, read-only closed and merged tickets), `ITicketAccessService` (scope all, my teams, assigned), service-level timers (`Tickets` scope; policy from account, type, then desk; a reopened ticket keeps counting its one resolution instead of starting a new cycle). Settings records `TicketStatus`, `TicketPriority`, `TicketDesk`, `TicketView` with lists on `<admin-list>` (seeded statuses, priorities, a Support desk and four shared views), editors, one catalog handler (name required and unique), recipe and deployment steps with schemas. Checked in the browser: the four lists in List and Grid layouts, an edit form, a desk refused without a name and with a taken name, created and read back. Left for later slices: Buttons action mode pass (done with the ticket list) |
| Ticket list and ticket page (5.7, 5.3 merge, 4.1 links) | Done, unit tested, browser checked | **Tickets** menu (shown to whoever may view a desk) opens the list on `<admin-list>`: saved views as pills, search by number or subject, pickers for state, status, priority, assignee, team, desk, type, service level (past a target, due within 4 hours) and sort in the toolbar, rows from `TicketRowDisplayDriver` in both layouts, bulk assign to me, set status, merge into the oldest and delete (each ticket checked on its own). **New ticket** on behalf of a requester (party picker, or a guest's name and e-mail) with the ticket type's own editor (its title is the subject; fields from the type), desk, priority, first message and files. The ticket page: thread from the Timeline card, a composer for a reply or an internal note with files and "send and set status", and the `DetailAdmin` display of the ticket with panels from `TicketPartDisplayDriver` (values with team and user pickers and "assign to me", requester with their other tickets, service levels, links through the record picker) beside the type's fields; **Edit fields**, **Merge** (by number) and **Delete**. `TicketTimelineAccess` (read by view scope, nothing added through the generic composer), `TicketRecordSource` (tickets are linkable records), `TicketContentCleanup` (a deleted ticket loses its timeline and timers), `ITicketService.EditAsync` and `DeleteAsync`, `EntryId` on replies so files belong to the reply entry. Checked in the browser: create with a guest requester, reply with a status change, internal note, assign to me, priority change, link to another ticket, merge (read-only merged ticket with banner), edit fields (subject change event), bulk set status (merged ticket refused with a message), delete (its timeline gone), List and Grid, Buttons and Menu. Choice: the default ticket type's title is shown as **Subject** and required (new sites; this test site keeps "Title") |
| Assignment and life cycle jobs (5.3, 5.4, Teams 4.8 moved from phase 1) | Done, unit and SQLite tested | A desk says how new tickets with a team are assigned (**New tickets go to**: nobody, the member with the fewest open tickets, or each member in turn; `TicketDesk.AssignmentStrategy`, validated, in the recipe schema); `TicketTeamMemberLoadSource` reports each member's open tickets and latest assignment (`TicketPart.AssignedUtc`, index column and index, Tickets migration v2). Availability: `WorkforceShiftAvailabilitySource` (off a published shift or on a break inside one), `CalendarAbsenceAvailabilitySource` (new `MarksAbsence()` on event types; Out of office and Time off marked) and `AgentPresenceAvailabilitySource` (Contact Center presence). **Close solved tickets** background task (every 15 minutes, `TicketAutoCloser`, at most 100 per desk a run) closes solved tickets after the desk's delay (`TicketQuery.SolvedBeforeUtc`). Not browser checked here beyond the desk editor: the job and the sources have no page of their own |
| E-mails, assignee notices and reply templates (5.2, 5.6 notifications, 4.6 and 4.7 left from phase 1) | Done, unit tested, browser checked | `ITicketObserver` gains `AssignedAsync` and `StatusChangedAsync`, raised by every path that assigns or moves a ticket (a snapshot taken under the ticket's lock). `TicketEmailObserver` e-mails the requester when a ticket is received, when an agent replies in public and when it is solved, on a desk that e-mails requesters, after the request commits; kinds `ticket-received`, `ticket-reply`, `ticket-solved` in the Tickets group; `TicketEmailTemplateSource` over `TicketEmailSettings` (only ticket kinds). **Settings > Tickets > E-mails** through the new shared `TemplatedEmailTemplatesEditor` (Core) and `TemplatedEmailTemplatesEditor` shape (Resources). `TicketAssignmentNotifier` tells the assignee through Orchard notifications (with Notifications on, on a desk that tells assignees, never about a ticket they took). Reply templates: with Messaging on, Tickets registers its scope, the template editor gets **On a ticket, also** (status, priority, team, kept in the template's properties), and the composer offers the Tickets templates filled in for the ticket (`ITicketReplyTemplateSource`, `MessagingTicketReplyTemplateSource`); sending applies the template's changes as far as the user may. Checked in the browser: the e-mails settings page (save, Liquid with a filter that isn't available refused), a Tickets template with a macro, inserting it on a ticket (placeholders filled, status preselected) and sending (status and priority changed). Not checked: delivery, because the test site has no e-mail service |
| Workflows, webhooks and audit trail (5.6, 5.8 webhooks) | Done, unit tested, browser checked | With Workflows on: 15 events (created, updated with a changed-field filter, status changed with a category filter, assigned, customer replied, agent replied, note added, service-level warning and breached, solved, reopened, closed, merged, action requested, time elapsed in clock or business hours since created, the last customer or agent reply or the last status change) and 11 tasks (create, update, assign with team assignment, add reply, add note, escalate, get, find, check service level, link, notify the requester or the assignee), each with its views and schema; every event narrows to desks. `TicketWorkflowObserver` starts them from the ticket observer events, `TicketServiceLevelObserver` brings the service-level warning and breach; a chain of workflow-caused changes stops starting workflows at depth 3 (`TicketWorkflowDepth`; a task's actor is marked `FromWorkflow`). Named actions: each published workflow that starts with Ticket Action Requested is offered under **Actions** on the ticket page (`ITicketActionSource`). **Ticket time elapsed** background task (every 15 minutes) starts each time-elapsed workflow once per ticket and moment. Webhooks (with Webhooks on): `ticket.created`, `ticket.updated`, `ticket.assigned`, `ticket.reply_added` (public replies only), `ticket.solved`, `ticket.closed`, the payload being `TicketApiModel`. Audit trail (with Audit Trail on): created, changed, assigned, status changed, merged, deleted. Fixed on the way: the ticket page's `tickets/{id}` route answered `tickets/priorities` with a 404; ticket pages now live under `Admin/ticket/{id}` (route test added). Checked in the browser: a workflow imported as JSON (Ticket Action Requested, then Escalate Ticket), offered under Actions and run from a ticket (priority and escalation level changed). Not checked: the workflow editor itself, which fails on this site for every workflow because of an unrelated Omnichannel driver that needs AI (follow-up) |
| Data sets and Staff (5.8, 5.5 Staff, 4.2 and 4.5 data sets left from phase 1) | Done, unit and SQLite tested, browser checked (Report Builder lists the sources and previews Tickets, TicketEntries and TicketChanges rows) | With Data Pipelines Core on, a **Tickets** data source with five data sets: `Tickets` (current state with desk, status and priority names, requester, times, counters, minutes to first response and to solve; the ticket id references the Ticket type in `Contents` for custom fields, the assignee references Users), `TicketEntries` (replies and notes), `TicketChanges` (one row per recorded field change, the history), `TicketLinks` and `TicketBacklog`. Every set applies the reader's ticket scope: `TicketScopeFilter` (Tickets.Core) is now the one rule the list, the data sets and the timer access use; rows from the timeline are checked a batch at a time against the ticket index. `TicketBacklog` reads `TicketBacklogDay` snapshots that the **Record the ticket backlog** task (hourly, one per desk and site day, `TicketBacklogRecorder`, Tickets migration v3 with `TicketBacklogIndex`) takes of the open tickets by status, priority, team, assignee, type and channel, with overdue counts and ages. The building blocks got their own data sets: `TimelineEntries` (Timeline module, rows filtered by each record kind's timeline access) and `ServiceLevelTimers` (ServiceLevels module) with a new extension point, `IServiceLevelTimerAccessProvider` (`AddServiceLevelTimerAccessProvider`): policy managers see every timer, others the timers of records a provider lets them see; Tickets registers one. Staff: a staff member who leaves or whose user is unlinked hands back their open tickets (unassigned, with the change on each ticket's timeline). Choice: backlog snapshots have no recipe or deployment step; they are history the site records, like service-level timers, not records anyone edits. `TicketRatings` comes with satisfaction in phase 3 |
| Reports (5.8) | Done, unit and SQLite tested, browser checked | With Reports on, five coded reports behind a new **View ticket reports** permission, each counting only the reader's ticket scope: **Ticket Volume and Backlog** (created and solved by day in the reader's time zone, open, overdue and unassigned now, age of open tickets, backlog at the start of each day from the snapshots), **Ticket Response and Resolution Times** (share responded, medians and averages by priority, solved on first contact), **Ticket Service Level Compliance** (targets ended in the period met or breached by metric, median business time used, running targets already breached or warned), **Ticket Agent and Team Performance** (solved, time to solve and first response, reopen and escalation rates, open now, by assignee and by team) and **Ticket Distribution** (by channel, desk, ticket type, priority and where they stand now). Merged tickets are left out. Choices: "solved on first contact" is solved after a single reply and never reopened; response and resolution times are calendar time, business time is in the service-level report; the reports have only the date range filter. Satisfaction comes with ratings in phase 3 |
| API (5.8) | Done, unit tested; checked live without a token only | A **Tickets API** feature maps `api/tickets/v1` through `MapModuleApi` (Api scheme, no antiforgery): `GET tickets` (search, desk, status categories, assignee, requester, account, paged to 100, the caller's scope), `GET tickets/{id or number}`, `GET tickets/{id}/entries` (replies, notes and events with files and signed download paths for an hour), `POST tickets` (a desk the caller may reply in, a type with the ticket part, requester as `Kind:Id` or name and e-mail, the Api channel), `PATCH tickets/{id}` (subject, status, priority, desk and escalation need edit, team and assignee need assign, a desk move needs a view scope on the new desk), `POST tickets/{id}/replies` and `/notes` (reply or note permission; a note never sets a status). Files travel in the JSON as base64 and are stored as files of the entry being added, so there are no orphan uploads; a refused file or a failed write removes what was stored. Writes take an `Idempotency-Key`. A ticket out of scope is answered 404, not 403. Models in Tickets.Core (`TicketApiMessages`). Not done: an authenticated live call; the test site has no OpenId application, and creating one (an access key) waits for Mike's OK. Live: every route answers 401 without a token |
| Accounts, personal data, AI tools, starter recipe (5.5, 5.8 and section 5 integrations) | Done, unit and SQLite tested, browser checked (account tab, recipe) | **Accounts:** with Accounts on, a **Tickets** tab on the account page (new `AccountPageTabs.Tickets`, position 55, the section registered by Tickets): the account's latest 25 tickets in the user's scope with status, priority and local time, the open count and **New ticket** for the account. **Personal data:** `TicketPersonalDataContributor` finds the tickets a person asked for by their user (`Requester`) and by the e-mail on a guest's ticket (new `TicketIndex.RequesterEmail`, lower case, Tickets migration v4); export lists each ticket and its public replies; erasure removes the contact snapshot, rewrites the subject and rewrites the timeline through a new `ITimelineService.ScrubAsync(subject, scrub)` (body, author name and recorded change values; the Timeline owner keeps entries otherwise unchanged). **AI tools** (with AI on, selectable on profiles, category Tickets): `listTicketOptions`, `searchTickets`, `getTicket`, `createTicket`, `addTicketReply` (reply or internal note) and `updateTicket`; each acts as the signed-in user within their scope and permissions and answers with the API models. **Help desk starter** recipe (`help-desk-starter.recipe.json`): a weekday schedule, a Tickets service-level policy, the Support desk set up and an IT desk, and two example workflows (escalate on breach, note on reopen) that start turned off. The desk step now matches by `ItemId` and then by name, so the recipe updates the Support desk the feature seeds. Checked live: imported twice, no duplicates. Not checked live: personal data (no admin page on the test site for it; SQLite tested) and the AI tools (no AI connection on the test site; unit tested) |
| Docs (phase 2) | Done, manuals check passed | Technical page `modules/tickets.md` and user manual `user-manual/administration/tickets.md` (in the sidebars), module README, feature reference (Tickets, Tickets API), Report Builder data sources (Tickets, Timeline, Service levels), AI tools catalog (ticket tools), changelog 3.0.0, building-blocks catalog (a Tickets row; Service Levels timer access; Timeline `ScrubAsync` and data set), and data set notes on the Timeline and Service Levels pages |

## 12. Not in this plan

Change management and a configuration database, enforced status paths per type, side conversations, social channels, voice tickets beyond the Contact Center bridge, NPS and effort surveys, operational-level agreements between teams, and moving the remaining timeline and attachment copies named in section 4.
