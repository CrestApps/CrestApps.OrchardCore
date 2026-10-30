---
sidebar_label: Calendars
title: Calendars
description: "Keep shared, personal, team, room, equipment and office calendars in one place, with repeating events, attendees and invitations, room and desk limits, team availability, sharing, reminders, subscription addresses and external calendars."
technical_manual:
  - modules/calendars
---

The calendar keeps everyone's time in one place: company events, your own meetings, a team's on-call rota, who books which meeting room, and who is in the office. Each calendar has a **type** that says what it's for, and the type decides who owns the calendar, which kinds of events it holds, and whether events may overlap.

| | |
| --- | --- |
| **Menu** | Calendars > Calendar, Calendars > Manage Calendars |
| **Permission** | Use calendars (to create calendars of your own: Create calendars) |
| **Feature** | Calendars, plus the calendar features below for more types |

<AskYourAdmin />

## Kinds of calendars

Which types you can choose depends on the features your administrator turned on.

| Type | Feature | Use it for | Kinds of events |
| --- | --- | --- | --- |
| **General** | Calendars | A shared calendar that belongs to nobody in particular, such as a company or project calendar. | Event, Meeting, Holiday, Reminder |
| **Personal** | Calendars | Your own calendar. Everyone gets one the first time they open the calendar. Others see when you're busy, not what's on it, unless you share it. | Event, Meeting, Focus time, Out of office |
| **Team** | Team Calendars | A team's calendar. The team is a role: everyone in the role can see it and add to it. | Meeting, On call, Out of office, Milestone |
| **Room** | Resource Calendars | A meeting room or another space that one group uses at a time. Invite the room to an event to book it. | Booking, Maintenance |
| **Equipment** | Resource Calendars | Something that can be reserved, such as a projector or a vehicle. | Reservation, Maintenance |
| **Office** | Office Calendars | Who is in an office and which desks are booked, up to the number of desks. | In office, Desk booking, Visitor, Office closed |

