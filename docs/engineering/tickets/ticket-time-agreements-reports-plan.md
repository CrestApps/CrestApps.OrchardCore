# Ticket time, support agreements and business reports

Status: slices 1 and 2 built (2026-11-15). Mike took every recommendation in section 9 (2026-11-15); slices 3 to 7 are being built.

Builds on the Tickets module ([`ticket-support-plan.md`](ticket-support-plan.md), branch `ma/tickets-phase-2`) on Orchard Core 4 preview-19225 and .NET 11. Ticket boards, the intake plan (categories and conditional questions, [`ticket-intake-plan.md`](ticket-intake-plan.md) on `ma/pensive-jackson-c56dcb`) and business hours are planned in other sessions and are only referenced here.

## 1. Goal

A service desk reports on business outcomes, not only on ticket counts:

- how long each person worked on a ticket, and on all tickets in a period;
- for a solved ticket, how long it took (resolution time) against how long people actually worked on it (work time and effort);
- how much time each account got;
- how each account is tracking against its support agreement: hours used, hours left, service level met.

The reports sit under **Reports** when the Reports feature is on, like the five ticket reports already there.

## 2. Concept map

| Concept | Owner | How Tickets uses it |
| --- | --- | --- |
| Time spent by a person | **Time Tracker** (`ITimeTrackerService`, `TimeEntry`, stages and categories). The catalog says "never keep your own time ledger" | Ticket time is Time Tracker entries with source `Ticket` and source ref `Ticket:{id}` (already so for the start/stop timer). Manual entries are Time Tracker entries too |
| Billable or not, payable or not | **Time Tracker** categories on stages (`TimeStage.CategoryIds`) | A desk names the stage ticket work is recorded in, and (new) a second stage for work that isn't billed |
| Organizations | **Accounts** | `TicketPart.AccountId`, `TicketIndex.AccountId` |
| Service levels | **ServiceLevels** (policies, timers per record) | Policy per desk, type and account (`TicketAccountPart.ServiceLevelPolicyId`) |
| Business hours | **Business Hours** (`IOpenHoursService`, `HoursSchedule`) | Covered hours of an agreement, through ServiceLevels policies or directly |
| Agreements with an account | **Work Orders** `ServiceAgreement` today (field-service maintenance contract: schedules, assets, price per visit) | Not used yet. Section 6 decides the owner |
| Selling a recurring plan | **Subscriptions** (`SubscriptionEntitlementPart` grants roles while current) | Option for selling support plans (section 6.4) |
| Reports | **Reports** (`IReport`, `ReportDocument`, exports, subscriptions) | New reports in the Tickets module, in a startup that needs Reports and Time Tracker |
| Labor and expenses priced on a job | **Work Orders** line items (part, service, labor, expense) | A ticket's billable work becomes a work order or an invoice line later, not a second line-item model |

## 3. Time on a ticket

### 3.1 What exists

The desk names a Time Tracker stage. **Start working on this ticket** switches the agent's clock to that stage for the ticket; **Stop** goes back to the stage they were in before, or clocks them out. The ticket's **Time** panel shows each person's minutes and the total.

### 3.2 Manual entries (built, slice 1)

An agent logs time after the fact from the ticket's **Time** panel: **Log time** opens a short form with

- **Time spent**: `30`, `45m`, `1h 30m`, `1:30` or `1.5h`, up to 24 hours;
- **Started**: optional, in the agent's time zone; empty means the work just ended;
- **Note**: what was done, kept on the entry and shown on the ticket;
- **Billable**: shown when the desk has a stage for work that isn't billed (3.3).

The problem to solve: Time Tracker keeps one entry at a time per person, and an agent who is clocked in (Work, 9:00 to now) can't add a 14:00 to 14:30 ticket entry over it; `AddAsync` refuses the overlap. Options:

