# CrestApps: operations and workforce

Part of the [AI tool coverage plan](README.md). Nothing in this file is built yet. The design rules, the permission and approval model and the test plan that apply to every tool below are in [01-foundation.md](01-foundation.md) and [03-testing.md](03-testing.md); the final agent roster is in [02-agents.md](02-agents.md) and wins over the agent notes in this file.

Scope: `CrestApps.OrchardCore.Calendars`, `.Scheduling`, `.Dispatch`, `.WorkOrders`, `.TimeTracker`, `.Workforce`, `.Workforce.ContactCenter` and `.Assets` under `src/Modules/`, with their `src/Core/*` and `src/Abstractions/*` projects. Everything below was read from code; anything not checked is marked "unverified".

Facts that hold for the whole group:

- 25 AI tools exist today: Calendars 6, Scheduling 4, Dispatch 7 (three of them are work-order tools), Workforce 6, Workforce.ContactCenter 2. WorkOrders, TimeTracker and Assets register none. None of the 25 calls `.RequireApproval()`.
- One agent exists: `CalendarAgent` (`Calendars/Templates/Profiles/calendar-agent.md`, `RequiresFeatures: CrestApps.OrchardCore.Calendars`, `ToolNames:` the 6 calendar and 4 scheduling tools).
- Only one site settings page exists in the whole group: Dispatch (`DispatchSettings`, group `dispatch`). Everything else is configured through catalog records (list / edit / delete screens), and every one of those catalogs has a recipe step, so `importOrchardCoreRecipe` can already write them (it cannot list, read or delete them, and it checks the recipe permission, not the module's permission).
- Workforce tools must check two things, as the controllers do: the permission, and the manager's scope (`IWorkforceScopeService.GetManagedScopeAsync(user)` / `ManagesWorkerAsync`), because a manager without `ManageAllWorkforce` only sees the teams they manage.
- Catalog validation lives in `ICatalogEntryHandler<T>.ValidatingAsync` for WorkOrders, Assets, TimeTracker, Workforce and Workforce.ContactCenter, so a tool can call the manager and get the same errors as the screen. Scheduling is the exception (see its notes).
- Where a tool sends a message to a customer or a worker (visit notices by SMS/email, calendar invitations by email, workforce notifications by inbox, web push and email) I classed it `External`. Where it changes what someone is paid (balances, pay rates, predictability pay, payroll export) I also classed it `External` ("costs money").

## Calendars — `CrestApps.OrchardCore.Calendars`, `CrestApps.OrchardCore.Calendars.Teams`, `.Resources`, `.Office`, `.Reminders`, `.Invitations`

**What an admin can do in the UI:**

- Calendars → Calendar (`Admin/calendar`, `CalendarController.Index`): day, week, month and agenda view of every calendar the user can see; pick calendars; view other people's schedules with `schedule` / `people` (needs `ListCalendarEvents`); drag an event to move it (`POST Admin/api/calendars/occurrences/move`); create, edit, cancel, delete an event in a modal (`CalendarEventsController`: `Choose`, `Create`, `Edit`, `Cancel`, `Delete`); confirm a tentative event (`Confirm`, needs Manage on the calendar); answer an invitation (`Respond`: accept / tentative / decline).
- Event editor fields: title, event type (each calendar type has its own list), all-day, start, end, time zone, location, description, status, transparency (busy / free), visibility, color, category, reminders (Reminders feature), recurrence (frequency, interval, weekdays, monthly mode, ends never / until / count, or a custom RRULE), attendees (users, guest emails, resources = Room and Equipment calendars); for a repeating event the scope: this occurrence, this and following, all.
- Calendars → Manage Calendars (`Admin/calendars`, `CalendarsController`): list with search and type filter; create by type (`calendars/create/{type}`); edit — General (name, description, color, time zone, visibility, enabled), Owner (a role for Team calendars; the user of a Personal calendar only with `ManageAllCalendars`), Sharing (users and roles at FreeBusy / Read / Write / Manage), Overlaps (limit + how many events may overlap), Room / Equipment details (location, seats, amenities), Office details (address, contact); delete (refused when the calendar has a `Source`, i.e. a module owns it).
- Calendars → Categories (`Admin/calendar-categories`, `CalendarCategoriesController`): list / search, create or edit (name, color, shared with everyone), delete. A user manages their own categories; shared ones need `ManageAllCalendars`.
- Calendars → Team Availability (Teams feature, `Admin/calendar/availability`, `TeamAvailabilityController`): choose a Team calendar, a day and a meeting length; shows a busy grid for each user in the team's role (up to a cap) and the first 10 times everyone is free.
- Reminders feature: on-screen reminders with dismiss (`Admin/api/calendars/reminders`, `.../{id}/dismiss`). Invitations feature: emails attendees with an .ics file and a public answer link (`calendars/respond/{eventId}/{token}/{response}`); no settings page.
- Not in the UI (the task brief assumed they were): calendar types are registered in code with `AddCalendarType` (General, Personal, Team, Room, Equipment, Office; `Appointments` from Scheduling and `Workforce` from Workforce are `ManagedByCode()`), so there is no "manage calendar types" screen. There are no business-hours or holiday settings in Calendars: a holiday is an event (`Holiday` on General, `Closed` on Office) and working hours belong to Scheduling resources.

**Already covered:**

- Existing tools (registered in `Calendars/AIToolsStartup.cs`, `[RequireFeatures("CrestApps.OrchardCore.AI")]`, category "Calendar", base class `CalendarAIToolBase` which requires a signed-in user with `AccessCalendars`):
  - `listCalendars` — "Lists the calendars the user can see, with each calendar's id, type, time zone and whether the user can add events to it." No arguments.
  - `findCalendarEvents` — "Lists the events on the user's calendars between two times, with their ids, times and attendees. Events on calendars the user can only see as busy time have no details." Arguments `from`, `to`, `calendarIds`; at most 62 days and 200 occurrences.
  - `findFreeTime` — "Finds stretches of time when the user, and any other users and calendars named, are all free for at least the given number of minutes." Arguments `from`, `to`, `durationMinutes`, `userIds`, `calendarIds`; at most 31 days, 20 users, 15 windows.
  - `createCalendarEvent` — "Adds an event to a calendar the user can add to. Read the time back to the user and get a clear yes before calling it. Fails when a room or calendar is already full at that time." Arguments `title`, `start`, `end`, `calendarId` (default: the user's personal calendar, created on demand), `location`, `description`, `attendees`.
  - `moveCalendarEvent` — "Moves an event the user can change to another time. Confirm the new time with the user first." Arguments `eventId`, `occurrenceStart`, `start`, `end`.
  - `cancelCalendarEvent` — "Cancels an event the user can change. Confirm with the user first." Arguments `eventId`, `occurrenceStart`.
- Agent `CalendarAgent` owns those six plus the four scheduling tools.
- Recipe step `Calendars` (sections `Categories` and `Calendars`, each calendar with optional `Events`; runs with `SkipConflictCheck` and `SendInvitations = false`) and deployment step `Calendars` (`IncludeEvents`, `IncludeCategories`).
- `CalendarWidgetPart` (dashboard widget) is a content part: the content tools cover it.
- Workflow activities exist for the same actions (Create / Update / Cancel / Get / Find events, Find free time, Add / Remove attendee), so `createOrUpdateWorkflow` can automate them.

**Proposed changes to existing tools (no new names):**

- `listCalendars`: add `search` and `type` filters and return `enabled`, `ownerKind`, `ownerId`, `accessLevel`, `source`; the UI list has both filters.
- `findCalendarEvents`: add `userIds` / `everyone` to read other people's schedules, checking `ListCalendarEvents` on `CalendarSchedule` as `CalendarAdminEndpoints.GetOccurrencesAsync` does; return `eventType`, `categoryId`, `recurring`.
- `createCalendarEvent`: add `eventType`, `timeZoneId`, `allDay`, `recurrence`, `status`, `transparency`, `categoryId`, `reminderMinutes`, `resourceCalendarIds` (`CalendarEventInput` already has most of them). With Calendar Invitations on it emails every attendee, so it should become `External` with approval when `attendees` is not empty.
- `moveCalendarEvent` / `cancelCalendarEvent`: add `scope` (`ThisOccurrence`, `ThisAndFollowing`, `All` — `CalendarEditScope`); today only one occurrence or the whole series can be changed. `cancelCalendarEvent` should require approval (it emails attendees when Invitations is on).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getCalendar` | Read | no | `AccessCalendars` and calendar access of Read or higher (`ICalendarAccessService.GetAccessLevelAsync`); shares and subscription only at Manage | Gets one calendar: type, owner, time zone, visibility, enabled, overlap limit, room or office details, its event types, who it is shared with, whether it has a feed address, and its subscription state and last sync error. Use it before changing a calendar. | CalendarAdminAgent |
| `listCalendarTypes` | Read | no | `AccessCalendars` | Lists the calendar types enabled on the site, which ones the user may create, and each type's event types and defaults. Use it before `saveCalendar` or to choose an event type. | CalendarAdminAgent |
| `saveCalendar` | Write | no | New: `ICalendarAccessService.CanCreateAsync` (`CreateCalendars`; a personal calendar only needs `AccessCalendars`). Existing: calendar access = Manage. Changing a personal calendar's owner: `ManageAllCalendars` | Creates a calendar of a given type or updates one: name, description, color, time zone, visibility, enabled, owner (user or role), overlap limit and capacity, and room, equipment or office details. It does not change sharing, the feed or the subscription. | CalendarAdminAgent |
| `shareCalendar` | Security | yes | Calendar access = Manage | Replaces who a calendar is shared with: users and roles at free/busy, read, write or manage level. Read the current shares with `getCalendar` first, because the list you give replaces them. | CalendarAdminAgent |
| `deleteCalendar` | Destructive | yes | Calendar access = Manage | Deletes a calendar and every event on it. Refused for a calendar a module manages (appointments, workforce teams, fleet assets). | CalendarAdminAgent |
| `getCalendarEvent` | Read | no | `AccessCalendars` and `ViewCalendarEvents` / `ViewOwnCalendarEvents` on the event (`CalendarEventAuthorizationHandler`) | Gets one event in full: type, times and time zone, recurrence, status, visibility, category, reminders, each attendee with their answer, and whether the user can change it. Use it before `updateCalendarEvent`. | CalendarAgent |
| `updateCalendarEvent` | Write | no | `CalendarEventEditor.CanChangeAsync` (calendar access of Write or higher and the event is editable); setting status to Confirmed needs Manage | Changes an event's title, type, times, time zone, location, description, status, busy/free, visibility, category, reminders, recurrence or attendees; for a repeating event say which occurrences (this one, this and following, all). Use `moveCalendarEvent` when only the time changes. | CalendarAgent |
| `deleteCalendarEvent` | Destructive | yes | `CalendarEventEditor.CanChangeAsync` | Permanently deletes an event (the whole series for a repeating event). Prefer `cancelCalendarEvent`, which keeps the event and tells the attendees. | CalendarAgent |
| `respondToCalendarEvent` | Write | no | Signed-in attendee of the event (`ICalendarEventManager.RespondAsync` with `user:{id}`) | Records the user's own answer to an invitation: accepted, tentative or declined. It cannot answer for someone else. | CalendarAgent |
| `listCalendarCategories` | Read | no | `AccessCalendars` | Lists the event categories the user can use (their own and the shared ones) with id, name and color. Use it to find a category id for an event. | CalendarAgent |
| `saveCalendarCategory` | Write | no | `AccessCalendars`; a shared category needs `ManageAllCalendars` | Creates or updates an event category: name, color (#rrggbb) and whether everyone shares it. | CalendarAdminAgent |
| `deleteCalendarCategory` | Destructive | yes | Owner of the category, or `ManageAllCalendars` for a shared one | Deletes an event category. Events that used it keep their own color and lose the category. | CalendarAdminAgent |
| `getTeamAvailability` | Read | no | `AccessCalendars` and Read access to the Team calendar | For a team calendar and a day, lists each team member's busy times and the times everyone is free for a meeting of the given length. Needs the Team Calendars feature. | CalendarAgent |

**Implementation notes:**

- Services: `ICalendarService` (`NewAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `FindByIdAsync`), `ICalendarAccessService`, `ICalendarTypeManager`, `ICalendarEventManager` (`UpdateAsync`, `UpdateOccurrenceAsync`, `DeleteAsync`, `RespondAsync`, `AddAttendeeAsync`, `RemoveAttendeeAsync`), `ICalendarCategoryService`, `ICalendarAvailabilityService`, `CalendarEventEditor` (module service: `CanAddAsync`, `CanChangeAsync`, `CopyEditableFields`), `CalendarEventInputApplier` (already used by the tools and the workflow tasks).
- The calendar editor is built from display drivers (`CalendarDisplayDriver`, `CalendarOwnerDisplayDriver`, `CalendarSharingDisplayDriver`, `CalendarRulesDisplayDriver`, `ResourceCalendarSettingsDisplayDriver`, `OfficeCalendarSettingsDisplayDriver`). The only validation outside the service is "name is required" and "capacity ≥ 1" in the drivers; `ICalendarService.CreateAsync` / `UpdateAsync` return `CalendarResult` errors for the rest. `CalendarsController.EnsureCreatorCanManage` (the creator gets a Manage share on a calendar they do not own) is controller-only logic the tool must repeat.
- `ResourceCalendarSettings` and `OfficeCalendarSettings` are stored as properties on the calendar (`calendar.Put(...)`).
- `getTeamAvailability`: the member lookup (`UserByRoleNameIndex` on the calendar's `OwnerId` role, capped) and the grid are inside `TeamAvailabilityController.Index`; extract them to a service first. With Teams enabled, membership also comes from `TeamMembershipCalendarAccessContributor`.
- `shareCalendar` changes who can read and write a calendar, hence `Security`. Role and user ids must be validated (the UI offers every role except Anonymous).
- `deleteCalendar` removes every event of the calendar (`DefaultCalendarService.DeleteAsync` pages and deletes them); there is no undo.
- Not proposed: reminders list / dismiss (personal, on-screen only), and the `Confirm` action (folded into `updateCalendarEvent` with `status: Confirmed`).

**Test prompts:**

- "Create a room calendar called Boardroom, 12 seats, Eastern time, and let the Sales role book it."
- "Who can see the Marketing team calendar? Give Priya write access."
- "Make my Thursday stand-up repeat every weekday until the end of March and add Tom as an attendee."
- "When is everyone on the Support team free for 45 minutes tomorrow?"

## Calendar Feeds and External Calendars — `CrestApps.OrchardCore.Calendars.Feeds`, `CrestApps.OrchardCore.Calendars.External`

**What an admin can do in the UI:**

- Manage Calendars → a calendar's actions menu (`CalendarFeedsController`, Feeds feature): Subscription address (`calendars/{id}/feed`: show the private iCalendar URL; make a new one, which stops the old one; remove it), Export (`calendars/{id}/export`: download every event as .ics, needs Read), Import (`calendars/{id}/import`: upload an .ics file of up to 10 MB, needs Write; reports created / updated / unchanged and the first 10 errors).
- Edit calendar → Subscription tab (External feature, `CalendarSubscriptionDisplayDriver`): provider (built-in: iCalendar address), address (`https://`, `http://` or `webcal://`), interval (5 to 10080 minutes), enabled, two-way (only for writable providers), conflict policy (external wins / local wins); shows last sync, last result, last error and "needs reconnect". Saving again clears "needs reconnect".
- The sync itself runs in the background task "External calendar synchronization" (every 5 minutes); there is no "sync now" button. A health check `CalendarSyncHealthCheck` reports failures.

**Already covered:** nothing. The `Calendars` recipe step can write a `CalendarSubscription` through the calendar's `Properties`, and the deployment step removes `FeedToken`.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getCalendarFeedAddress` | Security | yes | Calendar access = Manage | Gets the private iCalendar address other calendar apps subscribe to, when the calendar has one. Anyone who has the address can read the calendar, so only give it to the calendar's manager. | CalendarAdminAgent |
| `resetCalendarFeedAddress` | Security | yes | Calendar access = Manage | Makes a new private subscription address for a calendar, or removes it. The old address stops working at once, so everyone subscribed must be given the new one. | CalendarAdminAgent |
| `importCalendarEvents` | Write | no | Calendar access of Write or higher | Imports events from iCalendar (.ics) text or a media file into a calendar and reports how many were added, changed and left alone. Events are matched by their UID, so importing twice does not duplicate them. | CalendarAdminAgent |
| `saveCalendarSubscription` | External | yes | Calendar access = Manage | Sets or turns off a calendar's subscription to an external calendar: provider, address, how often to sync, one-way or two-way, and which copy wins a conflict. The site then fetches that address on a schedule. | CalendarAdminAgent |
| `syncCalendar` | External | yes | Calendar access = Manage | Syncs a subscribed calendar with its external calendar now and returns how many events were added, changed and removed, or the error. Use it after changing a subscription or when the last sync failed. | CalendarAdminAgent |

**Implementation notes:**

- Feed: the token is `calendar.FeedToken` (24 random bytes, hex); `CalendarFeedsController.FeedPost` sets or clears it and calls `ICalendarService.UpdateAsync`; the public route is `calendars/feed/{token}.ics`. It is a bearer secret: never log it.
- Import: `ICalendarReader.Read(text, timeZone)` then `ICalendarImporter.ImportAsync(calendar, events, options)`. Export has no tool: `getCalendar` should return the admin export URL (`Admin/calendars/{id}/export`) instead of streaming a file through the model.
- Subscription: stored as `CalendarSubscription` on the calendar; the driver resets `LastAttemptUtc`, `Failures`, `SyncToken` and `PendingDeletes` when the provider or address changes — repeat that. Providers come from `CalendarSyncService.ListProviders()`; two-way needs `IWritableCalendarProvider`. The address is user-supplied and fetched server-side, so validate the scheme as the driver does and treat it as an SSRF surface.
- `syncCalendar` has no UI equivalent; it would call `CalendarSyncService.SyncAsync(calendarId)`. Keep it only if wanted.

**Test prompts:**

- "Give me the subscription link for the Company Holidays calendar so I can add it to Outlook."
- "Subscribe the Public Holidays calendar to this address and refresh it every 6 hours: https://…/holidays.ics"
- "The Partner Events calendar stopped updating. What was the last sync error?"

## Scheduling — `CrestApps.OrchardCore.Scheduling`

**What an admin can do in the UI:**

- Calendars → Appointments (`Admin/scheduling/appointments`, `AppointmentsController`, `ViewAppointments`): list with search and status filter (Requested, Confirmed, Held, Completed, No-show, Cancelled, Expired); New (`scheduling/appointments/new`: appointment type, an open time, customer name / email / phone, notes — `ManageAppointments`); Detail (`scheduling/appointments/{id}`: resources, status, the customer's manage link, open times for a move); Do (`.../{id}/do`: `Confirm`, `Complete`, `NoShow`, `Cancel` with a reason, `Reschedule` to a new start — `ManageAppointments`).
- Calendars → Appointment Types (`scheduling/types`, `AppointmentTypesController`, `ManageScheduling`): list, create / edit, delete. Fields: name, description, slug (public booking address), duration, slot interval, buffer before / after, minimum notice, horizon days, max per day / week, max upcoming per customer, cancel and reschedule notice, capacity, requires confirmation, hold minutes, allow self-service, location, color, enabled, and resource requirements (groups of resources, each with an assignment rule and per-resource weights).
- Calendars → Scheduling Resources (`scheduling/resources`, `SchedulingResourcesController`, `ManageScheduling`): list, create / edit, delete (its calendar and appointments are kept). Fields: name, kind (Staff, Room, Equipment, Other), user (a staff picker when Staff is on), calendar, time zone, availability mode (WorkingHours or Shifts), shift calendars, working hours per weekday, date overrides (closed days or different hours on a date), enabled. This is where business hours and holidays are set. Saving ensures an `Appointments` calendar for the resource.
- Calendars → Waitlist (`scheduling/waitlist`, `WaitlistController`, `ManageAppointments`): list, add (appointment type, from / to days, customer name / email / phone), remove (sets status Removed).
- Background task "Appointment holds" (every minute) ends expired holds.
- Workflows: tasks Book, Cancel, Confirm, Reschedule, Get appointment, Find slots, Add to waitlist; events Requested, Confirmed, Rescheduled, Cancelled, Completed, No-show, Hold expired, Starting soon, Waitlist slot available.

**Already covered:**

- Existing tools (`Scheduling/AIToolsStartup.cs`, `[Feature(Scheduling)]`, `[RequireFeatures("CrestApps.OrchardCore.AI")]`, category "Scheduling", base `SchedulingAIToolBase`). They are written for a customer-facing assistant: they do not require a signed-in user; a user with `ManageAppointments` also sees types that are not self-service and is not held to the notice rules.
  - `listAppointmentTypes` — "Lists the kinds of appointments that can be booked, with their ids and how long each lasts." No arguments; enabled types only.
  - `findAppointmentSlots` — "Finds open times for a kind of appointment. Offer the customer two or three of them." Arguments `appointmentTypeId`, `from`, `to`; at most 14 days, 20 slots.
  - `bookAppointment` — "Books an appointment at an open time. Read the time back to the customer and get a clear yes first. If the result says it waits for confirmation, tell the customer it is requested, not booked." Arguments `appointmentTypeId`, `startUtc`, `customerName`, `customerEmail`, `customerPhone`, `notes`. Returns the appointment's `manageCode` (the manage-link secret).
  - `changeAppointment` — "Cancels or moves a customer's appointment. Confirm with the customer first." Arguments `appointmentId`, `proof` (required), `action` (`cancel` or `move`), `newStartUtc`.
- `CalendarAgent` already lists the four tools.
- Recipe step `Scheduling` (sections `Resources`, `AppointmentTypes`, `Appointments`, `Waitlist`) and deployment step `Scheduling` (`IncludeAppointments`, `IncludeWaitlist`).

**Proposed changes to existing tools:**

- `listAppointmentTypes`: add `includeDisabled` for users with `ManageScheduling`.
- `findAppointmentSlots` and `bookAppointment`: add `resourceId` (`ISchedulingAvailabilityService.FindSlotsAsync` and `AppointmentRequest.ResourceId` already take it), so staff can book "with Dr. Lee".
- `changeAppointment`: `proof` is required by the schema but ignored when the user has `ManageAppointments`; make it optional for them, and add `reason` (cancel) and `resourceId` (move).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listAppointments` | Read | no | `ViewAppointments` | Lists appointments, newest first, filtered by status, a search on the customer's name, email or phone, and a start date, with paging. Use it to find an appointment's id. | CalendarAgent |
| `getAppointment` | Read | no | `ViewAppointments` | Gets one appointment: type, time, status, resources, customer, notes, cancel reason and the calendar event it sits on. | CalendarAgent |
| `setAppointmentStatus` | Write | no | `ManageAppointments` | Confirms a requested appointment, or marks a past one completed or no-show. Use `changeAppointment` to move or cancel. | CalendarAgent |
| `getAppointmentType` | Read | no | `ManageScheduling` | Gets an appointment type in full: timing, limits, notice rules, capacity, resource groups with their assignment rules, self-service and its public booking address. | CalendarAdminAgent |
| `saveAppointmentType` | Write | no | `ManageScheduling` | Creates or updates an appointment type: name, slug, duration, slot interval, buffers, notice, horizon, daily and weekly limits, capacity, confirmation, hold time, self-service, location, color, enabled and the groups of resources it needs. A resource can be in only one group. | CalendarAdminAgent |
| `deleteAppointmentType` | Destructive | yes | `ManageScheduling` | Deletes an appointment type. Appointments already booked keep pointing at it, so turn the type off instead when it has appointments. | CalendarAdminAgent |
| `listSchedulingResources` | Read | no | `ManageScheduling` | Lists the staff, rooms and equipment that take appointments, each with its kind, user, calendar, time zone, working hours, date overrides and whether it is enabled. | CalendarAdminAgent |
| `saveSchedulingResource` | Write | no | `ManageScheduling` | Creates or updates a scheduling resource: name, kind, linked user, time zone, whether it follows working hours or shift calendars, weekly working hours, closed days and special hours on dates, and enabled. Use it to set business hours and holidays. | CalendarAdminAgent |
| `deleteSchedulingResource` | Destructive | yes | `ManageScheduling` | Deletes a scheduling resource. Its calendar and its appointments are kept, but it can no longer be booked. | CalendarAdminAgent |
| `listAppointmentWaitlist` | Read | no | `ManageAppointments` | Lists the customers waiting for an opening, with the appointment type, the days they can come and their status. | CalendarAgent |
| `addToAppointmentWaitlist` | Write | no | `ManageAppointments` | Adds a customer to the waitlist of an appointment type for a range of days. They are told when a time opens (through the Waitlist Slot Available workflow event). | CalendarAgent |
| `removeFromAppointmentWaitlist` | Write | no | `ManageAppointments` | Takes a customer off the waitlist. The entry is kept with status Removed. | CalendarAgent |

**Implementation notes:**

- Services: `IAppointmentService` (`BookAsync`, `ConfirmAsync`, `CompleteAsync(noShow)`, `RescheduleAsync`, `CancelAsync`, `FindByIdAsync`), `ISchedulingAvailabilityService.FindSlotsAsync`, and the concrete stores in `Scheduling.Core/Services`: `AppointmentStore.PageAsync(status, search, fromUtc, skip, take)`, `AppointmentTypeStore`, `SchedulingResourceStore`, `WaitlistStore`. `SchedulingPageService.BuildDetailAsync` and `SchedulingResourceCalendars.EnsureCalendarAsync` are module services.
- Gap: validation of appointment types and resources exists only in the display drivers (`AppointmentTypeFieldsDisplayDriver`: name required, duration and interval, one group per resource, unique slug; `SchedulingResourceFieldsDisplayDriver`: name, working-hours text parsing, time zone). There is no `ICatalogEntryHandler` for them (only `SchedulingResourceStaffHandler` when Staff is on), and the recipe step does not validate. Move the checks into handlers before adding the save tools.
- `saveSchedulingResource` must call `EnsureCalendarAsync` (as the controller does) and, with Staff on, the `Bookable` capability is added by `SchedulingResourceStaffHandler`.
- `getAppointment` should not return `ManageToken`; `bookAppointment` returns it today as `manageCode`, which is a secret that lets anyone move or cancel the appointment.
- `setAppointmentStatus` raises workflow events (Confirmed, Completed, No-show) that sites commonly use to email the customer; consider approval if that matters.

**Test prompts:**

- "Show me today's appointments that still wait for confirmation and confirm the one for Maria Lopez."
- "Add a 45-minute 'Initial consultation' appointment type with a 15-minute buffer after, bookable online, with Dr. Lee or Dr. Shah."
- "Dr. Lee works Monday to Thursday 8 to 4 and is off on December 24."
- "Put John Reed on the waitlist for a cleaning next week."

## Work Orders — `CrestApps.OrchardCore.WorkOrders`

**What an admin can do in the UI:**

- Field Service → Work Orders (`Admin/work-orders`, `WorkOrdersController`, `ViewWorkOrders`): list filtered by status, type, priority, asset and a search on number or summary; New by type (`work-orders/new/{typeId}`); Edit (`work-orders/edit/{id}`); Details (`work-orders/view/{id}`) with panels other modules add (visits, technicians). All changes need `ManageWorkOrders`.
- Work order fields: type, priority, summary, description, customer / account / contact, contact name / phone / email, service address, access notes, territory, assets, earliest / latest time, required skills, estimated minutes, preferred and excluded technicians (added by Dispatch), lines (product, service, labor, expense with estimated quantity, unit price, billable), checklists.
- Actions on the details page: Status (`.../status`: next status from `WorkOrderStatusRules`, sub-status, note; Canceled needs a reason; Closed and Canceled are final), Use (`.../use`: record a quantity used on a line, from a stock location when Inventory is on), Answers (`.../checklists/{n}`: save checklist answers), Sign (`.../sign`: a signer's name), Note (`.../note`), Complete (`.../complete`: checks required answers, signature and photos, then invoices when the type is billable and Transactions is on), Follow-up (`.../follow-up`: a new work order for the same customer, place and assets). There is no delete.
- Field Service → Work Order Types / Checklists / Service Levels / Sub-statuses (`work-orders/settings/{kind}`, `WorkOrderSettingsController`, `ManageWorkOrderSettings`): list with search, edit, delete (refused while in use).
  - Type: name, color, default duration, required skills with minimum proficiency, required certifications, crew size, required equipment types, checklists, default lines, service level, billing mode (not billable, fixed price, time and materials), currency, fixed price, default priority, requires signature, appointment types that create it, required photos.
  - Checklist: name and questions (section, text, kind — yes/no, pass/fail/NA, text, number, choice, multi-choice, photo, signature, meter reading, date — required, help, min / max, unit, choices, minimum photos, meter, show-when condition).
  - Service level: name, response and resolution minutes per priority, hours calendar or weekly hours, time zone, statuses that pause the clock, warning percent.
  - Sub-status: name, parent status, color, pauses the service level.
- Background task "Work order service levels" (every minute) raises warnings and breaches. Workflow events: Created, Status changed, SLA warning, SLA breached, Completed.

**Already covered:**

- Existing tools, registered by Dispatch (`Dispatch/AIToolsStartup.cs`, category "Field Service"), so they only exist when Dispatch is enabled:
  - `workOrderCreate` — "Creates a work order with the customer's problem, contact and service address, and gives its number. Confirm the details with the person first." Permission `ManageWorkOrders`. Arguments `typeName`, `summary`, `description`, `priority`, `contactName`, `contactPhone`, `contactEmail`, `street`, `city`, `region`, `postalCode`, `accessNotes`.
  - `workOrderStatus` — "Gives a work order's status, when it must be answered and resolved by, and its visits with their technicians, times and ETA." Permission `ViewWorkOrders`. Argument `number`.
  - `workOrderSummarize` — "Gives a work order's details and history (notes, checklist answers, parts used, signatures and visits), so they can be summarized for a technician." Permission `ViewWorkOrders`. Argument `number`.
- Recipe step `WorkOrders` (sections `Checklists`, `ServiceLevels`, `SubStatuses`, `Types`, `Agreements`) and deployment step `WorkOrders` (`IncludeAgreements`).

**Proposed changes to existing tools:**

- Move the three work-order tools' registration from Dispatch to a WorkOrders `AIToolsStartup` (WorkOrders has none), so they work without Dispatch; `workOrderStatus` and `workOrderSummarize` then read visits only when `IDispatchService` is present.
- `workOrderCreate`: add `customerId` / `accountId` / `contactId`, `assetIds`, `earliest` / `latest`, `country`; today it can only create a walk-in style order.
- Names are noun-first (`workOrderCreate`); the brief's style is verb-first (`createWorkOrder`, `getWorkOrderStatus`, `summarizeWorkOrder`). See Open questions.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listWorkOrders` | Read | no | `ViewWorkOrders` | Lists work orders, newest first, filtered by status (or all open), type, priority, asset and a search on number or summary, with paging. Use it to find a work order's number. | FieldServiceAgent |
| `updateWorkOrder` | Write | no | `ManageWorkOrders` | Changes a work order's summary, description, priority, customer, contact, service address, access notes, assets, time window, estimated length, lines and preferred or excluded technicians. It does not change the status. | FieldServiceAgent |
| `setWorkOrderStatus` | Write | no | `ManageWorkOrders` | Moves an open work order to Ready, Scheduled, In progress or On hold, with an optional sub-status and note. Only the moves the current status allows are accepted. | FieldServiceAgent |
| `closeWorkOrder` | Destructive | yes | `ManageWorkOrders` | Cancels an open work order (a reason is required) or closes a completed one. Both are final: the work order cannot be reopened. | FieldServiceAgent |
| `addWorkOrderNote` | Write | no | `ManageWorkOrders` | Adds a note to a work order's history. | FieldServiceAgent |
| `recordWorkOrderPartUsed` | Write | no | `ManageWorkOrders` | Records the quantity used of a product, labor or expense line of a work order, taken from a stock location when Inventory is on. | FieldServiceAgent |
| `answerWorkOrderChecklist` | Write | no | `ManageWorkOrders` | Saves answers to the questions of one of a work order's checklists. Each answer must fit the question's kind and limits. | FieldServiceAgent |
| `completeWorkOrder` | External | yes | `ManageWorkOrders` | Completes a work order once its required answers, signature and photos are in, and creates the customer's invoice when the type is billable. It returns what is still missing instead when it cannot complete. | FieldServiceAgent |
| `createFollowUpWorkOrder` | Write | no | `ManageWorkOrders` | Creates a follow-up work order for the same customer, place and assets as an existing one, with a new summary, and returns its number. | FieldServiceAgent |
| `listWorkOrderSettings` | Read | no | `ManageWorkOrderSettings` | Lists the work order types, checklists, service levels or sub-statuses (choose the kind), with a name search. | FieldServiceSetupAgent |
| `getWorkOrderSetting` | Read | no | `ManageWorkOrderSettings` | Gets one work order type, checklist, service level or sub-status in full, with the JSON schema of that kind. Use it before `saveWorkOrderSetting`. | FieldServiceSetupAgent |
| `saveWorkOrderSetting` | Write | no | `ManageWorkOrderSettings` | Creates or updates a work order type, checklist, service level or sub-status. A type refers to checklists, a service level, skills, certifications and equipment types by id, so create those first. | FieldServiceSetupAgent |
| `deleteWorkOrderSetting` | Destructive | yes | `ManageWorkOrderSettings` | Deletes a work order type, checklist, service level or sub-status. Refused while work orders use the type, or while a type uses the checklist or service level. | FieldServiceSetupAgent |

**Implementation notes:**

- Services: `WorkOrderService` (`Core/CrestApps.OrchardCore.WorkOrders.Core/Services/WorkOrderService.cs`: `NewAsync`, `CreateAsync`, `UpdateAsync`, `ChangeStatusAsync`, `UsePartAsync`, `AnswerAsync`, `SignAsync`, `AttachAsync`, `AddNoteAsync`, `CompleteAsync`, `FollowUpAsync`, `FindAsync`) and `WorkOrderSettingManager<T>` for `WorkOrderType`, `ChecklistTemplate`, `ServiceLevelPolicy`, `WorkOrderSubStatus` (validated by handlers in `WorkOrderStores.cs`).
- Gap: the list query (YesSql `WorkOrderIndex` / `WorkOrderAssetIndex` with the filters) is inside `WorkOrdersController.Index`; the delete guards ("in use") are inside `WorkOrderSettingsController.Delete` (`DeleteTypeAsync`, `DeleteUnusedAsync`). Extract both.
- The work order editor is driver-based (`WorkOrderDisplayDriver`, plus `WorkOrderTechniciansDisplayDriver` from Dispatch); `updateWorkOrder` should set the model and call `WorkOrderService.UpdateAsync`, which validates.
- The four setting kinds share one controller with a `kind` route value, one permission and one generic manager, which is why one tool family with a `kind` argument fits (`types`, `checklists`, `service-levels`, `sub-statuses`).
- `completeWorkOrder` calls the billing seam (`TransactionsWorkOrderBilling.InvoiceAsync`) when Transactions is on: it creates an invoice, hence `External`.
- Not proposed: `Sign` (a signature should come from the person signing, not the assistant) and `AttachAsync` (photos come from the technician app).
- A service level's `HoursCalendarId` points at an hours calendar supplied by `OpenHoursServiceHoursProvider` (another module); unverified which screen manages those.

**Test prompts:**

- "Show me the urgent work orders that are still open."
- "Put WO-000123 on hold, waiting for parts, and add a note that the compressor is back-ordered until Friday."
- "Create a work order type 'AC tune-up': 90 minutes, one technician, the HVAC checklist, fixed price 149 dollars."
- "Cancel WO-000140, the customer sold the house."

## Service Agreements — `CrestApps.OrchardCore.WorkOrders.Agreements`

**What an admin can do in the UI:**

- Field Service → Service Agreements (`Admin/work-orders/agreements`, `ServiceAgreementsController`, `ManageServiceAgreements`): list with filters; Details (`.../view/{id}`: the agreement, its schedules with the next due dates, and its work orders); Run (`.../view/{id}/run`: make the work orders that are due now); Edit (`.../edit/{id?}`); Delete (`.../delete/{id}`: only a Draft; an active one must be cancelled).
- Fields: name, status (Draft, Active, Expired, Canceled), customer / account, contact, service address, access notes, assets, start and end date, time zone, schedules (name, work order type, repeat rule — presets weekly, every 2 weeks, monthly, every 3 or 6 months, yearly, or a custom RRULE — lead days, assets), pricing (included or per visit) and price per visit, expiring notice days, notes.
- Background task "Service agreement visits" (hourly at :05) makes work orders ahead of time and expires agreements. Workflow event: Service Agreement Expiring. An account's page shows its agreements (`AccountAgreementsDisplayDriver`).

**Already covered:** recipe step `WorkOrders` section `Agreements`. No tools.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listServiceAgreements` | Read | no | `ManageServiceAgreements` | Lists service agreements filtered by status and a search text, with paging. Use it to find an agreement's id or number. | FieldServiceSetupAgent |
| `getServiceAgreement` | Read | no | `ManageServiceAgreements` | Gets one service agreement: customer, place, assets, period, pricing, each schedule with its repeat rule and next visit dates, and the work orders already made. | FieldServiceSetupAgent |
| `saveServiceAgreement` | Write | no | `ManageServiceAgreements` | Creates or updates a service agreement: customer, contact, address, assets, start and end, schedules (work order type, repeat rule, lead days), pricing and status. Setting the status to Active starts making work orders; Canceled stops them. | FieldServiceSetupAgent |
| `runServiceAgreement` | Write | no | `ManageServiceAgreements` | Makes the work orders of an active agreement that are due now, once each, and returns how many were made. The background task does this hourly; use it to avoid waiting. | FieldServiceSetupAgent |
| `deleteServiceAgreement` | Destructive | yes | `ManageServiceAgreements` | Deletes a draft service agreement. An agreement that was ever active cannot be deleted; cancel it with `saveServiceAgreement` instead. | FieldServiceSetupAgent |

**Implementation notes:** `ServiceAgreementManager` (`CatalogManager<ServiceAgreement>`, validated by `ServiceAgreementHandler`) and `ServiceAgreementScheduler.RunAsync(agreement)` in `WorkOrders.Core/Services`. The list query, the "next due dates" preview and the Draft-only delete rule are in the controller. `runServiceAgreement` creates work orders, and with Dispatch's "create visits for new work orders" setting on, each one gets a visit.

**Test prompts:**

- "Set up a yearly furnace maintenance agreement for the Hendersons starting in October, 189 dollars per visit."
- "Which service agreements end in the next 60 days?"
- "Make the work orders that are due for agreement SA-000012 now."

## Dispatch — `CrestApps.OrchardCore.Dispatch`

**What an admin can do in the UI:**

- Field Service → Dispatch Board (`Admin/dispatch/board`, `DispatchBoardController`, `ViewDispatch`): a day's technicians (filter by team and capability) with their shifts, time off, visits and positions, the unscheduled queue and a map (`board/data`); drag a visit onto a technician and time (`board/move`, `ManageDispatch`); Optimize the day (`board/optimize`) and Apply chosen moves (`board/apply`); candidates for a visit (`board/candidates/{visitId}`).
- Field Service → Visits (`dispatch/visits`, `VisitsController`, `ViewDispatch`): list filtered by status (or open), search (visit number, work order number, summary), day and technician; shows how many work orders have no visit; Make missing visits (`dispatch/make-missing-visits`); a work order's page has "Add a visit" (`dispatch/work-orders/{id}/visits`).
- Visit details (`dispatch/visits/{id}`): find technicians for a day; Assign (`.../assign`: technicians and start); Unassign (`.../unassign`: back to unscheduled); Status (`.../status`: the next status from `VisitStatusRules` — Dispatched, En route, On site, Paused, Completed, Incomplete, Canceled — with a note); Pin / Unpin (`.../pin`: a pinned visit is left alone by the optimizer); tracking link for the customer. All changes need `ManageDispatch`.
- Reports → Field Service → KPIs (`dispatch/kpis`, `FieldServiceKpisController`, `ViewDispatch`): for a date range (capped) — finished visits, first-time fix rate, on-time arrival, travel and on-site minutes, jobs per technician day, return-visit and repeat-work rates, work orders, response and resolution compliance, mean hours to repair, revenue per work order, parts used by location.
- Get Started (`dispatch/get-started`, `FieldServiceSetupController`, `ManageDispatchSettings`): a checklist of the setup steps with their status.
- Settings → Dispatch (site settings group `dispatch`).
- Customer notices: with `NoticesEnabled`, the customer is texted or emailed when a visit is assigned (Scheduled), the day before (Reminder), and when it goes En route, Completed or Canceled (`VisitCustomerServices`). Background tasks: visit risk, visit reminders. Workflow events: Visit Assigned, Visit Status Changed, Visit At Risk.

**Already covered:**

- Existing tools (`Dispatch/AIToolsStartup.cs`, `[RequireFeatures("CrestApps.OrchardCore.AI")]`, category "Field Service"):
  - `dispatchFindTechnician` — "Lists the technicians who can do a visit on a day, best first, with the earliest start, travel and score, and the technicians ruled out with the reason (skills, certifications, territory, shifts, time off, other visits)." Permission `ViewDispatch`. Arguments `number`, `date`.
  - `dispatchOptimizeAndExplain` — "Proposes a better dispatch day: each move (visit, from whom and when, to whom and when), the travel before and after, and the visits that fit nobody. Explain the proposal; apply moves only when the dispatcher says so, by calling again with their visit numbers in apply." Permission `ManageDispatch`. Arguments `date`, `includeUnscheduled`, `apply`.
  - `dispatchRescheduleVisit` — "Moves a visit to another time, or gives it to another technician, after the dispatcher confirmed it. Refused, with the reason, when the technician isn't free, on shift or able to get there." Permission `ManageDispatch`. Arguments `number`, `technician`, `start`.
  - `dispatchWhereIsMyTechnician` — "Tells a customer the status of their visit: when the technician is expected (ETA or arrival window) and the technician's first name. Use the caller's own phone number, never one they say; or the token of their tracking link." No sign-in; customer-facing. Arguments `phone`, `trackingToken`, `workOrderNumber`.
  - `workOrderCreate`, `workOrderStatus`, `workOrderSummarize` — see Work Orders.
- `DispatchSettings` is a site-settings property with a deployment step (`AddSiteSettingsPropertyDeploymentStep<DispatchSettings>`) and a recipe schema (`DispatchSettingsSchema`), so the settings recipe path (`applySiteSettings`) can write it.

**Settings groups:** `DispatchSettings` / group `dispatch` / permission `ManageDispatchSettings` / secrets: none. Fields: `CreateVisitsForNewWorkOrders`, `ArrivalWindowMinutes` (0–720), `HorizonDays` (1–60), `BufferMinutes` (0–120), `StrictTerritory`, `TimeZoneId`, `TechnicianCapabilities`, objective weights `TravelWeight`, `UrgencyWeight`, `ProficiencyWeight`, `PreferredWeight`, `BalanceWeight` (0–10), `AtRiskMinutes` (0–240), `OptimizerSeconds` (1–120), `NoticesEnabled`, `TrackingEnabled`, `ReminderHour` (0–23), and the notice texts `ScheduledText`, `ReminderText`, `EnRouteText`, `CompletedText`, `CanceledText`. The Technician Mobile options (`CrestApps:Dispatch:Mobile`) are appsettings only.

**Proposed changes to existing tools:**

- `dispatchRescheduleVisit` and the `apply` half of `dispatchOptimizeAndExplain` assign visits, which texts or emails the customer when notices are on; both should require approval (`External`). Splitting the optimizer into a read (`proposeDispatchDay`) and an apply (`applyDispatchMoves`) would let only the second ask for approval.
- `dispatchWhereIsMyTechnician` is customer-facing and anonymous: keep it off the admin agents.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listVisits` | Read | no | `ViewDispatch` | Lists visits filtered by status (or all open), day, technician and a search on visit number, work order number or summary, with paging; also says how many work orders have no visit. | FieldServiceAgent |
| `getVisit` | Read | no | `ViewDispatch` | Gets one visit: its work order, status and history, technicians, times, arrival window, ETA, travel, equipment, whether it is pinned or at risk, the customer notices sent and the statuses it can move to next. | FieldServiceAgent |
| `createVisits` | Write | no | `ManageDispatch` | Makes a visit for one work order, or for every open work order that has none. New visits start unscheduled. | FieldServiceAgent |
| `unassignVisit` | Write | no | `ManageDispatch` | Takes a visit off its technicians and time and puts it back with the unscheduled visits. | FieldServiceAgent |
| `setVisitStatus` | External | yes | `ManageDispatch` | Moves a visit to Dispatched, En route, On site or Paused, with an optional note. En route texts or emails the customer when customer notices are on. | FieldServiceAgent |
| `closeVisit` | Destructive | yes | `ManageDispatch` | Ends a visit as Completed, Incomplete or Canceled, with a note. These are final, and Completed and Canceled notify the customer when notices are on. | FieldServiceAgent |
| `pinVisit` | Write | no | `ManageDispatch` | Pins a visit so the optimizer leaves its technician and time alone, or unpins it. | FieldServiceAgent |
| `getDispatchBoard` | Read | no | `ViewDispatch` | Gives a day's dispatch picture: each technician with their shift, time off and visits in order, and the visits waiting for a time; optionally for one team or capability. Use it to answer "what does tomorrow look like". | FieldServiceAgent |
| `getFieldServiceKpis` | Read | no | `ViewDispatch` | Gives the field-service KPIs for a date range: finished visits, first-time fix, on-time arrival, travel and on-site time, jobs per technician day, repeat work, SLA compliance, hours to repair, revenue per work order and parts used. | FieldServiceAgent |
| `getFieldServiceSetupStatus` | Read | no | `ManageDispatchSettings` | Lists the field-service setup steps (maps, territories, technicians, shifts, work order types, assets, customer notices, the Field page) with what is done and what is missing. Use it when someone asks what is left to set up. | FieldServiceSetupAgent |

**Implementation notes:**

- Services: `IDispatchService` (`FindAsync`, `ListForWorkOrderAsync`, `ListScheduledAsync`, `ListUnscheduledAsync`, `CreateAsync`, `ListWorkOrdersWithoutVisitsAsync`, `CreateMissingAsync`, `AssignAsync`, `UnassignAsync`, `ChangeStatusAsync`, `PinAsync`), `IDispatchCandidateFinder`, `IDispatchOptimizationService`, `IDispatchSettingsProvider`, `Reports.FieldServiceKpiService.GetAsync(fromUtc, toUtc, zone, repeatDays)`, `DispatchTimeZone`. The existing tools resolve a visit from its number with `AI/FieldServiceLookups.cs`; reuse it.
- Gaps: the visits list query (`ServiceVisitIndex`, `VisitWorkerIndex`) is in `VisitsController.Index`; the board's day data (worker availability, positions, queue) is in `DispatchBoardController.Data`; the setup checklist is in `FieldServiceSetupController.Index`. Each needs extracting to a service.
- The existing tools call `ISession.SaveChangesAsync` themselves after a change; new write tools must follow the same pattern.
- Customer notices are the risk: `AssignAsync` and `ChangeStatusAsync` send SMS / email through `IVisitNoticeSender` when `DispatchSettings.NoticesEnabled`.
- `getDispatchBoard` should not return technicians' live positions unless asked (personal data collected "with their consent").

**Test prompts:**

- "Which visits are still unscheduled for this week?"
- "What does Thursday look like for the Phoenix technicians?"
- "V-000231 could not be finished, the part was wrong. Mark it incomplete."
- "How was our first-time fix rate and on-time arrival last month?"

## Assets — `CrestApps.OrchardCore.Assets`, `CrestApps.OrchardCore.Assets.Fleet`

**What an admin can do in the UI:**

- Assets → Assets (`Admin/assets`, `AssetsController`, `ViewAssets`): list with search and type filter; Details (`assets/view/{id}`: the asset, its parents and children, warranty, meter readings; Record a reading — `ManageAssets`); Edit (`assets/edit/{id?}`, `ManageAssets`); Delete (`assets/delete/{id}`: refused while other assets are inside it).
- Asset fields: name, number, type, product, serial number, manufacturer, model, owner (internal, customer, account or contact), location address, parent asset, status (Active, In service, Out of service, Retired), install date, warranty (start, end, provider, terms), the type's attributes, documents. With Fleet: schedulable (gets its own Equipment calendar), plate, VIN, capacity, fuel type, home base, default driver, carries stock (gets its own Inventory location).
- Assets → Asset Types (`assets/types`, `AssetTypesController`, `ManageAssets`): list, edit (name, category — equipment, vehicle, tool, customer unit, component, facility — description, attribute definitions, meter definitions with unit and cumulative flag), delete (refused while assets use it).
- A contact's page lists its assets (`ContactAssetsDisplayDriver`); owner search endpoint (`AssetOwnerEndpoints`).

**Already covered:** recipe step `Assets` (sections `Types`, `Assets`) and deployment step (`IncludeAssets`). Asset report data sets are registered by Dispatch. No tools.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listAssets` | Read | no | `ViewAssets` | Lists assets filtered by type, owner, status and a search on name, number or serial number, with paging. | FieldServiceSetupAgent |
| `getAsset` | Read | no | `ViewAssets` | Gets one asset: type and attributes, owner, location, status, warranty and whether it covers today, the assets it sits in and those inside it, its latest meter readings and, with Fleet, its vehicle details and calendar. | FieldServiceSetupAgent |
| `saveAsset` | Write | no | `ManageAssets` | Creates or updates an asset: name, type, serial number, make and model, owner, location, parent asset, status, install date, warranty, attributes and, with Fleet, whether it can be booked, its plate, home base, driver and stock. | FieldServiceSetupAgent |
| `deleteAsset` | Destructive | yes | `ManageAssets` | Deletes an asset. Refused while other assets are inside it; set the status to Retired to keep the history instead. | FieldServiceSetupAgent |
| `recordAssetMeterReading` | Write | no | `ManageAssets` | Records a reading of one of an asset's meters (such as hours or kilometres) at a time. A cumulative meter cannot go down. | FieldServiceSetupAgent |
| `listAssetTypes` | Read | no | `ManageAssets` | Lists the asset types with their category, attribute definitions, meters and how many assets use each. | FieldServiceSetupAgent |
| `saveAssetType` | Write | no | `ManageAssets` | Creates or updates an asset type: name, category, description, the attributes its assets have and the meters they are read on. | FieldServiceSetupAgent |
| `deleteAssetType` | Destructive | yes | `ManageAssets` | Deletes an asset type. Refused while assets use it. | FieldServiceSetupAgent |

**Implementation notes:** `AssetManager` and `AssetTypeManager` (`CatalogManager`, validated by `AssetHandler` / `AssetTypeHandler`), `AssetService` (`RecordReadingAsync` returns errors, `ReadingsAsync`, `ChildrenAsync`, `AncestorsAsync`), `AssetOwnerDirectory`, `AssetTypeStore.CountByTypeAsync`. With Fleet, saving a schedulable asset creates an Equipment calendar (`FleetCalendarHandler`) and, with Inventory, a stock location (`FleetStockLocationHandler`). The list query and both delete guards are in the controllers; the UI list filters only by search and type, so owner and status filters are additions. `RecordReadingAsync` refuses a lower value on a cumulative meter ("only goes up").

**Test prompts:**

- "Add the Carrier rooftop unit at 14 Main Street for Acme Corp, serial 44X-1182, installed last June, 5-year warranty."
- "Is the Hendersons' furnace still under warranty?"
- "Van 7 is at 84,210 kilometres today."

## Time Tracker — `CrestApps.OrchardCore.TimeTracker`

**What an admin can do in the UI:**

- Time Tracker → My Time (`Admin/time/my`, `MyTimeController`, `TrackOwnTime`): current stage, today's entries, the week's totals by category; Switch stage (`time/my/switch`, clocks in when not clocked in); Clock out (`time/my/out`). When an external clock keeps the user's time (the Workforce time clock), the page points there instead.
- Time Tracker → Time Entries (`time/entries`, `TimeEntriesController`, `ViewTimeEntries`): list filtered by search, stage and day; Add / Edit (`time/entries/edit/{id?}`: user, stage, start, end, note, and a reason that is kept in the entry's change log — `ManageTimeEntries`); Delete with a reason (`time/entries/delete/{id}`).
- Time Tracker → Stages (`time/settings/stages`, `TimeSettingsController`, `ManageTimeTrackerSettings`): name, code, color, position, the categories its time counts toward, self-service, enabled. Deleting a stage turns it off (its time stays).
- Time Tracker → Categories (`time/settings/categories`): name, code, color, position, factor, enabled; delete.
- Reports → Hours by Category (`HoursByCategoryReport`, `ViewTimeEntries`) when Reports is on. Audit trail events when Audit Trail is on.

**Already covered:** recipe step `TimeTracker` (sections `Categories`, `Stages`, `Entries`) and deployment step (`IncludeEntries`). No tools.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getMyTimeStatus` | Read | no | `TrackOwnTime` | Says whether the user is clocked in, in which stage and since when, with today's entries, the week's hours by category and the stages they can switch to. | direct |
| `clockMyTime` | Write | no | `TrackOwnTime` | Clocks the user in or switches them to another stage (such as Work, Break or Lunch), or clocks them out. Refused when another clock, such as the Workforce time clock, keeps their time. | direct |
| `listTimeEntries` | Read | no | `ViewTimeEntries` | Lists anyone's time entries filtered by user, stage and a date range, with who changed each and why. | TimeAndAttendanceAgent |
| `getTimeTotals` | Read | no | `ViewTimeEntries` | Totals tracked time per person, optionally per day, by category (such as Payable or Billable) and by stage for a date range. Use it for "how many hours did X work". | TimeAndAttendanceAgent |
| `saveTimeEntry` | Write | no | `ManageTimeEntries` | Adds a time entry for a user or changes one's stage, start, end or note. A reason is required and is kept in the entry's history. | TimeAndAttendanceAgent |
| `deleteTimeEntry` | Destructive | yes | `ManageTimeEntries` | Deletes a time entry, with a reason. | TimeAndAttendanceAgent |
| `listTimeTrackerSetup` | Read | no | `ManageTimeTrackerSettings` | Lists the time stages (with the categories each counts toward) or the time categories (with their factor). | TimeAndAttendanceAgent |
| `saveTimeTrackerSetup` | Write | no | `ManageTimeTrackerSettings` | Creates or updates a time stage (name, code, color, position, categories, self-service, enabled) or a time category (name, code, color, position, factor, enabled). | TimeAndAttendanceAgent |
| `deleteTimeTrackerSetup` | Destructive | yes | `ManageTimeTrackerSettings` | Deletes a time category, or turns a time stage off (a stage is never deleted, so its recorded time still adds up). | TimeAndAttendanceAgent |

**Implementation notes:** `ITimeTrackerService` (`GetStatusAsync`, `SwitchAsync`, `ClockOutAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`, `FindAsync`, `ListAsync(TimeEntryQuery)`, `GetTotalsAsync(query, zone, byDay)`), `ITimeStageManager`, `ITimeCategoryManager`, `IExternalTimeClock` (when it returns a clock for the user, the entry is "kept by" that clock and must not be edited here — the UI shows `KeptBy`), `TimeTrackerTimeZone`. Entries edited by a manager are logged in `TimeEntry.Changes` and, with Audit Trail on, in the audit trail. The entries list search (`Q`) is done in the controller.

**Test prompts:**

- "Clock me in." / "I'm going to lunch."
- "How many payable hours did Ana track last week?"
- "Add a Training stage that counts as payable but not billable."
- "Fix Sam's entry from yesterday, he forgot to clock out at 5."

## Workforce — `CrestApps.OrchardCore.Workforce`

**What an admin can do in the UI:**

- Workforce → Activity Codes (`Admin/workforce/activity-codes`, `ActivityCodesController`, `ManageActivityCodes`): list, edit, delete.
- Workforce → Get Started (`workforce/get-started`, `WorkforceSetupController`, `ManageWorkforceTeams`): the setup checklist.
- Workforce adds cards to editors that belong to other modules: on a Team (Teams module) — location (address, latitude, longitude), lead worker, open-shift claim policy, whether trades need approval, how time off treats shifts, time clock policy (`WorkforceTeamDisplayDriver`, `WorkforceTeamSettings`) and the list of members (`WorkforceTeamMembersDisplayDriver`); on a Staff member (Staff module) — minimum and maximum hours per week, maximum hours per day and weekly availability windows (`WorkforceStaffDisplayDriver`, `WorkforceStaffSettings`).
- Each team gets a `Workforce` calendar managed by code (`WorkforceCalendarService`).
- Background task "Shift reminders". Report data source `Workforce`.

**Already covered:**

- Existing tools (`Workforce/AIToolsStartup.cs`, three startups all with `[RequireFeatures("CrestApps.OrchardCore.AI")]`, category "Workforce", base `WorkforceAIToolBase` which requires a signed-in user):
  - Base feature: `workforceWhoIsWorking` — "Lists who is on shift at a time in the teams the user manages, with each person's team, shift times and whether they are clocked in." Arguments `at`, `teamName`. `workforceMySchedule` — "Lists the user's own published shifts, with their team and local start and end times." Arguments `from`, `days` (1–28).
  - Shift Planning feature: `workforceFindCover` — "Finds the colleagues in the same team who can cover a worker's shift on a day, in the teams the user manages, and why the others can't. It doesn't change the schedule." Arguments `workerName`, `date`. `workforceExplainViolations` — "Checks a team's draft schedule against its labor rules and lists each problem with the rule, the worker, the day and whether it warns, blocks or needs an override." Arguments `teamName`, `date`.
  - Time Off feature: `workforceTimeOffBalance` — "Gives the user's own time-off balances: what they have, what is booked, what waits for approval and what is available, per type." No arguments. `workforceRequestTimeOff` — "Asks for time off for the user. Confirm the type and the days with the user first. The request follows the same rules and approvals as one made in My Work." Permission `RequestTimeOff`. Arguments `typeName`, `startDate`, `endDate`, `startTime`, `endTime`, `reason`.
- Recipe steps: `Workforce` (sections `ActivityCodes`, `Teams`, `Workers`), `WorkforceShiftPlanning` (`Templates`, `Patterns`, `CoverageRequirements`), `WorkforceTimeOff` (`Types`, `Policies`, `Allotments`, `BlackoutPeriods`), `WorkforceTimeClock` (`PayPeriods`, `PayCodes`, `Policies`, `Kiosks`, `PayrollCsv`), `WorkforceLaborRules` (`RuleSets`). They refer to teams, types and activity codes by name. Deployment steps `Workforce` (`IncludeWorkers`) and `WorkforceSettings` (exports every enabled feature's setup).
- Teams and staff members themselves belong to the Teams and Staff modules (another group).

One tool family covers every setup catalog of Workforce and Workforce.ContactCenter, because they are all the same shape (a catalog with list / edit / delete screens and a recipe section). The `kind` argument selects the catalog and each kind checks its own permission:

| `kind` | Record | Feature | Permission |
| --- | --- | --- | --- |
| `activityCode` | `ActivityCode` | Workforce | `ManageActivityCodes` |
| `shiftTemplate` | `ShiftTemplate` | Shift Planning | `ManageShiftTemplates` |
| `rotation` | `ShiftPattern` | Shift Planning | `ManageWorkforceSchedules` (`WorkforcePermissions.ManageSchedules`) |
| `coverageRequirement` | `CoverageRequirement` | Shift Planning | `ManageWorkforceSchedules` |
| `timeOffType` | `TimeOffType` | Time Off | `ManageTimeOffPolicies` |
| `timeOffPolicy` | `TimeOffPolicy` | Time Off | `ManageTimeOffPolicies` |
| `timeOffAllotment` | `TimeOffAllotment` | Time Off | `ManageTimeOffPolicies` |
| `blackoutPeriod` | `BlackoutPeriod` | Time Off | `ManageTimeOffPolicies` |
| `timeClockPolicy` | `TimeClockPolicy` | Time Clock | `ManageTimeClockPolicies` |
| `payPeriodCalendar` | `PayPeriodCalendar` | Time Clock | `ManageTimeClockPolicies` |
| `payCode` | `PayCode` | Time Clock | `ManageTimeClockPolicies` |
| `timeClockKiosk` | `TimeClockKiosk` | Time Clock | `ManageTimeClockPolicies` |
| `payrollCsvColumns` | `PayrollCsvSettings` (one record) | Time Clock | `ExportPayroll` |
| `laborRuleSet` | `LaborRuleSet` | Labor Rules | `ManageLaborRules` |
| `planningGroup` | `PlanningGroup` | Forecasting | `ManageWorkforceForecasts` (`ManageForecasts`) |
| `forecastEvent` | `ForecastEvent` | Forecasting | `ManageWorkforceForecasts` |
| `capacityPlan` | `CapacityPlan` | Forecasting | `ManageWorkforceForecasts` |
| `adherenceRuleSet` | `AdherenceRuleSet` | Adherence | `ManageWorkforceAdherence` (`ManageAdherence`) |

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listWorkforceSetup` | Read | no | The permission of the chosen kind (table above) | Lists the records of one kind of workforce setup (activity codes, shift templates, rotations, coverage requirements, time-off types, policies, allotments and blackout dates, time clock policies, pay periods, pay codes, kiosks, labor rule sets, planning groups, forecast events, capacity plans, adherence rule sets), with a name search. Only kinds of enabled features are accepted. | WorkforceSetupAgent |
| `getWorkforceSetup` | Read | no | The permission of the chosen kind | Gets one setup record in full, with the JSON schema of its kind and the names behind its ids (teams, activity codes, types). Use it before `saveWorkforceSetup`. | WorkforceSetupAgent |
| `saveWorkforceSetup` | Write | no | The permission of the chosen kind | Creates or updates one setup record of a kind. Teams, activity codes, time-off types and skills may be given by name. The record is checked by the same rules as the admin screen and the errors are returned. | WorkforceSetupAgent |
| `deleteWorkforceSetup` | Destructive | yes | The permission of the chosen kind | Deletes one setup record of a kind, exactly as the admin screen's Delete does. Most kinds have no in-use check, so turn the record off (`enabled: false`) instead when it may still be referenced. | WorkforceSetupAgent |
| `saveWorkforceTeamSettings` | Write | no | `ManageWorkforceTeams` (`WorkforcePermissions.ManageTeams`) | Sets a team's workforce settings: location, lead worker, how open shifts are claimed, whether shift trades need approval, what happens to shifts during time off, and its time clock policy. | WorkforceSetupAgent |
| `saveWorkerWorkLimits` | Write | no | `ManageWorkers` and the worker is in the user's scope | Sets a worker's minimum and maximum hours per week, maximum hours per day and the weekly times they are available. The auto-scheduler and the cover search respect them. | WorkforceSetupAgent |
| `getWorkforceSetupStatus` | Read | no | `ManageWorkforceTeams` | Lists the workforce setup steps with what is done and what is missing, for the features that are on. Use it when someone asks what is left to set up. | WorkforceSetupAgent |

**Implementation notes:**

- Managers: `IActivityCodeManager`, `IShiftTemplateManager`, `IShiftPatternManager`, `ICoverageService` (`ListRequirementsAsync`, `SaveRequirementAsync`, `DeleteRequirementAsync`), `ITimeOffTypeManager`, `ITimeOffPolicyManager`, `ITimeOffAllotmentManager`, `IBlackoutPeriodManager`, `ITimeClockPolicyManager`, `IPayPeriodCalendarManager`, `IPayCodeManager`, `ITimeClockKioskManager`, `IPayrollExportService.GetCsvSettingsAsync` / `SaveCsvSettingsAsync`, `ILaborRuleSetManager`; in Workforce.ContactCenter `PlanningGroupManager`, `ForecastEventManager`, `CapacityPlanManager` and the adherence rule set manager in `Services/AdherenceStores.cs`. Validation is in `Workforce.Core/Handlers/*CatalogHandler.cs`.
- The controllers also parse friendlier input than the stored model (shift template segments as text lines, working times as `HH:mm`); `saveWorkforceSetup` should accept the same shape the recipe steps accept, and can reuse `Workforce.Core/Recipes/RecipeImport.cs`, which already resolves names to ids. Make the kinds pluggable (a small `IWorkforceSetupKind` per catalog) so Workforce.ContactCenter can add its four kinds.
- The team and worker cards are stored as extension data on `Team` and `StaffMember` (`team.Put(WorkforceTeamSettings)`); if the Teams / Staff group's own `saveTeam` / `saveStaffMember` tools accept extension properties, drop `saveWorkforceTeamSettings` and `saveWorkerWorkLimits`.
- The setup checklist is computed in `WorkforceSetupController.Index` (view model `WorkforceSetupViewModel` in Workforce.Core); extract it.
- A time clock kiosk has a secret address token; `getWorkforceSetup` must not return it (see `renewTimeClockKioskAddress` under Time Clock).

**Test prompts:**

- "List our shift templates."
- "Add a 'Late' shift template from 14:00 to 22:00 with a 30-minute meal at 18:00 for the Warehouse team."
- "Create a Vacation policy: 15 days a year, accrued monthly, for the Support team."
- "What is left to set up in Workforce?"

## Workforce Shift Planning — `CrestApps.OrchardCore.Workforce.ShiftPlanning`

**What an admin can do in the UI:**

- Calendars → Shift Schedules (`Admin/workforce/schedules`, `SchedulesController`, `ManageWorkforceSchedules`): list by team; Create a period (team, start date, weeks, name); Plan board (`schedules/plan/{id}`: the draft week by worker, coverage against requirements, labor rule problems); add / edit a draft shift (`schedules/shift/{periodId}/{id?}`: worker or open, date, start, end, template, breaks and activities, notes) and delete it; Apply rotations; Auto-schedule; Copy from another period; Publish (`PublishWorkforceSchedules`; with an override reason it also needs `OverrideLaborRules`); Changes log (`schedules/changes/{id}`); Delete (a published schedule cannot be deleted).
- Published shift (`schedules/published-shift/{periodId}/{id}`): change times, reassign to another worker, or remove, with a reason and whether the worker asked for it (decides predictability pay).
- Calendars → Open Shifts (`workforce/open-shifts`, `OpenShiftsController`, `ManageWorkforceOpenShifts`): list by team and status; Post one (team, date, times, template, required skills, notes, claim policy, claim deadline); Details with eligible workers; Assign to a worker; Claim for a worker; Decline a claim with a note; Cancel.
- Calendars → Shift Trades (`workforce/shift-trades`, `ShiftTradesController`, `ApproveWorkforceShiftTrades`): list; Create an offer for a worker (give away or swap); Details; Accept / Reject for the other worker; Approve; Decline with a note; Cancel.
- Calendars → Rotations, Coverage, Shift Templates: setup catalogs (kinds `rotation`, `coverageRequirement`, `shiftTemplate` above).
- Background task "Open shifts awarded by seniority". Workflow events: Schedule Published, Shift Assigned / Changed / Removed, Open Shift Posted.

**Already covered:** `workforceFindCover`, `workforceExplainViolations` (above); `workforceWhoIsWorking`, `workforceMySchedule`; recipe step `WorkforceShiftPlanning`. The plan document names a `workforce_draft_schedule` tool that was never built (`docs/engineering/workforce/workforce-management-plan.md`, §18.3 and the A9 notes).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listShiftSchedules` | Read | no | `ManageWorkforceSchedules` and team in scope | Lists the schedule periods of the teams the user manages, with team, dates, name and whether each is a draft or published. | WorkforceSchedulingAgent |
| `getShiftSchedule` | Read | no | `ManageWorkforceSchedules` and team in scope | Gets a schedule period: its draft shifts by worker and day, the shifts nobody has, where coverage is short or over, the labor rule problems and, when asked, the log of changes made after publishing. | WorkforceSchedulingAgent |
| `createShiftSchedule` | Write | no | `ManageWorkforceSchedules` and team in scope | Starts a draft schedule for a team from a start date for a number of weeks. Nothing is visible to workers until it is published. | WorkforceSchedulingAgent |
| `saveDraftShift` | Write | no | `ManageWorkforceSchedules` and team in scope | Adds or changes a shift in a draft schedule: the worker (or nobody, for an open shift), day, start and end, template, breaks and notes. Returns the labor rule problems the shift causes. | WorkforceSchedulingAgent |
| `deleteDraftShift` | Destructive | yes | `ManageWorkforceSchedules` and team in scope | Removes a shift from a draft schedule. A published shift is removed with `changePublishedShift` instead. | WorkforceSchedulingAgent |
| `fillShiftSchedule` | Write | no | `ManageWorkforceSchedules` and team in scope | Fills a draft schedule in one of three ways: apply the team's rotations, copy the shifts of another period, or run the auto-scheduler against the coverage requirements. Returns how many shifts were added and what could not be covered. | WorkforceSchedulingAgent |
| `publishShiftSchedule` | External | yes | `PublishWorkforceSchedules` (`WorkforcePermissions.PublishSchedules`); with an override reason also `OverrideLaborRules` | Publishes a draft schedule: shifts go on the team calendar, workers are notified, and shifts nobody has are offered as open shifts. Blocked by labor rule problems unless an override reason is given; run `workforceExplainViolations` first. | WorkforceSchedulingAgent |
| `changePublishedShift` | External | yes | `ManageWorkforceSchedules` and team in scope | Changes a published shift: new times, another worker, or removal, with a reason and whether the worker asked for it. The worker is notified and a late change can record predictability pay. | WorkforceSchedulingAgent |
| `deleteShiftSchedule` | Destructive | yes | `ManageWorkforceSchedules` and team in scope | Deletes a draft schedule and its draft shifts. A published schedule cannot be deleted. | WorkforceSchedulingAgent |
| `listOpenShifts` | Read | no | `ManageWorkforceOpenShifts` (`ManageOpenShifts`) and team in scope | Lists open shifts filtered by team, status and date, each with its claims and, when asked, the workers who are eligible to take it. | WorkforceSchedulingAgent |
| `postOpenShift` | External | yes | `ManageWorkforceOpenShifts` and team in scope | Posts a shift nobody has, for a team, day and time, with the skills needed, how it is claimed (first come, approval or seniority) and a deadline. Eligible workers are notified. | WorkforceSchedulingAgent |
| `decideOpenShiftClaim` | External | yes | `ManageWorkforceOpenShifts` and team in scope | Gives an open shift to a worker (one who claimed it or any eligible worker), or declines a worker's claim with a note. The worker is notified. | WorkforceSchedulingAgent |
| `cancelOpenShift` | Destructive | yes | `ManageWorkforceOpenShifts` and team in scope | Cancels an open shift so it can no longer be claimed. | WorkforceSchedulingAgent |
| `listShiftTrades` | Read | no | `ApproveWorkforceShiftTrades` (`ApproveShiftTrades`) and team in scope | Lists shift trades and giveaways filtered by team, worker and status, with the shifts involved and any rule problems. | WorkforceSchedulingAgent |
| `createShiftTrade` | External | yes | `ApproveWorkforceShiftTrades` and team in scope | Offers one worker's shift to another worker, as a giveaway or a swap for one of theirs, on the worker's behalf. The other worker is asked to accept. | WorkforceSchedulingAgent |
| `decideShiftTrade` | External | yes | `ApproveWorkforceShiftTrades` and team in scope | Approves, declines (with a note) or cancels a shift trade, or accepts or rejects it for the other worker. Approving swaps the shifts on the published schedule and notifies both workers. | WorkforceSchedulingAgent |

**Implementation notes:**

- Services: `ISchedulePlanningService` (`CreatePeriodAsync`, `ListPeriodsAsync`, `FindPeriodAsync`, `DeletePeriodAsync`, `ListDraftShiftsAsync`, `SaveDraftShiftAsync`, `DeleteDraftShiftAsync`, `ApplyPatternsAsync`, `CopyFromPeriodAsync`, `AutoScheduleAsync(scenarioId, AutoScheduleOptions)`, `CheckPublishAsync`, `PublishAsync(periodId, actor, overrideReason)`, `ListChangesAsync`), `IShiftService` (`ChangeTimesAsync`, `ReassignAsync`, `RemoveAsync`, `SwapAsync`, `CheckAsync`, `ListWorkerShiftsAsync`), `ICoverageService.ComputeAsync(CoverageQuery)`, `IOpenShiftService` (`PostAsync`, `PageAsync(OpenShiftQuery)`, `ListEligibleWorkersAsync`, `ClaimAsync`, `AssignAsync`, `DeclineClaimAsync`, `CancelAsync`), `IShiftTradeService` (`OfferAsync`, `PageAsync(ShiftTradeQuery)`, `AcceptAsync`, `RejectAsync`, `ApproveAsync`, `DeclineAsync`, `CancelAsync`).
- Scope: `SchedulesController.AuthorizePeriodAsync` combines the permission with the team scope; copy it. Team names resolve as in the existing tools (`WorkforceNames.Normalize`).
- `publishShiftSchedule` has the widest effect in the group: calendar events for every worker, notifications (inbox, web push, email), open shifts, labor-rule override records and possibly predictability pay. `PublishAsync` returns `NeedsOverride` and the violations instead of publishing when rules block.
- `fillShiftSchedule` with the auto-scheduler can run for a while (`AutoScheduleOptions.TimeLimitSeconds`); cap it and return a summary, not every shift.
- The published-shift lookup (`FindPublishedAsync`) and the local-time conversion for "times" are in the controller.

**Test prompts:**

- "Start next month's schedule for the Warehouse team, copy last month's shifts, then tell me where we are short."
- "Is the Support draft for the week of the 14th safe to publish?"
- "Sam called in sick for tomorrow's 9 to 5. Who can cover, and give it to the first one who is free."
- "Post an open shift for Saturday 8 to 4 in Phoenix, first come first served."

## Workforce Labor Rules — `CrestApps.OrchardCore.Workforce.LaborRules`

**What an admin can do in the UI:**

- Workforce → Labor Rules (`Admin/workforce/labor-rules`, `LaborRuleSetsController`, `ManageLaborRules`): list, edit a rule set (name, description, jurisdiction, enabled, the teams, workers and employment types it applies to, and rule instances from the catalog — breaks, days off per week, max consecutive days, max hours per day / week, max and min shift length, min hours per week, min rest, minor limits, no work during time off, overtime, predictability pay, schedule notice, skills required — each with parameters and an enforcement of warn, block or require override), delete. Setup kind `laborRuleSet`.
- Workforce → Rule Overrides (`workforce/rule-overrides`, `LaborRuleOverridesController`, `ManageLaborRules`): a read-only log of every override with who, when, why and which rule.
- Workforce → Time Management → Predictability Pay (`workforce/predictability-pay`, `PredictabilityPayController`, `ApprovePredictabilityPay`): list by team, worker and status; Approve; Waive with a reason.

**Already covered:** `workforceExplainViolations`; recipe step `WorkforceLaborRules` (`RuleSets`). The rule catalog itself is code (`ILaborRule` implementations).

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listLaborRuleOverrides` | Read | no | `ManageLaborRules` and team in scope | Lists the labor rule overrides that were recorded: the rule, the worker and day, who overrode it, when and the reason. | WorkforceSchedulingAgent |
| `listPredictabilityPay` | Read | no | `ApprovePredictabilityPay` and team in scope | Lists the predictability pay entries raised by late schedule changes, filtered by team, worker and status, with the change that caused each and the amount. | WorkforceSchedulingAgent |
| `decidePredictabilityPay` | External | yes | `ApprovePredictabilityPay` and team in scope | Approves a predictability pay entry so it is paid with the worker's timesheet, or waives it with a reason. | WorkforceSchedulingAgent |

**Implementation notes:** `ILaborRuleOverrideService.PageAsync(teamIds, skip, take)`, `IPredictabilityPayService` (`PageAsync(PredictabilityPayQuery)`, `ApproveAsync`, `WaiveAsync`), `ILaborRuleEvaluator`. `getWorkforceSetup` for `laborRuleSet` should also return the rule catalog with each rule's parameters (`LaborRuleParameter`, `LaborRuleParameterKind`), otherwise the model cannot know which rule keys exist (`LaborRuleKeys`).

**Test prompts:**

- "Which labor rules did we override this month, and why?"
- "Add a rule set for the California teams: 10 hours rest between shifts (block) and no more than 6 days in a row (needs override)."
- "Approve the predictability pay for Ana's cancelled shift."

## Workforce Time Off — `CrestApps.OrchardCore.Workforce.TimeOff`

**What an admin can do in the UI:**

- Workforce → Time Management → Time-Off Requests (`Admin/workforce/time-off`, `TimeOffController`, `ApproveTimeOff`): queue filtered by team, worker, status and dates; bulk Approve / Decline; New request for a worker (`time-off/new`, with a "check" preview of the days, the balance and the conflicts); Details (`time-off/requests/{id}`); Approve / Decline / Cancel with a note.
- A worker's time off (`time-off/workers/{workerId}`): balances per policy, the ledger, the assigned policies; Adjust a balance (`.../adjust`, `AdjustTimeOffBalances`: policy, kind, amount, effective date, expiry, note); Assign a policy from a date and Unassign (`.../assign`, `.../unassign`, `ManageTimeOffPolicies`).
- Workforce → Time Management → Time-Off Types, Time-Off Policies, Time-Off Allotments, Blackout Dates: setup catalogs (kinds `timeOffType`, `timeOffPolicy`, `timeOffAllotment`, `blackoutPeriod`).
- Background tasks "Time off accrual" and "Time off to Time Tracker". Workflow events: Time Off Requested / Approved / Declined / Canceled / Waitlist Promoted. Approved time off goes on the team calendar and, by the team's setting, removes or keeps the shifts.

**Already covered:** `workforceTimeOffBalance` and `workforceRequestTimeOff` (both for the user's own time off); recipe step `WorkforceTimeOff`.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listTimeOffRequests` | Read | no | `ApproveTimeOff` and team in scope | Lists time-off requests of the teams the user manages, filtered by team, worker, status and dates, with the days asked for and the balance left. Use it for "what is waiting for me". | TimeOffAgent |
| `submitTimeOffRequest` | External | yes | `ApproveTimeOff` and the worker is in scope | Checks or submits a time-off request on a worker's behalf: type, days or part of a day, and reason. With `checkOnly` it only previews the days counted, the balance and the conflicts (blackout dates, allotments, shifts) and changes nothing. | TimeOffAgent |
| `decideTimeOffRequest` | External | yes | `ApproveTimeOff` and the worker is in scope | Approves, declines or cancels a time-off request, with a note. Approving books the days, puts them on the team calendar and handles the worker's shifts by the team's rule; the worker is notified. | TimeOffAgent |
| `getWorkerTimeOff` | Read | no | `ApproveTimeOff` and the worker is in scope | Gets a worker's time-off picture: the policies assigned, the balance of each (earned, booked, pending, available) and the recent ledger entries. | TimeOffAgent |
| `adjustTimeOffBalance` | External | yes | `AdjustTimeOffBalances` and the worker is in scope | Adds to or takes from a worker's time-off balance for a policy on a date, with a note and an optional expiry. It is written to the ledger and cannot be edited afterwards, only offset. | TimeOffAgent |
| `assignTimeOffPolicy` | Write | no | `ManageTimeOffPolicies` | Assigns a time-off policy to a worker from a date, or takes it away. A worker without a policy for a type cannot ask for that type. | TimeOffAgent |

**Implementation notes:** `ITimeOffRequestService` (`PreviewAsync`, `SubmitAsync`, `ApproveAsync`, `DeclineAsync`, `CancelAsync`, `FindAsync`, `PageAsync(TimeOffRequestQuery)`), `ITimeOffBalanceService` (`GetBalancesAsync`, `PageLedgerAsync`, `AdjustAsync(TimeOffAdjustment)` — it has an `IdempotencyKey`; set it so a retried tool call does not adjust twice), `ITimeOffPolicyManager`, `ITimeOffTypeManager`. The assign / unassign logic (a `TimeOffPolicyAssignment` on the policy) is in `TimeOffController.Assign` / `Unassign`; extract it. Bulk approve / decline is a loop over the same service calls; let `decideTimeOffRequest` take several ids. `submitTimeOffRequest` with `checkOnly` is a read; if a tool may not be both, split it into `previewTimeOffRequest` (Read) and `submitTimeOffRequest`.

**Test prompts:**

- "What time-off requests are waiting for my approval?"
- "Approve Lena's vacation in August and decline Mark's for the same week, we can only spare one."
- "How many vacation days does Ana have left?"
- "Give Tom 2 extra personal days for working the holiday."

## Workforce Time Clock — `CrestApps.OrchardCore.Workforce.TimeClock`

**What an admin can do in the UI:**

- Workforce → Time Management → Timesheets (`Admin/workforce/timesheets`, `TimesheetsController`, `ApproveTimesheets`): list by pay period, team, worker and status; bulk Approve; Prepare the period's timesheets for the teams (`timesheets/prepare`); Lock a period's approved timesheets (`timesheets/lock`, `ExportPayroll`; a locked timesheet cannot be reopened); Details (`timesheets/{id}`: days, punches, calculated hours by pay code, exceptions, corrections); Decide (`.../decide`: Approve, Reject with a reason, Reopen); add / correct / remove a punch with a reason (`.../punches/*`, `EditPunches`); decide a worker's punch correction request (`.../corrections`); add / remove a manual line (pay code, hours, note — `.../lines/*`); a worker's timesheet for a date (`timesheets/worker/{workerId}`).
- Workforce → Time Management → Attendance (`workforce/attendance`, `AttendanceController`, `ApproveTimesheets`): exceptions (late, early out, absent, missed punch…) filtered by team, worker, status, kind and dates; bulk Confirm / Excuse; Decide one (`attendance/{id}/decide`: Confirm or Excuse with a note). Confirmed exceptions add attendance points.
- Workforce → Time Management → Payroll Export (`workforce/payroll-export`, `PayrollExportController`, `ExportPayroll`): past batches; Run (provider, pay period calendar, period start, include already exported); Download a batch; Columns (the CSV columns, with reset).
- Workforce → Time Clock Policies, Pay Periods, Pay Codes, Time Clock Kiosks: setup catalogs (kinds `timeClockPolicy`, `payPeriodCalendar`, `payCode`, `timeClockKiosk`); a kiosk also has "address" (`time-clock-kiosks/address/{id}`: show and renew its secret kiosk address).
- A worker's time clock page (`workforce/workers/{workerId}/time-clock`, `WorkerTimeClockController`, `ManageWorkers`): set or remove the kiosk PIN; pay rates by date and activity code — add, remove (`ViewPayRates`).
- Kiosk pages (`TimeClockKioskController`, public by token): identify by badge and PIN, punch.
- Background tasks "Timesheet refresh" and "Time clock to Time Tracker". Workflow events: Punched In / Out, Attendance Exception Raised, Attendance Points Threshold Reached, Timesheet Submitted / Approved.

**Already covered:** recipe step `WorkforceTimeClock`. `workforceWhoIsWorking` shows who is clocked in. No time clock tools.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listTimesheets` | Read | no | `ApproveTimesheets` and team in scope | Lists timesheets filtered by pay period, team, worker and status, with each one's total hours, overtime and open exceptions. | TimeAndAttendanceAgent |
| `getTimesheet` | Read | no | `ApproveTimesheets` and the worker is in scope | Gets one timesheet (by id, or a worker and a date): each day's punches and hours, the totals by pay code, manual lines, attendance exceptions and pending punch corrections. | TimeAndAttendanceAgent |
| `prepareTimesheets` | Write | no | `ApproveTimesheets` and team in scope | Creates the timesheets of a pay period for the workers of a team (and its sub-teams) who do not have one yet, so the period can be reviewed. | TimeAndAttendanceAgent |
| `decideTimesheet` | External | yes | `ApproveTimesheets` and the worker is in scope | Approves one or more timesheets, sends one back to the worker with a reason, or reopens an approved one. The worker is notified. | TimeAndAttendanceAgent |
| `lockPayPeriod` | Destructive | yes | `ExportPayroll` | Locks every approved timesheet of a pay period, ready for payroll. A locked timesheet cannot be reopened or changed. | TimeAndAttendanceAgent |
| `saveTimesheetPunch` | External | yes | `EditPunches`, `ApproveTimesheets` and the worker is in scope | Adds a missing punch to a worker's timesheet or corrects the kind or time of one, with a reason that is kept. It changes the hours the worker is paid for. | TimeAndAttendanceAgent |
| `removeTimesheetPunch` | Destructive | yes | `EditPunches`, `ApproveTimesheets` and the worker is in scope | Removes a punch from a worker's timesheet, with a reason that is kept. | TimeAndAttendanceAgent |
| `addTimesheetLine` | External | yes | `ApproveTimesheets` and the worker is in scope | Adds a manual line to a timesheet: a pay code (such as a bonus or an allowance), hours on a date and a note. | TimeAndAttendanceAgent |
| `removeTimesheetLine` | Destructive | yes | `ApproveTimesheets` and the worker is in scope | Removes a manual line from a timesheet. | TimeAndAttendanceAgent |
| `decidePunchCorrection` | Write | no | `ApproveTimesheets` and the worker is in scope | Approves or declines a worker's request to add or correct a punch. Approving applies it to the timesheet. | TimeAndAttendanceAgent |
| `listAttendanceExceptions` | Read | no | `ApproveTimesheets` and team in scope | Lists attendance exceptions (late, left early, absent, missed punch) filtered by team, worker, kind, status and dates, with the worker's current attendance points. | TimeAndAttendanceAgent |
| `decideAttendanceException` | Write | no | `ApproveTimesheets` and the worker is in scope | Confirms one or more attendance exceptions (which adds their points) or excuses them with a note. | TimeAndAttendanceAgent |
| `listPayrollExports` | Read | no | `ExportPayroll` | Lists the payroll export batches that were run, with the pay period, provider, who ran it, how many timesheets it holds and its download link. | TimeAndAttendanceAgent |
| `runPayrollExport` | External | yes | `ExportPayroll` | Exports a pay period's locked timesheets to a payroll file with the chosen provider and returns the batch and its download link. Timesheets already exported are left out unless asked for. | TimeAndAttendanceAgent |
| `getWorkerTimeClock` | Read | no | `ManageWorkers` and the worker is in scope; pay rates only with `ViewPayRates` | Gets a worker's time clock setup: the policy that applies, whether they have a kiosk PIN and, when the user may see them, their pay rates by date and activity. | WorkforceSetupAgent |
| `setWorkerTimeClockPin` | Security | yes | `ManageWorkers` and the worker is in scope | Sets or removes the PIN a worker uses at a time clock kiosk. The PIN is never read back, so tell the worker what it is. | WorkforceSetupAgent |
| `saveWorkerPayRate` | External | yes | `ManageWorkers`, `ViewPayRates` and the worker is in scope | Adds a pay rate for a worker from a date, for all work or for one activity code. It decides what payroll export pays from that date. | WorkforceSetupAgent |
| `removeWorkerPayRate` | Destructive | yes | `ManageWorkers`, `ViewPayRates` and the worker is in scope | Removes a worker's pay rate that starts on a given date for a given activity code. | WorkforceSetupAgent |
| `renewTimeClockKioskAddress` | Security | yes | `ManageTimeClockPolicies` | Gives a time clock kiosk a new secret address and returns it. The old address stops working at once, so the device must be opened with the new one. | WorkforceSetupAgent |

**Implementation notes:**

- Services: `ITimesheetService` (`GetForDateAsync`, `FindAsync`, `CalculateAsync`, `PageAsync(TimesheetQuery)`, `SubmitAsync`, `ApproveAsync`, `RejectAsync`, `ReopenAsync`, `LockPeriodAsync`, `AddLineAsync`, `RemoveLineAsync`), `ITimeClockService` (`PunchAsync`, `CorrectAsync`, `RemoveAsync`, `ListAsync`, `GetPolicyAsync`, `GetStatusAsync`, `SetPinAsync`, `RenewKioskTokenAsync`), `IAttendanceService` (`PageAsync(AttendanceQuery)`, `ExcuseAsync`, `ConfirmAsync`, `GetPointsAsync`, `DecideCorrectionAsync`, `ListPendingCorrectionsAsync`), `IPayrollExportService` (`RunAsync(provider, calendarId, periodStart, includeExported, actor)`, `PageAsync`, `FindAsync`), `IPayrollExportProvider` (the provider list).
- Gaps in controllers: `TimesheetsController.Prepare` (the loop over teams and workers), `ForPunchesAsync` / `CanManageAsync` (permission plus scope), the conversion of a date and time to UTC in the worker's zone for punches; pay-rate storage in `WorkerTimeClockController.AddRate` / `RemoveRate` (where the rates live on the staff member is unverified).
- Secrets: the kiosk PIN (`ITimeClockService.SetPinAsync`, which delegates to a badge service; how the PIN is stored is unverified), the kiosk token URL, and pay rates (guarded by `ViewPayRates`; `ViewSensitiveWorkerData` exists for other fields). None may be echoed into logs.
- `runPayrollExport` produces a file; return the batch id and the admin download route (`Admin/workforce/payroll-export/{id}/download`), not the content.
- `lockPayPeriod`: `DefaultTimesheetService.ReopenAsync` refuses a locked timesheet, so locking cannot be undone.

**Test prompts:**

- "Which timesheets for last week still wait for approval? Approve the ones without exceptions."
- "Sam forgot to clock out on Tuesday, he left at 5:30."
- "Who was late more than twice this month?"
- "Lock the period that ended on the 15th and run the payroll export."

## Workforce for Contact Center — `CrestApps.OrchardCore.Workforce.ContactCenter`, `CrestApps.OrchardCore.Workforce.ContactCenter.TimeTracker`

**What an admin can do in the UI:**

- Workforce → Contact Center Agents (`Admin/workforce/agents`, `AgentsController`, `ManageWorkers`): the contact center agents with the worker profile of each; Create workers for the agents that have none, optionally into a team (`agents/create-workers`). New agents get a worker automatically (`AgentWorkerHandler`).
- Time Tracker → Contact Center Stages (`Admin/time/contact-center-stages`, `ContactCenterTimeStagesController`, `ManageTimeTrackerSettings`, feature `…ContactCenter.TimeTracker`): map each agent state and each not-ready reason to a Time Tracker stage.
- `…ContactCenter.SignInPunches` has no screen: it punches agents in and out as they sign in and out (`SignInPunchHandler`).

**Already covered:** nothing here (the two existing tools belong to Forecasting and Adherence below).

**Settings groups:** `ContactCenterTimeStages` is stored on the site settings document (`site.GetOrCreate<ContactCenterTimeStages>()`) but is edited by its own controller, with no settings group id and no `SiteDisplayDriver`. The generic `getSiteSettings(group)` trio will not find it. Permission `ManageTimeTrackerSettings`; no secrets.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `createWorkersForAgents` | Write | no | `ManageWorkers` | Creates a worker profile for every contact center agent that has none, optionally in a team, and returns how many were created. Use it after turning Workforce on for a contact center that already has agents. | WorkforceSetupAgent |
| `getContactCenterTimeStages` | Read | no | `ManageTimeTrackerSettings` | Gets which Time Tracker stage each contact center agent state (available, on a call, wrap-up…) and each not-ready reason is recorded in. | TimeAndAttendanceAgent |
| `saveContactCenterTimeStages` | Write | no | `ManageTimeTrackerSettings` | Sets which Time Tracker stage each agent state and each not-ready reason is recorded in. Only existing stage ids are accepted. | TimeAndAttendanceAgent |

**Implementation notes:** `AgentWorkerService.CreateMissingAsync(teamId)` and `ListAsync()`. The stage mapping is read with `ContactCenterTimeTracker.BuildAsync(settings)` and written by `ContactCenterTimeStagesController.IndexPost` (`Apply` drops unknown stage ids). If the generic settings tools gain a way to register a settings object without a group, the two stage tools can go.

**Test prompts:**

- "We just enabled Workforce. Give every contact center agent a worker profile in the Support team."
- "Record 'Not ready – Coaching' as Training time in Time Tracker."

## Workforce Forecasting and Intraday — `CrestApps.OrchardCore.Workforce.Forecasting`, `CrestApps.OrchardCore.Workforce.Intraday`

**What an admin can do in the UI:** (all `ManageWorkforceForecasts`)

- Workforce → Forecasting → Forecasts (`Admin/workforce/forecasts`, `ForecastsController`): list by planning group; Create (planning group, from / to dates, model); Details by day (intervals: volume, handle time, agents required and to schedule, service level, occupancy; backtest accuracy); Adjust (a percentage for a date range with a note); Activate (it becomes the team's coverage for the auto-scheduler); Delete.
- Workforce → Forecasting → Planning Groups (`forecasting/planning-groups`): queues, channel, team, service goal (percent in seconds, or average speed of answer), maximum occupancy, concurrency, patience, shrinkage, weeks of history. Setup kind `planningGroup`.
- Workforce → Forecasting → Capacity Plans (`workforce/capacity-plans`): long-range plan per planning group (start, weeks, weekly workload or the recent history, growth, hours per FTE, current FTE, attrition, hiring classes with training and ramp weeks); Details shows the weekly projection of required against available FTE. Setup kind `capacityPlan`.
- Workforce → Forecasting → Forecast Events (`forecasting/events`): named date ranges (kind, volume multiplier, yearly) for planning groups. Setup kind `forecastEvent`.
- Workforce → Forecasting → Intraday (`workforce/intraday`, `IntradayController`): for a planning group and day, forecast against actual per interval, required against scheduled and available, service level; Reforecast the rest of the day (`intraday/reforecast`); post an offer of voluntary overtime or voluntary time off for a time range and a number of places (`intraday/offer`); close an offer (`intraday/offer/{id}/close`). Workers take offers in My Work (`MyOffersController`).

**Already covered:** existing tool `workforceForecastSummary` (`Workforce.ContactCenter/Startup.cs`, `ForecastingAIToolsStartup`, feature Forecasting, `[RequireFeatures(AIConstants.Feature.Area)]`, category "Workforce") — "Summarizes the active contact center forecast of planning groups for a day: the contacts expected, the average handle time, when the work starts and ends, and the most agents to schedule and when, in each planning group's time zone." Permission `ManageWorkforceForecasts`. Arguments `planningGroupName`, `date`. Recipe step `WorkforceForecasting` (`PlanningGroups`, `ForecastEvents`, `CapacityPlans`) with a deployment step.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `listForecasts` | Read | no | `ManageWorkforceForecasts` | Lists the forecasts of a planning group or of all, with their dates, model, status (draft or active) and accuracy. | WorkforcePlanningAgent |
| `getForecast` | Read | no | `ManageWorkforceForecasts` | Gets a forecast: its adjustments, its accuracy against history and, for a chosen day, each interval's contacts, handle time and agents needed. | WorkforcePlanningAgent |
| `createForecast` | Write | no | `ManageWorkforceForecasts` | Builds a draft forecast for a planning group and a date range from its interval history, with the chosen model. It affects nothing until it is activated. | WorkforcePlanningAgent |
| `adjustForecast` | Write | no | `ManageWorkforceForecasts` | Raises or lowers a forecast's volume by a percentage for a range of dates, with a note, and recalculates the agents needed. | WorkforcePlanningAgent |
| `activateForecast` | Write | no | `ManageWorkforceForecasts` | Makes a forecast the active one for its dates, so the auto-scheduler and the intraday view staff to it. It replaces the forecast that was active for those dates. | WorkforcePlanningAgent |
| `deleteForecast` | Destructive | yes | `ManageWorkforceForecasts` | Deletes a forecast. | WorkforcePlanningAgent |
| `getCapacityPlanProjection` | Read | no | `ManageWorkforceForecasts` | Gives a capacity plan's week-by-week projection: the workload, the full-time agents needed, those available and those in training, and when the plan runs short. | WorkforcePlanningAgent |
| `getIntradayStatus` | Read | no | `ManageWorkforceForecasts` | For a planning group and day, compares forecast with actual contacts and handle time and agents needed with scheduled and available per interval, and lists the open overtime and time-off offers. | WorkforcePlanningAgent |
| `reforecastIntraday` | Write | no | `ManageWorkforceForecasts` | Rescales the rest of today's forecast for a planning group from how the day has gone so far, and returns the factor used. | WorkforcePlanningAgent |
| `postIntradayOffer` | External | yes | `ManageWorkforceForecasts` | Offers voluntary overtime or voluntary time off to a team for a time range, for a number of places. Workers see it in My Work and take a place until none are left. | WorkforcePlanningAgent |
| `closeIntradayOffer` | Write | no | `ManageWorkforceForecasts` | Closes an overtime or time-off offer so no more workers can take it. Places already taken stay. | WorkforcePlanningAgent |

**Implementation notes:** `ForecastService` (`CreateAsync(groupId, from, to, model, actor)`, `AdjustAsync`, `ScaleAsync`, `ActivateAsync`, `ActiveAsync`, `PageAsync`, `FindAsync`, `ActiveIntervalsAsync`, `HistoryAsync`), `CapacityPlanService.ProjectAsync`, `IntradayService` (`DayAsync`, `ReforecastAsync`, `OfferAsync`, `ListAsync`, `CloseAsync`), `PlanningGroupManager`. These are concrete classes in the module (`Services/Forecasting`, `Services/Intraday`), not interfaces. A forecast holds one interval per 15 minutes, so `getForecast` must return one day at a time or a summary. Forecast deletion: whether an active forecast may be deleted is unverified (`ForecastsController.Delete`).

**Test prompts:**

- "Build a forecast for Sales voice for the first two weeks of December and tell me the busiest half hour."
- "Black Friday will be about 40 percent busier than forecast. Adjust it."
- "How is today going for Support chat against the forecast? Do we need overtime this afternoon?"
- "Offer voluntary time off to the Billing team from 2 to 4 for three people."

## Workforce Adherence — `CrestApps.OrchardCore.Workforce.Adherence`

**What an admin can do in the UI:**

- Workforce → Adherence → Real-time (`Admin/workforce/adherence`, `AdherenceController`, `ViewWorkforceAdherence`): a live board of agents by team — scheduled activity, actual state and reason, in or out of adherence and for how long (refreshes every five seconds); a worker's day (`adherence/worker/{workerId}`: scheduled against actual timeline, adherence and conformance percent); Save / recompute the day (`.../save`, `ManageWorkforceAdherence`).
- Workforce → Adherence → Exceptions (`adherence/exceptions`, `AdherenceExceptionsController`): list by team (`ViewWorkforceAdherence`); Add (worker, start, end, reason, note — `ManageWorkforceAdherence`); Delete.
- Workforce → Adherence → Rule Sets (`adherence/rule-sets`, `AdherenceRuleSetsController`, `ManageWorkforceAdherence`): per team, which agent states and reasons count as adherent for each scheduled activity, with start and end grace, minimum out minutes and when to alert. Setup kind `adherenceRuleSet`.
- My Work → Adherence for the agent (`MyAdherenceController`). Background tasks "Workforce Adherence Alerts" (every minute) and "Workforce Adherence History". Workflow event: Out Of Adherence.

**Already covered:** existing tool `workforceOutOfAdherence` (`AdherenceAIToolsStartup`, feature Adherence) — "Lists the contact center agents out of adherence right now in the teams the user manages: each agent's team, the scheduled activity, the agent's state and reason, how many minutes they have been out, and whether an alert was raised." Permission `ViewWorkforceAdherence`. Argument `teamName`. Recipe step `WorkforceAdherence` (`RuleSets`) with a deployment step.

| Tool | Kind | Approval | Permission | Description | Agent |
| --- | --- | --- | --- | --- | --- |
| `getAdherenceDay` | Read | no | `ViewWorkforceAdherence` and the worker is in scope | Gets an agent's adherence for a day: adherence and conformance percent, minutes in and out, each stretch out of adherence with the scheduled activity and the actual state, and the exceptions applied. | WorkforcePlanningAgent |
| `listAdherenceExceptions` | Read | no | `ViewWorkforceAdherence` and team in scope | Lists the adherence exceptions recorded for a team or a worker and date range, with reason, note and who recorded each. | WorkforcePlanningAgent |
| `addAdherenceException` | Write | no | `ManageWorkforceAdherence` and the worker is in scope | Records an adherence exception for an agent for a time range, with a reason and note, so that time does not count against them, and recalculates the day. | WorkforcePlanningAgent |
| `deleteAdherenceException` | Destructive | yes | `ManageWorkforceAdherence` and the worker is in scope | Deletes an adherence exception, so the time counts against the agent again. | WorkforcePlanningAgent |

**Implementation notes:** `AdherenceService` (`BoardAsync(user, teamId, scopeService)`, `ComputeDayAsync`, `SaveDayAsync`, `FindDayAsync`, `PageExceptionsAsync`, `ListExceptionsAsync`, `AddExceptionAsync`, `FindExceptionAsync`, `RulesForTeamAsync`), a concrete class in the module. `getAdherenceDay` should call `ComputeDayAsync` for today and `FindDayAsync` (saved history) for past days, as the controller does. The delete call for an exception is in `AdherenceExceptionsController.Delete` (store call unverified).

**Test prompts:**

- "Who is out of adherence right now, and for how long?"
- "How was Ana's adherence yesterday?"
- "Excuse Tom from 10:00 to 10:45 today, he was in a coaching session."

## No tools needed

| Module | Reason | Settings group (if any) |
| --- | --- | --- |
| Calendars API — `CrestApps.OrchardCore.Calendars.Api` | HTTP endpoints under `/api/calendars/v1` for other systems (calendars, events, respond, free-busy, free-time); the tools call the services directly. Turn it on or off with `enableSiteFeature`. | — |
| Calendar Reminders — `CrestApps.OrchardCore.Calendars.Reminders` | On-screen reminders for the signed-in user (`Admin/api/calendars/reminders`, dismiss) and the "starting soon" / "completed" moments; no configuration. The reminder minutes of an event are a field of `updateCalendarEvent`. | — |
| Calendar Invitations — `CrestApps.OrchardCore.Calendars.Invitations` | Sends invitation emails and serves the public answer link; no screen and no settings (it uses the site's Email settings). It changes the risk of the event tools (they email attendees). | — |
| Calendar dashboard widget (`CalendarWidgetPart`, needs Admin Dashboard) | A content part: the content tools cover it. | — |
| Scheduling Self-Service — `CrestApps.OrchardCore.Scheduling.SelfService` | Public booking and manage pages (`book/{slug}`, `book/manage/{id}/{token}`); the admin sets them up through the appointment type (`slug`, `AllowSelfService`). | — |
| Scheduling API — `CrestApps.OrchardCore.Scheduling.Api` | HTTP endpoints under `/api/scheduling/v1`; same actions as the tools. | — |
| Technician Mobile — `CrestApps.OrchardCore.Dispatch.Mobile` | The technician's Field page (`Admin/my-work/field`) and its offline sync endpoints; configured only in appsettings (`CrestApps:Dispatch:Mobile`: `OfflineDays`, `PingIntervalSeconds`, `LocationRetentionDays`, `ReceiptRetentionDays`…). | — (appsettings) |
| Field Service API — `CrestApps.OrchardCore.Dispatch.Api` | HTTP endpoints under `api/work-orders/v1`, `api/dispatch/v1`, `api/geo/v1`; same actions as the tools. | — |
| Customer tracking page (`track/{token}`, in Dispatch) | Public page for the customer; `dispatchWhereIsMyTechnician` is its conversational twin. | — |
| Workforce API — `CrestApps.OrchardCore.Workforce.Api` | HTTP API for other systems; same actions as the tools. | — |
| Workforce My Work Portal — `CrestApps.OrchardCore.Workforce.Portal` | Worker self-service (schedule, time off, open shifts, trades, availability, notification settings, calendar feed, punch, timesheet, approvals for managers). Not an administrator's job; the manager approvals in it (`MyTeamController`) are the same service calls as the `decide…` tools. See Open questions for a worker-facing set. | — |
| Contact Center Sign-in Punches — `CrestApps.OrchardCore.Workforce.ContactCenter.SignInPunches` | No screen; punches agents in and out on sign-in. Enable or disable the feature. | — |
| Time clock kiosk pages (`TimeClockKioskController`, in Workforce Time Clock) | Public kiosk device pages (identify by badge and PIN, punch). | — |
| Report data sources and reports (Calendars, Scheduling, Dispatch field-service and asset data sets, Workforce, Forecast intervals, Adherence; Time Tracker "Hours by Category") | Read through the Reports / Report Builder / Data Pipelines tools of another group; `getTimeTotals` and `getFieldServiceKpis` give the two most asked numbers directly. | — |
| Workflow activities of every module in the group | Covered by the existing workflow tools (`listWorkflowActivities`, `createOrUpdateWorkflow`). | — |
| Background tasks of the group (calendar reminders and sync, appointment holds, SLA monitor, agreement visits, visit risk and reminders, shift reminders, open-shift awards, accrual, timesheet refresh, tracker sync, adherence alerts and history) | Managed by the Orchard Core Background Tasks admin (another group's tools). | — |

## Proposed agents for this group

| Agent | Purpose (one sentence) | Tools | Could merge with |
| --- | --- | --- | --- |
| `CalendarAgent` (exists; extend) | Day-to-day calendar and appointment work: find, add, change and answer events, find free time, book and manage appointments and the waitlist. | Existing 10 (`listCalendars`, `findCalendarEvents`, `findFreeTime`, `createCalendarEvent`, `moveCalendarEvent`, `cancelCalendarEvent`, `listAppointmentTypes`, `findAppointmentSlots`, `bookAppointment`, `changeAppointment`) + `getCalendarEvent`, `updateCalendarEvent`, `deleteCalendarEvent`, `respondToCalendarEvent`, `listCalendarCategories`, `getTeamAvailability`, `listAppointments`, `getAppointment`, `setAppointmentStatus`, `listAppointmentWaitlist`, `addToAppointmentWaitlist`, `removeFromAppointmentWaitlist` = 22 | Bookings (if that module gets tools); a general "assistant" agent |
| `CalendarAdminAgent` (new) | Sets up calendars and scheduling: calendars, sharing, categories, feeds, external subscriptions, appointment types and bookable resources with their working hours. | `getCalendar`, `listCalendarTypes`, `saveCalendar`, `shareCalendar`, `deleteCalendar`, `saveCalendarCategory`, `deleteCalendarCategory`, `getCalendarFeedAddress`, `resetCalendarFeedAddress`, `importCalendarEvents`, `saveCalendarSubscription`, `syncCalendar`, `getAppointmentType`, `saveAppointmentType`, `deleteAppointmentType`, `listSchedulingResources`, `saveSchedulingResource`, `deleteSchedulingResource` + shared reads `listCalendars`, `listCalendarCategories`, `listAppointmentTypes` = 21 | `CalendarAgent` only if tools are trimmed (together 40); otherwise stands alone |
| `FieldServiceAgent` (new) | Runs field service day to day: work orders, visits, technicians and the dispatch day. | Existing 6 (`workOrderCreate`, `workOrderStatus`, `workOrderSummarize`, `dispatchFindTechnician`, `dispatchOptimizeAndExplain`, `dispatchRescheduleVisit`) + `listWorkOrders`, `updateWorkOrder`, `setWorkOrderStatus`, `closeWorkOrder`, `addWorkOrderNote`, `recordWorkOrderPartUsed`, `answerWorkOrderChecklist`, `completeWorkOrder`, `createFollowUpWorkOrder`, `listVisits`, `getVisit`, `createVisits`, `unassignVisit`, `setVisitStatus`, `closeVisit`, `pinVisit`, `getDispatchBoard`, `getFieldServiceKpis` = 24 | Geo / territories and Inventory reads (parts) if those stay small |
| `FieldServiceSetupAgent` (new) | Sets up field service: work order types, checklists, service levels, sub-statuses, service agreements and assets. | `listWorkOrderSettings`, `getWorkOrderSetting`, `saveWorkOrderSetting`, `deleteWorkOrderSetting`, `listServiceAgreements`, `getServiceAgreement`, `saveServiceAgreement`, `runServiceAgreement`, `deleteServiceAgreement`, `listAssets`, `getAsset`, `saveAsset`, `deleteAsset`, `recordAssetMeterReading`, `listAssetTypes`, `saveAssetType`, `deleteAssetType`, `getFieldServiceSetupStatus` = 18 | Geo (territories), Skills and Staff capabilities setup; Inventory setup |
| `WorkforceSchedulingAgent` (new) | Plans and runs team schedules: drafts, publishing, published-shift changes, open shifts, trades, labor rule overrides and predictability pay. | Existing 4 (`workforceWhoIsWorking`, `workforceMySchedule`, `workforceFindCover`, `workforceExplainViolations`) + `listShiftSchedules`, `getShiftSchedule`, `createShiftSchedule`, `saveDraftShift`, `deleteDraftShift`, `fillShiftSchedule`, `publishShiftSchedule`, `changePublishedShift`, `deleteShiftSchedule`, `listOpenShifts`, `postOpenShift`, `decideOpenShiftClaim`, `cancelOpenShift`, `listShiftTrades`, `createShiftTrade`, `decideShiftTrade`, `listLaborRuleOverrides`, `listPredictabilityPay`, `decidePredictabilityPay` = 23 | Staff directory reads (who is on a team) |
| `TimeOffAgent` (new) | Handles time off: requests and approvals, balances, adjustments and policy assignment. | Existing 2 (`workforceTimeOffBalance`, `workforceRequestTimeOff`) + `listTimeOffRequests`, `submitTimeOffRequest`, `decideTimeOffRequest`, `getWorkerTimeOff`, `adjustTimeOffBalance`, `assignTimeOffPolicy` = 8 | `WorkforceSchedulingAgent` (31 together, so only if schedule tools are trimmed) or a Staff / HR agent |
| `TimeAndAttendanceAgent` (new) | Reviews worked time: timesheets, punches, attendance, payroll export and Time Tracker entries and setup. | `listTimesheets`, `getTimesheet`, `prepareTimesheets`, `decideTimesheet`, `lockPayPeriod`, `saveTimesheetPunch`, `removeTimesheetPunch`, `addTimesheetLine`, `removeTimesheetLine`, `decidePunchCorrection`, `listAttendanceExceptions`, `decideAttendanceException`, `listPayrollExports`, `runPayrollExport`, `listTimeEntries`, `getTimeTotals`, `saveTimeEntry`, `deleteTimeEntry`, `listTimeTrackerSetup`, `saveTimeTrackerSetup`, `deleteTimeTrackerSetup`, `getContactCenterTimeStages`, `saveContactCenterTimeStages` = 23 | Split into a Time Tracker agent (9) and a Time Clock agent (14) for sites that run only one of them |
| `WorkforceSetupAgent` (new) | Sets up Workforce: every setup catalog through one tool family, team and worker settings, kiosks, PINs and pay rates. | `listWorkforceSetup`, `getWorkforceSetup`, `saveWorkforceSetup`, `deleteWorkforceSetup`, `saveWorkforceTeamSettings`, `saveWorkerWorkLimits`, `getWorkforceSetupStatus`, `getWorkerTimeClock`, `setWorkerTimeClockPin`, `saveWorkerPayRate`, `removeWorkerPayRate`, `renewTimeClockKioskAddress`, `createWorkersForAgents` = 13 | Staff, Teams and Skills setup tools (same people use them) |
| `WorkforcePlanningAgent` (new) | Contact center workforce planning: forecasts, capacity, the intraday view and offers, and adherence. | Existing 2 (`workforceForecastSummary`, `workforceOutOfAdherence`) + `listForecasts`, `getForecast`, `createForecast`, `adjustForecast`, `activateForecast`, `deleteForecast`, `getCapacityPlanProjection`, `getIntradayStatus`, `reforecastIntraday`, `postIntradayOffer`, `closeIntradayOffer`, `getAdherenceDay`, `listAdherenceExceptions`, `addAdherenceException`, `deleteAdherenceException` + shared `listWorkforceSetup` = 18 | A Contact Center supervisor agent (queues, agents, interval statistics) |
| direct | Self-service a signed-in user asks the main assistant for. | `getMyTimeStatus`, `clockMyTime` (and, if wanted on the main assistant, the existing `workforceMySchedule`, `workforceTimeOffBalance`, `workforceRequestTimeOff`) | — |

New tools proposed in this group: 144 (Calendars 13, Feeds and External 5, Scheduling 12, Work Orders 13, Service Agreements 5, Dispatch 10, Assets 8, Time Tracker 9, Workforce base 7, Shift Planning 16, Labor Rules 3, Time Off 6, Time Clock 19, Workforce for Contact Center 3, Forecasting and Intraday 11, Adherence 4). Existing tools kept: 25.

## Open questions

- Renaming the existing tools. Fifteen existing names are noun-first and do not match the verb-first style: `workOrderCreate`, `workOrderStatus`, `workOrderSummarize`, `dispatchFindTechnician`, `dispatchOptimizeAndExplain`, `dispatchRescheduleVisit`, `dispatchWhereIsMyTechnician`, `workforceWhoIsWorking`, `workforceMySchedule`, `workforceFindCover`, `workforceExplainViolations`, `workforceTimeOffBalance`, `workforceRequestTimeOff`, `workforceForecastSummary`, `workforceOutOfAdherence`. Candidates: `createWorkOrder`, `getWorkOrderStatus`, `summarizeWorkOrder`, `findTechniciansForVisit`, `proposeDispatchDay` + `applyDispatchMoves`, `rescheduleVisit`, `getTechnicianEta`, `listWorkersOnShift`, `getMySchedule`, `findShiftCover`, `explainScheduleViolations`, `getMyTimeOffBalance`, `requestTimeOff`, `getForecastSummary`, `listOutOfAdherenceAgents`. Renaming breaks saved AI profiles and agent templates whose `ToolNames` lists refer to the old names; is an alias mechanism available, or do we keep the names?
- Approval on existing tools. None of the 25 existing tools calls `.RequireApproval()`. `cancelCalendarEvent`, `createCalendarEvent` with attendees (emails when Invitations is on), `dispatchRescheduleVisit` and the apply half of `dispatchOptimizeAndExplain` (customer SMS / email when notices are on) and `changeAppointment` meet the brief's rule for approval. Add it, or keep relying on "confirm with the user first" in the description?
- One `kind`-based family for setup catalogs (`listWorkforceSetup` … with 18 kinds; `listWorkOrderSettings` … with 4; `listTimeTrackerSetup` … with 2) keeps the count at 11 tools instead of about 70. The cost is that the model must fetch the kind's schema with `get…` before saving. Is that acceptable, or do you prefer one family per feature (Shift Planning, Time Off, Time Clock, Labor Rules, Forecasting), about 20 tools?
- Should the same generic idea go further — one cross-module `listCatalogItems / getCatalogItem / saveCatalogItem / deleteCatalogItem` for every `CatalogManager<T>` record in CrestApps (these modules, Staff, Teams, Skills, Inventory…)? All of them already have recipe sections with the same shape.
- "Notifies a worker" as `External`. I classed tools that trigger workforce notifications (inbox, web push, email) and customer notices as `External` with approval, and tools that change pay as `External`. If worker notifications should not count, `decideTimeOffRequest`, `decideTimesheet`, `postOpenShift`, `decideOpenShiftClaim`, `createShiftTrade`, `decideShiftTrade`, `submitTimeOffRequest` and `postIntradayOffer` become `Write` without approval.
- Worker self-service. The My Work portal actions are not proposed (claim or withdraw an open shift, offer or answer a trade, set availability, notification settings, renew the calendar feed, punch in and out, submit a timesheet, ask for a punch correction, take an intraday offer). If a worker-facing assistant is wanted, a small `MyWorkAgent` would need about ten tools (`listMyOpenShifts`, `claimOpenShift`, `offerShiftTrade`, `answerShiftTrade`, `saveMyAvailability`, `punchMyTime`, `getMyTimesheet`, `submitMyTimesheet`, `requestPunchCorrection`, `takeIntradayOffer`) next to the three existing self-service tools.
- Where do work-order tools register? Today they sit in Dispatch's `AIToolsStartup`, so a site with Work Orders but without Dispatch has none. I propose a WorkOrders `AIToolsStartup`; confirm that the module may take a dependency on `CrestApps.Core.AI` behind `[RequireFeatures("CrestApps.OrchardCore.AI")]` as Calendars does.
- Scheduling tools are anonymous by design (customer-facing chat). On the admin assistant that is fine, but `bookAppointment` returns the `manageCode` secret and `listAppointmentTypes` shows non-self-service types to anyone with `ManageAppointments`. Should the admin-side variants be separate tools, or the same tools with the user's permission deciding the behavior as today?
- `ContactCenterTimeStages` lives on the site settings document without a settings group. Should the generic settings trio support group-less settings objects, or keep the two dedicated tools?
- Team and worker cards (`WorkforceTeamSettings`, `WorkforceStaffSettings`) are extension data on records owned by the Teams and Staff modules. Whoever inventories those modules should say whether `saveTeam` / `saveStaffMember` carry extension data; if so, `saveWorkforceTeamSettings` and `saveWorkerWorkLimits` are not needed.
- Scope in tools. `IWorkforceScopeService` limits a manager to their teams; tool results must apply it even when the model passes an id from another team. The existing tools do; make it a rule in the shared base (`WorkforceAIToolBase`).
- Service extraction. Several screens keep their query or rule in the controller (visits list, work orders list, dispatch board data, both setup checklists, team availability, timesheet prepare, time-off policy assign / unassign, the "in use" delete guards of work order settings and assets, and all Scheduling validation in display drivers). Is refactoring those into services in scope of the tool work, or should the tools duplicate the logic first?
- `syncCalendar` has no UI equivalent (sync is background-only). Keep it?
