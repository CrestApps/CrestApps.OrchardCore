---
sidebar_label: Scheduling
title: Scheduling
description: "Book appointments with staff, rooms and equipment from their working hours or shifts, never twice at the same time, with public booking pages, links customers use to move or cancel, a waitlist and workflows."
technical_manual:
  - modules/scheduling
---

Scheduling books appointments with your staff, rooms and equipment: a consultation, a service visit, a class. You describe each kind of appointment and who takes it, set when each person or room is available, and the site offers only the times that are really free. Two people can never book the same person or room at the same time, and every appointment goes on that person's or room's calendar.

| | |
| --- | --- |
| **Menu** | Calendars > Appointments, Calendars > Appointment Types, Calendars > Scheduling Resources, Calendars > Waitlist |
| **Permission** | View appointments, Manage appointments (booking and changing appointments, and the waitlist), Manage scheduling (appointment types and resources) |
| **Feature** | Scheduling (it needs Calendars) |

<AskYourAdmin />

To set it up, add your resources first, then your appointment types.

## Add the people, rooms and equipment that take appointments

Open **Calendars > Scheduling Resources** and choose **Add resource**.

| Field | What it does |
| --- | --- |
| **Name** | How the resource is shown, such as "Dr. Jane Smith" or "Treatment room 2". |
| **Kind** | **Staff member**, **Room**, **Equipment** or **Other**. |
| **User** | For a staff member: the user they are. Their own calendars count too, so a meeting on their personal calendar keeps that time from being booked. |
| **Calendar** | The calendar appointments go on. Its events also make the resource busy. Leave it on **Make a new calendar** to get one, or choose an existing calendar, such as a room's calendar. |
| **Time zone** | The time zone the working hours are in. |
| **Takes appointments** | Untick to stop offering this resource without deleting it. |
| **Hours come from** | **Working hours below**, or **Shifts on a calendar** (a calendar that holds **Shift** events, such as one a workforce management module adds). |
| **Working hours** | One line per day, such as *Monday 09:00-12:00, 13:00-17:00*. The gaps are breaks. A day that isn't listed is a day off. |
| **Days off and changed days** | One per line, such as *2026-12-24 closed*, or *2026-12-31 09:00-12:00* for shorter hours that day. |
| **Shift calendar** | For hours from shifts: the calendar whose **Shift** events name this staff member, such as one a workforce management module provides. They can then only be booked during their shifts. |

The resource's calendar is an **Appointments** calendar. Like any calendar, you can open it in **Calendars > Calendar**, and anything you add to it there, such as a **Blocked time** event, keeps that time from being booked.

Deleting a resource keeps its calendar and its appointments.

## Describe an appointment type

Open **Calendars > Appointment Types** and choose **Add appointment type**.

**General**

| Field | What it does |
| --- | --- |
| **Name**, **Description** | What customers see. |
| **Location** | Where the appointment takes place. |
| **Color** | The color of its events on the calendar. |
| **Can be booked** | Untick to stop offering it. |

**Time**

| Field | What it does |
| --- | --- |
| **Length (minutes)** | How long an appointment lasts (30 by default). |
| **Start times every (minutes)** | How far apart the offered times are, such as every 30 minutes. |
| **Free time before (minutes)**, **Free time after (minutes)** | Time the resource needs free around each appointment, such as travel or cleaning up. Other plans can't fall in it. |

**Who**

| Field | What it does |
| --- | --- |
| **Resources** | The staff, rooms and equipment that take this appointment. |
| **Pick** | **Any free resource** (the first one free in the list), **Share out (round robin)** (the free one with the fewest recent appointments, so work is shared out), or **All resources together** (every resource at once, such as a doctor and a room). |
| **Places per appointment** | More than 1 makes it a class or group session that several customers book. The time stays open until it is full. |

**Booking rules**

| Field | What it does |
| --- | --- |
| **Minimum notice (minutes)** | How soon before the start an appointment can still be booked (60 by default). |
| **Book up to (days ahead)** | How far ahead appointments can be booked (60 by default). |
| **Most per resource per day** | The most appointments of this type one resource takes in a day. 0 for no limit. |
| **Hold a time for (minutes)** | How long a time stays held for someone who is still booking (10 by default). |
| **Bookings wait for someone to confirm them** | New bookings are *Requested* until someone confirms them. |
| **Customers can book it themselves** | Offer it on the public booking page and through the scheduling AI tools. |
| **Booking page address** | The end of the public booking page's address, after */book/*. |

Deleting an appointment type keeps its appointments.

## How open times are worked out

A time is offered when the whole appointment, with its free time before and after, fits:

- inside the resource's working hours (or shifts), less its days off;
- outside anything busy on the resource's calendar, and for a staff member, on their own calendars;
- after the minimum notice and before the booking limit;
- without going over the most per day, or for a group session, while places are left.

With **All resources together**, every resource must be free at that time.

## Book an appointment for a customer

