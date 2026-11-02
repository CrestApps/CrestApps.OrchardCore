# Time Tracker, people and teams: design review

Status: decided 2026-11-02. This review compares what PR A built for time and people with three suggestions from review, and records what changes and in what order.

## 1. Time tracking

### What exists (Workforce Time Clock, PR A)

- **Punches** (`TimePunch`): In, Out, Meal start and end, Break start and end, Activity change and Job start and end, from the web, mobile, a kiosk, Contact Center sign-in, the API or a manager, with geofence data and corrections (`PunchCorrection`) that a manager approves.
- **Activity codes** (`ActivityCode`): configurable, but each has one fixed `Kind` (Work, Break, Meal, Meeting, Training, Coaching and so on) and two flags, `IsPaid` and `CountsAsWork`. Shift templates and segments use the same codes for planned time.
- **Pay codes** (`PayCode`): Regular, Overtime, Double time, Holiday, Paid time off, Predictability pay, Call back, Shift differential, Unpaid; with multipliers, premiums and time windows.
- **Timesheets**: lines by day, pay code and activity code, computed from punches, shifts and time off; submitted, approved, locked and exported to payroll.
- **Time off**: types (paid or not, hours or days), policies with accruals, tenure tiers, a maximum, carry-over and a yearly reset, a ledger, requests and approvals, allotments (how many people may be off on a day) and blackout dates.
- **Contact Center**: signing in and out of the Contact Center punches the agent in and out (`SignInPunchHandler`). Agent states (available, on a call, wrap-up, break codes) are read by adherence only.
- **Audit**: punches keep who changed them and why (`ReplacesPunchId`, `Reason`, `Actor`); nothing goes to Orchard's Audit Trail.

### What was suggested

A **Time Tracker** module: configurable stages (Work, Break, Lunch and so on) that each belong to one or more **stage categories** (Payable, Billable, Break, Vacation, PTO and so on); clocking in and out and switching stage through the day; the day's progression per person; a report with a column per category and totals; Contact Center states turned into stages automatically; people reviewing their own time; managers editing it, with every change in the Audit Trail; and time off with paid and unpaid time and allocated hours that expire.

### Comparison

| Need | Today | Suggested | Verdict |
| --- | --- | --- | --- |
| Configurable stages | Activity codes | Stages | Same idea; keep one concept. |
| How time is counted | One fixed kind and two flags per code | Many categories per stage, configured | **Suggested is better.** Billable time, PTO or a company's own groupings can't be said today. |
| Report by category | Timesheet lines by pay code | A column per category, per person, with totals | **Suggested is better**, and pay codes can be derived from categories. |
| Day's progression | Punches, read through the timesheet | Entries with a start, an end and a stage, as a timeline | **Suggested is better**: an entry is easier to review, edit and total than a pair of punches. |
| Use without shift planning | Needs workers, teams and Time Clock policies | Works for any user | **Suggested is better**: billable time tracking is useful on sites with no workforce planning. |
| Contact Center | Sign-in and sign-out only | Every agent state mapped to a stage | **Suggested is better**, and adherence already listens to those states. |
| Manager edits and audit | Corrections with approval; no Audit Trail | Manager edits; every change in the Audit Trail | **Suggested is better**; keep worker-requested corrections too. |
| Time off granted with an expiry | Policies accrue and reset yearly; no expiring grant | Grants such as 40 hours expiring 30 December 2026 | **Suggested is better**; used oldest grant first. |
| Self-review, paid and unpaid time off | Exist | Same | Keep. |

### Decision