Two more types are kept by other features and can't be added by hand: **Appointments**, one per staff member, room or piece of equipment that takes appointments (see [Scheduling](./scheduling.md)), and **Bookings**, which holds the store's bookings (see [Bookings](../commerce/bookings.md#bookings-on-the-calendar)). Other modules you add can bring types of their own, such as a calendar of employees' shifts and time off.

Some kinds of events have their own behavior:

- **Holiday**, **Reminder**, **Milestone** and **Office closed** leave the time free: they show on the calendar but don't make anyone busy.
- **On call**, **Out of office** (on a team calendar), **In office** and **Desk booking** must name at least one person.
- A kind of event that needs approval, such as time off on a calendar another module adds, is created as *Tentative* and waits until someone who manages the calendar confirms it.

## Use the calendar

Open **Calendars > Calendar**. The list on the side shows every calendar you can see; tick the ones to show. Your choice is remembered on that browser.

Times on the calendar are in your time zone (the one on your user profile, or the site's), not your computer's, so they match the times on forms and lists across the site even when you're away.

- **Day**, **Week**, **Month** and **Agenda** change the view, the arrows move back and forward, and **Today** comes back to today.
- Click an event to see its calendar, kind, place, status, attendees and description, and **Edit** to open it. On a calendar you can only see as busy time, the event shows as busy, without its details.
- To add an event, choose **New event**, or drag across the times you want. When more than one calendar you can add to is showing, you're asked to choose the calendar and the kind of event first.
- Drag an event to move it, or drag its end to make it longer or shorter. For a repeating event you're asked whether to move **This occurrence**, **This and following** or **All occurrences**. If the move isn't allowed, such as when a room is already booked then, the event goes back and you're told why.

Events another feature keeps, such as appointments and bookings, can't be moved or changed here: their details show who manages them.

## Create a calendar

1. Open **Calendars > Manage Calendars**.
2. Choose **Add calendar** and pick the type.
3. Fill in the tabs below and choose **Save**.

When you create a calendar, you manage it. **Manage Calendars** lists the calendars you can see, with a search box and a **Type** filter. **Open** shows a calendar in the calendar view, and on a calendar you manage, **Edit** opens its settings and the menu next to it has **Delete calendar**, which deletes the calendar and every event on it.

### General

| Field | What it does |
| --- | --- |
| **Name**, **Description** | How the calendar is listed. |
| **Color** | The color its events are drawn in, unless their kind has its own. |
| **Time zone** | The time zone new events on this calendar use. Leave it on **The site's time zone** to follow the site. |
| **Visibility** | Who can see the calendar besides its owner and the people it is shared with: **Private: only the owner and the people it is shared with**, **Shared: everyone sees when it is busy, not the details**, or **Public: everyone sees its events**. |
| **Enabled** | A disabled calendar keeps its events but takes no new ones. |
| **Location**, **Seats**, **Equipped with** | Room and equipment calendars: where it is, how many people the room seats (0 for equipment), and what it has. |
| **Address**, **Contact** | Office calendars: where the office is, and who to ask about it, such as the office manager. |

### Owner

Personal calendars belong to a user, and team calendars belong to a role. On the **Owner** tab, choose the user or the **Team** (role). Everyone in the role can see a team calendar and add events to it. Only calendar administrators can give a personal calendar to someone else.

### Sharing

Share a calendar with **Users** or **Roles**, each with one of these levels. Each level includes the ones above it:

| Level | Lets them |
| --- | --- |
| **See free/busy time** | See when the calendar is busy, without the details. |
| **See events** | See the events and their details. |
| **Add and change events** | Add, change and cancel events. |
| **Manage** | Change the calendar and who it is shared with. |

The calendar's owner and calendar administrators always manage it.

### Overlaps

Tick **Limit overlapping events** for a calendar that can only hold so much at once, and set **Events that may overlap**: 1 for a room, the number of desks for an office. A busy event is then refused when the calendar is already full at that time. Room and equipment calendars start with a limit of 1, and office calendars with 20.

Free events, and kinds of events that don't count (such as a team's **Out of office**), never fill the calendar.

## Add and change events

You can do everything from the calendar without leaving it. The editor opens in a window over the calendar, and the calendar refreshes when you save:

- **Add:** choose **New event**, or drag across the times (or click a day) where the event goes, then pick the calendar and the kind of event.
- **Change, cancel or delete:** click an event and choose **Edit**. The editor's **Open full page** link opens the same editor on its own page.
- **Move or resize:** drag the event, or drag its bottom edge.

Only the events of calendars you can change can be edited this way. The calendar loads the events of the days you're looking at as you move between them, so a busy calendar still opens quickly.

The event editor has three tabs.

**General**

| Field | What it does |
| --- | --- |
| **Title** | The event's name. |
| **Kind of event** | The kinds the calendar's type offers. The kind sets its color and its defaults, such as how long it lasts. |
| **Category** | One of your categories (see [Color-code with categories](#color-code-with-categories)). Its color wins over the kind's. |
| **All day** | Lasts whole days. **Ends** is then the last day it lasts. |
| **Starts**, **Ends** | When it starts and ends. |
| **Time zone** | A repeating event keeps its time of day in this time zone, across daylight saving changes. |
| **Location**, **Description** | Where and what. |
| **Status** | **Confirmed**, **Tentative** or **Cancelled**. |
| **Show as** | **Busy**, or **Free** for an event that is shown but leaves the time free for other plans. |
| **Visibility** | **Same as the calendar**, **Public**, or **Private: others see only that the time is busy**. |
| **Color** | Tick **Use its own color** to pick a color for this event only. |

**Repeat**

Choose how the event **Repeats**: **Does not repeat**, **Daily**, **Weekly**, **Monthly** or **Yearly**, and **every** how many days, weeks, months or years. A weekly event repeats **On** the days you tick, so "every day from the 3rd to the 30th except Mondays" is **Weekly**, every 1 week, with every day but Monday ticked, and **Ends** **On** the 30th. A monthly event repeats on **The same day of the month** or **The same weekday of the same week, such as the second Tuesday**. Under **Ends**, choose **Never**, **On** a date, or **After** a number of occurrences. For a pattern the choices don't cover, a **Custom rule** (an iCalendar repeat rule) is used instead.

**Attendees**

| Field | What it does |
| --- | --- |
| **People** | Users of the site. Choose **Invite users** and search. |
| **Guests** | People outside the site, one per line, as an email address or a name followed by the address in angle brackets. |
| **Rooms and equipment** | Book a room or a piece of equipment for the event. This fails when it is already booked at that time. |
| **Answers** | Who has accepted, said maybe or declined. |

When you're invited to an event, its page shows **You are invited. Your answer:** with **Accept**, **Maybe** and **Decline**.

### Change a repeating event

Opening one occurrence of a repeating event shows **Apply the changes, or the cancellation, to**: **This occurrence**, **This and following**, or **All occurrences**. Changing only this occurrence leaves the others as they are; changing this and following ends the series there and starts a new one with your changes. To change how the event repeats, edit the whole series from its first occurrence.

**Cancel event** (**Cancel** on an occurrence) cancels it, or the occurrences you chose: cancelling **This occurrence** removes just that day from the series, and **All occurrences** cancels the whole event, which stays on the calendar as cancelled. **Delete** removes it for good, with every occurrence if it repeats.

### Approve events

A kind of event that needs approval, such as time off on a calendar of employees' shifts, is created as *Tentative*. Someone who manages the calendar opens it and chooses **Confirm**.

## Book rooms and equipment

With Resource Calendars on, add a **Room** or **Equipment** calendar for each room or item. To book one, invite it under **Rooms and equipment** on any event, or add a **Booking** directly on the room's calendar. A room takes one booking at a time, so a second booking for the same time is refused with a message saying the room is already booked. A booking that the room has declined doesn't count.

Office calendars work the same way with their desk count: once that many people are in the office or have booked a desk at the same time, the next one is refused.

## Color-code with categories

Categories are yours to make: "Sales calls", "Admin", "Personal", whatever helps you read your week at a glance. Open **Calendars > Categories**, choose **Add category**, give it a **Name** and a **Color**, and **Save**. Then pick it as an event's **Category** in the event editor.

- An event shows in its category's color, unless the event picks its own color under **Color**.
- The calendar view lists your categories under **Categories**, with **No category** at the end. Untick a category to hide its events; your choice is remembered in this browser.
- Your categories are only yours. People who can manage all calendars can also tick **Everyone can use it**, to make a category everyone sees and uses (such as "Company holiday").
- Deleting a category keeps its events; they show again in their kind's color.

## See someone else's schedule

To see what people have scheduled, such as a manager checking a team's workload, use **Whose schedule** under the calendar list on **Calendars > Calendar**. Choose **Choose people** and pick one or more users in the box that appears, or choose **Everyone**, then **Show**. The calendar shows the events on those people's own calendars and the events they attend on any calendar. Events you aren't allowed to open show as **Busy**, with their time but no title. **Back to my calendars** returns to your own calendars. The address of the page keeps the choice, so you can bookmark it.

**Whose schedule** only appears for people allowed to see others' schedules. Two pairs of permissions decide this, under **Security > Roles**:

| Permission | What it allows |
| --- | --- |
| List anyone's calendar events | See anyone's schedule, or everyone's. |
| List own calendar events | See your own schedule. Everyone who can use calendars has it. |
| View anyone's calendar events | Open any event, instead of seeing it as busy. |
| View own calendar events | Open events you created, attend, or that are on your own calendar. Everyone who can use calendars has it. |

Being able to read a calendar, because it's shared with you or visible to everyone, also lets you open its events (private events need the right to change the calendar). **View all calendars** includes both "anyone's" permissions.

## See when your team is free

With Team Calendars on, open **Calendars > Team Availability**. Choose the team's **Calendar**, the **Day** and the **Meeting length (minutes)**, then **Show**. Each member of the team's role gets a row showing when they are **Busy**, **Tentative** or **Free**, from all of their calendars. **When everyone is free** lists the gaps long enough for the meeting that everyone shares.

## Invitations

With **Calendar Invitations** on, attendees with an email address are emailed:

- an invitation, with a calendar file they can add to Outlook, Google Calendar or their phone, when they are invited;
- an update when the event's title, time or place changes;
- a cancellation when the event is cancelled or deleted.

The invitation has **Accept**, **Maybe** and **Decline** links. Guests answer from those links without signing in, and their answer shows under **Answers**. It needs the site's email to be set up.

## Reminders

With **Calendar Reminders** on, an event's **Remind** setting (on its **General** tab) shows a reminder before it starts: when it starts, 5, 10, 15 or 30 minutes, 1 or 2 hours, or a day before. Events that take time remind 10 minutes before unless you change it; all-day events don't remind.

The reminder shows at the top right of every admin page of the calendar's owner (for a personal calendar) and of each invited user who hasn't declined. It stays until you click **Dismiss** or close it, and dismissing it on one page or tab removes it from the others. **Open calendar** opens the calendar on that day. Pages check for reminders every minute while they're open.

Calendar Reminders also runs every minute to notice when events are about to start and when they have ended, once for each occurrence, so workflows can act at those moments, such as emailing attendees 15 minutes before a meeting (see [Workflows](#workflows)).

## Subscribe from Outlook, Google Calendar or a phone

With **Calendar Feeds** on, a calendar's menu on **Manage Calendars** has:

- **Subscription address**: choose **Make an address**, then **Copy** it. Add it to Google Calendar (**Other calendars > From URL**), Outlook (**Add calendar > Subscribe from web**) or a phone, and they show the calendar's events and keep them up to date. Anyone with the address can see the events, so share it only with people you trust; private events show as busy time. **Make a new address** stops the old one working, and **Remove the address** turns it off.
- **Export .ics** downloads the calendar as a file.
- **Import .ics** adds the events of an iCalendar file, such as one exported from Google Calendar or Outlook. Events already in the calendar with the same identifier are updated instead of added again, so importing the same file twice is safe.

## Show an external calendar here

With **External Calendars** on, the calendar editor has a **Subscription** tab. Tick **Keep this calendar in step with an external calendar**, keep the **Provider** on the iCalendar address, and paste the external calendar's secret iCalendar **Address**, such as the one Google Calendar or Outlook shows under the calendar's settings. It must start with https://, http:// or webcal://. **Every (minutes)** sets how often it is read (at least 5).

The external calendar's events are copied here and kept up to date, and events removed there are removed here. They can't be changed here. **Last synchronized** shows when it last worked, or why it didn't; when the address keeps failing, the site waits longer between tries.

:::note[Google Calendar and Microsoft 365]
Signing in to Google Calendar or Microsoft 365 to keep calendars in step both ways is not available yet. Use the subscription address and the iCalendar address described above.
:::

## Workflows

With the **Workflows** feature on, the **Calendar** category of the workflow editor has:

- **Events** that start a workflow: **Calendar Event Created**, **Calendar Event Updated**, **Calendar Event Cancelled**, **Calendar Event Deleted**, **Calendar Attendee Invited** and **Calendar Attendee Response Received**, and with Calendar Reminders, **Calendar Event Starting Soon** (set **Minutes before the start**) and **Calendar Event Completed**. Each can be limited to some **Calendars**, **Calendar types** or **Kinds of events**. **Ignore changes made by workflows** is ticked by default, so a workflow that changes events doesn't start itself again.
- **Tasks** that act on the calendar: **Create Calendar Event**, **Update Calendar Event**, **Cancel Calendar Event**, **Get Calendar Event**, **Find Calendar Events**, **Add Calendar Attendee**, **Remove Calendar Attendee** and **Find Free Time**.

For example: when an **Out of office** event is created on a team calendar, email the team lead; or when a meeting is starting soon, send its attendees the room and the agenda. The values each event hands to the next steps are listed in the [Technical Manual](../../modules/calendars.md#workflows).

## Copy calendars to another site

Calendars, their events and the categories travel between sites with a deployment plan (**Configuration > Import/Export > Deployment Plans**, with the **Deployment** feature on):

1. Open or create a deployment plan and choose **Add Step**.
2. Under **Calendar**, add **Calendars**. Tick **Include events** to take every calendar's events (up to 10,000 per calendar) and **Include categories** (on by default) to take everyone's categories, so the events keep their colors.
3. **Execute** the plan to download it, then import the file on the other site (**Import/Export > Package Import**), or send it with a remote deployment.

The import creates what's missing and updates what's already there, matched by each record's id, so importing the same file twice doesn't duplicate anything. A calendar needs its kind's feature to be on in the other site (for example **Resource Calendars** for rooms). Imported events don't send invitations and aren't checked for overlaps.

## Which feature to turn on

| You want to... | Ask your administrator to turn on |
| --- | --- |
| Use the calendar, with shared and personal calendars | Calendars |
| Give teams their own calendar and see when a team is free | Team Calendars |
| Book meeting rooms and equipment | Resource Calendars |
| Track who is in the office and book desks | Office Calendars |
| Email invitations that attendees answer from the email | Calendar Invitations |
| Be reminded before events start, on screen | Calendar Reminders |
| Act a set time before events start, or once they end | Calendar Reminders (with Workflows) |
| Subscribe to a calendar from Outlook, Google Calendar or a phone, or import and export files | Calendar Feeds |
| Show an external calendar's events here | External Calendars |
| Let other systems read and change events | Calendars API |
| Let an AI assistant read your calendar, find free time and add events | Calendars and AI Services |
| Book appointments with staff, rooms and equipment | Scheduling (see [Scheduling](./scheduling.md)) |

## Troubleshooting

- **I can't see a calendar.** It's private and not shared with you. Ask its owner to share it, or to make it shared or public.
- **I can see the time but not the event.** You can only see the calendar's free/busy time, or the event is private.
- **My event was refused with "already booked" or "already full".** A room, a piece of equipment or the office has no room left at that time. Pick another time, or check what's booked on that calendar.
- **I can't drag or edit an event.** It's kept by another feature, such as an appointment or a booking, or you can only see the calendar. Change appointments under **Calendars > Appointments** and bookings under **Commerce > Bookings**.