1. Open **Calendars > Appointments** and choose **Book an appointment**, or choose **Book** next to an appointment type.
2. Choose the **Appointment type** and the **Day**, then **Show times**.
3. Pick a time, fill in the customer's **Name**, **Email**, **Phone** and **Notes**, and choose **Book**.

If someone else took the time a moment before you, you're told so and can pick another.

## Manage appointments

**Calendars > Appointments** lists appointments with a search by customer and a **Status** filter. **Open** shows an appointment with its time, resource, customer, place and notes, and these actions:

- **Confirm** a requested appointment.
- **Took place** or **No-show** after the time.
- **Move to another time on** a day: choose the **Day**, pick an open time, and choose **Move**.
- **Cancel appointment**, with an optional **Reason**.

The page also shows **The customer's manage link**, which you can send to the customer.

| Status | Means |
| --- | --- |
| **Held** | The time is held for a short while, such as while the customer finishes booking. |
| **Requested** | The customer asked for the time; someone has to confirm it. |
| **Confirmed** | The appointment is booked. |
| **Completed** | It took place. |
| **No-show** | The customer didn't come. |
| **Cancelled** | It was cancelled. |
| **Expired** | The hold ran out before the booking was finished. |

Appointments show on the calendar of the resource, tentative while held or requested. They can only be changed here, not by dragging them on the calendar.

## Let customers book online

With the **Scheduling Self-Service** feature on, every appointment type with **Customers can book it themselves** and a **Booking page address** gets a public page at */book/* followed by that address. Add it to your site's menu, or link to it from your pages.

On the page, the customer picks a **Day**, chooses **Show times**, picks a time (times are in the site's time zone), enters their **Name**, **Email**, **Phone** and **Notes**, and chooses **Book**. They land on a **Your appointment** page. Its address is their manage link: they can come back to it to move the appointment to another open time, or to cancel it. For an appointment type that waits for confirmation, the page says the request has been received.

Scheduling doesn't email customers by itself. To send a confirmation with the manage link, set up a workflow on **Appointment Confirmed** that sends an email; your administrator can do this, and the [Technical Manual](../../modules/scheduling.md#self-service) shows what to put in it.

## Waitlist

When a customer wants a time that's taken, add them to the waitlist. Open **Calendars > Waitlist**, choose the **Appointment type**, the **From** and **To** dates and times they can come, their **Name** and **Email**, and choose **Add**.

When an appointment of that type is cancelled or moved, the customer who has waited longest and can come at the freed time is marked *Notified*, and the **Waitlist Slot Available** workflow event starts, so a workflow can tell them. **Remove** takes a customer off the list.

## Copy scheduling to another site

Add the **Scheduling** step (under **Calendar**) to a deployment plan to take the appointment types and the staff, rooms and equipment that take appointments. Tick **Include appointments** to take every appointment too (with the customers' names, emails and phone numbers) and **Include the waitlist** for the waitlist. Add the **Calendars** step as well, with **Include events** when you take appointments, so the resources' calendars and the appointments' calendar events come along; put **Calendars** before **Scheduling** in the plan.

Importing creates what's missing and updates what's already there, matched by id. Customers' links to move or cancel an appointment aren't exported: an imported appointment keeps the link it already had on that site, or gets a new one.

## Workflows

With the **Workflows** feature on, the **Scheduling** category of the workflow editor has:

- **Events**: **Appointment Requested**, **Appointment Confirmed**, **Appointment Rescheduled**, **Appointment Cancelled**, **Appointment Completed**, **Appointment Marked No-Show**, **Reservation Hold Expired** and **Waitlist Slot Available**, and with Calendar Reminders, **Appointment Starting Soon** (set **Minutes before the start**). Each can be limited to some **Appointment types**.
- **Tasks**: **Find Appointment Slots**, **Book Appointment**, **Confirm Appointment**, **Reschedule Appointment**, **Cancel Appointment**, **Get Appointment** and **Add to Waitlist**.

For example: email the customer when an appointment is confirmed, send a text message an hour before it starts, or email a waitlisted customer when a time frees up.

## AI assistant

With **Scheduling** and **AI Services** both on, an AI assistant can list the appointment types customers can book, find open times, book an appointment, and move or cancel one. To move or cancel, the customer gives the code from their manage link, or the email or phone number they booked with. Only appointment types with **Customers can book it themselves** are offered to customers.

## Troubleshooting

- **No times are offered.** Check that the appointment type has **Resources**, that they **Take appointments**, that their working hours cover the day, and that the day is within the **Minimum notice** and **Book up to** limits. Something busy on the resource's calendar, or on a staff member's own calendar, also closes those times.
- **The booking page isn't found.** The appointment type needs **Can be booked**, **Customers can book it themselves** and a **Booking page address**, and the Scheduling Self-Service feature must be on.
- **A staff member with shifts is never offered.** Their **Shift calendar** must be chosen, and the **Shift** events there must name them as an attendee.