| | A. Insert over the person's time (built) | B. A duration with no time of day | C. Ticket time outside Time Tracker |
| --- | --- | --- | --- |
| How | New `ITimeTrackerService.InsertAsync`: the new entry takes its place in the day and the time it covers is cut around it (Work 9:00-14:00, ticket 14:00-14:30, Work 14:30-now) | Keep minutes on a ticket record, place nothing on the clock | A Tickets work log |
| Payroll | Unchanged total; the 30 minutes move from Work to the ticket stage and count toward its categories | Double counts the 30 minutes (Work and ticket) | Double counts |
| Reuse | Extends the owner (ladder step 3) | Second ledger | Second ledger; the catalog forbids it |

**Decision taken (reversible): A.** `InsertAsync` never cuts time from the entry's own source (another ticket's time) or time another clock keeps (Contact Center states, a time clock); it refuses the overlap instead, with the same message `AddAsync` gives. Each cut entry keeps a `TimeEntryChange` (who, why, what it was) and observers hear `Edited`, `Created` and `Deleted` as usual, so the Audit Trail shows the split.

Who: anyone who may reply on the ticket logs their own time. Logging for someone else and correcting or removing an entry go through Time Tracker's **Time entries** page, which already keeps the reason and the history (removing a split entry on the ticket would leave a hole in the person's clocked time, so it isn't offered there). Later: a **Log time** field on the reply composer, and time on behalf of a teammate for leads.

### 3.3 Billable or not (built)

A desk's time settings gain **Time not billed to the customer**, a second stage. The **Billable** box on the form picks the stage: checked uses the desk's ticket stage, unchecked the second one. The categories on each stage decide what the time counts toward (Payable on both, Billable on the first), so Time Tracker's own Hours by Category report agrees with the ticket reports. A report counts a ticket entry as not billable when its stage is any desk's not-billed stage. The start/stop timer records billable time; changing a timed entry to not billable is a correction on the Time entries page.

Rejected: a billable flag on each entry. Time Tracker's category totals would then disagree with the flag.

### 3.4 Totals by person, ticket, account and period

All totals come from the entries with source `Ticket`, read through `TimeEntryQuery` (new `Source` filter) and joined to the ticket index for the desk, account, type, priority and assignee. Entries are clipped to the period, so a timer running across midnight counts in each day. The reader sees only the tickets their ticket scope allows, the same scope the ticket list and the other ticket reports apply.

### 3.5 Resolution time, work time and effort

| Measure | Meaning | Source |
| --- | --- | --- |
| Resolution time | Calendar time from the ticket's creation to when it was solved | `TicketIndex.CreatedUtc`, `SolvedUtc` |
| Resolution in business hours | The same on the policy's schedule | ServiceLevels timers (shown by the service level report; added to the work time report later) |
| Work time | How long someone was working on the ticket: the union of its entries, so two people on it for the same hour count one hour | Time entries |
| Effort | Person-time: every person's minutes added up, so two people for an hour are two hours | Time entries |
| Touch ratio | Work time as a share of resolution time: how much of a ticket's life was spent working on it | Both |

## 4. Reports

| Report | State | Content |
| --- | --- | --- |
| **Ticket Time** | Built (slice 2) | Time logged on tickets in a period: totals and billable share; by person, account, desk and ticket type; the tickets with the most time |
| **Ticket Resolution and Work Time** | Built (slice 2) | Tickets solved in a period: resolution time against work time and effort, people per ticket, touch ratio; by desk, priority, ticket type and assignee; the tickets that took the most effort |
| **Service level by account** | Next, no decision needed | The service level compliance report gains a **By account** table (met, breached, at risk per account) |
| **Agreement Usage** | After section 6 | Per agreement: covered hours, used, left, overage; projected run-out date |
| **Agreement Burn-down** | After section 6 | Hours left over the agreement's period, a line per day, against an even burn line |
| **Account Health** | After section 6 | One row per account: tickets, time, SLA met, agreement used, satisfaction |

Both built reports need the Reports and Time Tracker features and the **View ticket reports** permission, and export to CSV and Excel through the Reports framework.

## 5. Ticket fields other service desks have