1. A new **Time Tracker** module (`CrestApps.OrchardCore.TimeTracker`, with `.Abstractions` and `.Core`) that depends on Users only:
   - `TimeStage` (name, code, color, whether it is on the clock, its categories, whether people may pick it themselves).
   - `TimeCategory` (name, code, color, report order, and how it counts: added, or a share such as 1.5 for an overtime premium).
   - `TimeEntry` (user, stage, start, end or open, source and reference, note), with no overlaps for a person (a lock per person).
   - `ITimeTrackerService`: clock in, switch stage, clock out, add, edit and delete (a manager, with a reason), and totals by category for people and a range.
   - Pages: **My time** (clock and switch, the day's timeline, the week), **Time entries** (an admin list with filters, manager edits), **Time report** (a row per person, a column per category, totals) and a report data source; stages and categories as admin lists.
   - Audit Trail events for every entry change (when Audit Trail is on); deployment and recipe steps for stages, categories and entries.
2. **Workforce Time Clock** builds on it. *Revised when D2 was built, after reading the time clock again:* punches stay the clock's record, and each worker's day is kept in Time Tracker from them, rather than punches being replaced by entries. Replacing them would have lost three things that work today: workers without a login (kiosk only) have no Time Tracker person; the original punch, never changed, is the record payroll audits ask for, with its device, location and geofence; and rounding, grace, meal and overtime rules, and attendance, are written against punches. So:
   - after every punch, correction and removal, the worker's entries around it are rebuilt from the punches (paired as timesheets pair them, without rounding) and kept by the clock (`ManagedBy`);
   - every activity code points at a stage (the same name or code, or a new one counting toward Paid and Worked), and meals and breaks use the meal and break codes' stages;
   - Time Tracker shows those entries read-only and links to the clock, so time is corrected in one place; My Time does too for those workers;
   - earlier punches are brought in by a background task, a few workers at a time;
   - timesheets keep their pay codes; Time Tracker's categories are the reporting view of the same time;
   - the time clock's pages, and Predictability Pay, move under the Time Tracker menu.
3. **Contact Center**: a mapping from agent states and break codes to stages (in Workforce Contact Center, which already references both), so an agent's day is tracked while they work.
4. **Time off**: grants with an expiry in the ledger, used oldest first; an approved request records its hours as entries in the type's stage, so the report shows them with worked time.

## 2. Workers and users

### What exists

`WorkerProfile` is its own record with an optional `UserId`: display name, email and phone, employee number, job title, employment type and status, hire, seniority and termination dates, manager, team memberships with effective dates, time zone, home base, contracted, minimum and maximum hours, availability, pay rates and time clock settings. It has its own list and editor.

### What was suggested

Drop the worker UI and extend the Orchard user with the fields needed, so there is one place to manage people.

### Comparison

A separate record is still needed, because:

- some workers have no login (they clock in at a kiosk with a badge, or are seasonal);
- HR data is dated and kept (team moves, pay rates, end dates) after an account is closed or deleted, for payroll and compliance;
- the scheduling, adherence and payroll queries read indexed worker rows, not user documents.

But the suggestion is right that people shouldn't be managed in two places.

### Decision

- The **user editor** gets a **Workforce** section (a display driver on the user) that edits that person's worker record in place, and creates it when needed.
- A linked worker takes its name and email from the user; they aren't typed twice.
- The **Workers** list stays, for HR views and for people without an account.

## 3. Teams

Teams are used in many projects, so they move to a shared **Teams** module. A team is a content item of a Team content type with a `TeamPart` (parent, kind, time zone, place), so sites add fields in the admin. Memberships with their dates and roles are records of their own, indexed, rather than part of the content item, so changing a member doesn't version the team. Workforce adds its parts (the team calendar, linked role). It is a separate PR after the Workforce stack, because Workforce, Contact Center Workforce and Dispatch all read teams.

## 4. Schedules and the Calendars module

Review asked that Workforce's schedules be an extension of the Calendars module.

### What exists

Shift planning already sits on Calendars where it matters: each team has a **Workforce** calendar (a calendar type of its own, kept by Workforce, with `SourceId` the team), and publishing a schedule writes its shifts there as events, beside time off and dispatch jobs, so they show on **Calendars > Calendar**, in "Whose schedule", in Team Availability, in subscription feeds and as busy time for appointments. What Workforce adds is the **planner**: a draft per team and period, the board of workers by day, coverage, auto-schedule, rotations, copying, labor rules, and publishing versions with a change log.

### Comparison

- Planning in the calendar view itself would put drafts on the live calendar, where workers and feeds see them, and the calendar grid has no room for coverage rows, weekly hours or auto-schedule. The planner stays a page of its own.
- The pages were under **Workforce** while what they make lives under **Calendars**, and Scheduling, the other module that extends Calendars, puts its pages under **Calendars**.

### Decision

- The shift planning pages move under the **Calendars** menu, after Scheduling's: **Shift Schedules** (renamed from Schedules, beside "Whose schedule"), Open Shifts, Shift Trades, Rotations, Coverage and Shift Templates.
- A team's Workforce calendar on **Manage Calendars** gets **Shift schedules** in its actions, opening that team's schedules; the planner already links back to the team calendar.
- Nothing changes in how shifts are stored or published.

## 5. Consistency rules from review

Every module in the plan follows these: admin lists through `<admin-list>`; forms with the `ocat-*` classes, one label and field per line, grouped in cards; a deployment step, a recipe step and the CrestApps `IRecipeStep` definition for every kind of stored record; `ConfigurationSchema.json` for every options class read from configuration; an icon on every top-level menu item; and no one-time setup pages in the menu.

## 6. Order of work

In PR C (#1252), after C7, one commit each (fewer PRs, one per plan):

1. D1 Time Tracker module: stages, categories, entries, the service, My time, Time entries, the time report, Audit Trail, deployment and recipe steps.
2. D2 Workforce Time Clock on Time Tracker, with the migration of punches.
3. D3 Contact Center states to stages.
4. D4 Time off grants with an expiry, and approved time off as entries.
5. D5 The Workforce section in the user editor; names from the user.
6. D6 Consistency pass over every module (forms in cards, menu icon and Get Started, deployment, recipe and `IRecipeStep` for every record, `ConfigurationSchema.json`).

Then C8 technician mobile, C9 OR-Tools and C10 API, AI tools, reports and integrations, following the same rules; and the Teams module as its own PR.