| Field | Ours today | Where it belongs |
| --- | --- | --- |
| Company | `TicketPart.AccountId` | Done |
| Contact | `TicketPart.Requester` | Done |
| Site (a location of the account) | Missing | **Ticket part property** `SiteId`, an address of the account through Addresses (an account's addresses are already kept there). Content field only if owners want free text |
| Board | Desk; boards are views over desks | Ticket boards session |
| Status, priority | Done | Done |
| Type and subtype | Ticket type is the content type | **Intake plan**: categories with sub-categories on a taxonomy (`TicketPart.CategoryId`) |
| Item (third level) | Missing | **Intake plan**: the taxonomy's third level |
| Category, subcategory | Missing | **Intake plan** |
| Owner (the one accountable, apart from the assignee doing the work) | Assignee only | **Ticket part property** `OwnerId` if Mike wants the split; most desks use the assignee. Decision 8 |
| Closed by | Timeline has it | **Ticket part property** `SolvedById` (with `SolvedUtc`) and in the index, so reports group by it |
| Source | `TicketPart.Channel` | Done |
| Billing type (billable, not billable, covered by agreement, no charge) | Missing | **Ticket part property** `BillingType`, the default for the ticket's time entries; set from the agreement when there is one (section 6) |
| Agreement | Missing | **Ticket part property** `AgreementId`, indexed; section 6 |
| SLA, SLA status, due date | Policy and timers (ServiceLevels), `NextDueUtc` | Done |
| Impact and urgency driving priority | Priority only | **Ticket part properties** `Impact` and `Urgency` with a desk **priority matrix** (impact x urgency to priority) in the desk settings. The intake plan's conditional questions can ask them; the matrix sets the priority |
| Estimated start, estimated duration | Missing | **Ticket part properties** `EstimatedStartUtc` and `EstimatedMinutes`; the work time report then compares estimate to effort. A schedule tab goes through Calendars or Dispatch, not a ticket model |
| Tasks | Missing | Linked records (`TicketPart.Links`): a work order or a checklist. A task list on tickets would be its own building block shared with Work Orders checklists |
| Time | Time panel | Done (sections 3.1 to 3.3) |
| Expenses | Missing | Work Orders line items; a ticket's expense becomes a work order line or an invoice line. No expense model in Tickets |
| Attachments | Done (Attachments building block) | Done |

Fields with "content field an owner adds" are any the owner wants per type without code (a serial number, an asset tag): Orchard content fields on the ticket type, already supported, with the intake plan's **Show this field when** rules.

## 6. Support agreements

### 6.1 What the desk needs

- An agreement with an account: number, name, status (draft, active, expired, cancelled), start and end dates.
- Coverage: one of
  - a **block of hours** (prepaid 20 hours, used until gone, optional expiry);
  - an **allowance per period** (10 hours a month, with or without roll-over);
  - **unlimited** within covered hours and services.
- **Covered hours**: a Business Hours schedule (24x7, business days 8-6).
- **What it covers**: desks, ticket types or categories; everything else is billable outside it.
- **The service level it brings**: a ServiceLevels policy, which then runs the account's tickets.
- **Usage**: billable ticket time logged against it, rounded as the agreement says (per entry, up to 15 minutes, say); warnings at 80% and 100%; what happens past 100% (keep working and bill the overage, or warn only).

### 6.2 What exists

Work Orders' `ServiceAgreement` is an agreement with an account too: number, status, account, contact, dates, time zone, notes, expiry notice, number sequence (`INumberSequence`), an account tab (`AccountAgreementsDisplayDriver`), an admin list and editor, recipes and workflows. Its field-service parts (service address, assets, visit schedules, price per visit) are specific to field work.

Two modules needing "an agreement with an account" is the reuse ladder's trigger to extract (step 4): a second agreement model inside Tickets is ruled out.

### 6.3 Options for the owner

| | A. Extract an **Agreements** building block (recommended) | B. Tickets uses Work Orders' `ServiceAgreement` | C. Tickets keeps its own support agreement |
| --- | --- | --- | --- |
| How | New `Agreements.Abstractions`/`.Core`/module: `Agreement` (number, name, status, account, contact, dates, time zone, notes, expiry notice, `Kind` open string, `Properties`), the account tab, the admin list and editor through display drivers, recipes, deployment, workflows. Work Orders moves `ServiceAgreement` onto it: its schedules, assets, address and pricing become a Work Orders part and driver on the agreement. Tickets adds support coverage the same way | Generalize `ServiceAgreement` in place | `SupportAgreement` in Tickets |
| A help desk without field service | Works | Needs Work Orders on | Works |
| One agreement covering both (maintenance visits and remote support) | Yes: one agreement, two coverage panels | Yes | No: two agreements per account |
| Work | Largest: data migration of Work Orders agreements, renamed permissions and recipe step kept as aliases | Medium, but Tickets then depends on a field-service module | Smallest, against the rules |
| Ladder step | 4 (extract) | 1 (use), with the wrong owner | Forbidden (second copy) |

**Recommendation: A.** Agreements owns the agreement and its life cycle (activation, expiry, renewal, notices, numbering); Work Orders and Tickets each add their coverage through parts, drivers and an `IAgreementUsageProvider` that reports usage (hours used by tickets, visits done by work orders), so the agreement page and the usage report never name a consumer.

### 6.4 Selling an agreement

Optional and later: a support plan sold through Carts, Checkout and Subscriptions. A subscription plan's entitlement grants an agreement (a new entitlement kind next to roles) and renews its allowance each period. This keeps selling in the commerce chain and the agreement in Agreements.

### 6.5 Tickets on an agreement

- `TicketPart.AgreementId`, indexed. A new ticket gets the account's active agreement that covers its desk and type (the only one, or the one marked default); agents may change it. Changes go on the timeline.
- The agreement's policy runs the ticket's service level, before the account's own policy (`TicketAccountPart.ServiceLevelPolicyId` stays for accounts without an agreement).
- Time logged on the ticket counts against the ticket's agreement when its stage counts as billable and the agreement covers it; the entry remembers the agreement in its `Properties` so a later change of the ticket's agreement doesn't move past usage (decision 4).
- The account page's Agreements tab shows hours used and left; the ticket's Time panel shows the agreement's balance.

## 7. Order of work

1. **Manual ticket time** (built): `ITimeTrackerService.InsertAsync`, `TimeEntryQuery.Source`, the desk's not-billed stage, the **Log time** form and the entry list on the Time panel.
2. **Time reports** (built): Ticket Time, Ticket Resolution and Work Time.
3. **Service level by account** in the service level report (no decision needed).
4. **Ticket fields** from section 5 that Mike approves (decision 8): `SolvedById`, `Impact` and `Urgency` with the priority matrix, estimates, site, billing type.
5. **Agreements building block** (after decisions 1 to 3), migrating Work Orders' agreements.
6. **Support coverage and usage** in Tickets: `AgreementId`, usage, balances, warnings.
7. **Agreement reports**: usage, burn-down, account health.

## 8. Tests

Slice 1: `TicketTimeTests` (logging over clocked-in time splits the work entry; another ticket's time and time another clock keeps are refused; the not-billed stage; the duration parser), `TimeTrackerInsertTests` (cut around, open entries, history kept, observers told). Slice 2: `TicketTimeReportsTests` (clipping to the period, scope, by person and account, union versus sum, touch ratio). Every later slice adds its own tests first.

## 9. Decisions (approved 2026-11-15)

Mike took the recommended answer to each decision ("Take recommendations").

| # | Decision | Approved |
| --- | --- | --- |
| 1 | Where agreements live (6.3) | A: extract an **Agreements** building block and move Work Orders' agreements onto it |
| 2 | Coverage kinds for v1 (6.1) | A block of hours and an allowance per month; unlimited later |
| 3 | Past 100% of an agreement | Keep working, mark the overage billable, warn the account manager at 80% and 100% |
| 4 | Which time counts | Billable ticket time, plus work order labor through Work Orders' usage provider; rounding per agreement: none, 6, 15 or 30 minutes per entry |
| 5 | The agreement's policy over the account's policy (6.5) | Yes |
| 6 | Billable by stage (3.3) | Keep |
| 7 | Inserting manual time over clocked-in time (3.2) | Keep |
| 8 | Ticket fields from section 5 | `SolvedById`, impact and urgency with a priority matrix on the desk, estimated start and minutes, billing type; site and owner only on request |
