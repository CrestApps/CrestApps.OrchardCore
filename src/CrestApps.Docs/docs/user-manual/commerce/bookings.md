---
sidebar_label: Bookings
title: Bookings
description: "Sell tours, events and classes by date, rooms and rentals by the night, and appointments by the hour: seats or rooms per date, prices per person, deposits with the rest charged later, cancellation policies, requests you confirm first, travelers' names, a date search and a calendar."
technical_manual:
  - modules/bookings
  - modules/calendars
---

Sell what's booked for a date: a tour or a class with seats, a room or a rental by the night, or an appointment at a time. Each date has its own capacity, and a date that's full can't be bought.

| | |
| --- | --- |
| **Menu** | Commerce > Bookings |
| **Permission** | Manage bookings |
| **Feature** | Bookings (it needs Orders, Customers and Transactions) |

<AskYourAdmin />

To try it, run the **Bookings sample** recipe under **Configuration > Recipes**. It adds three products:

- An Alpine Lakes Tour with four departures and a deposit.
- A massage booked by the hour.
- A lakeside room booked by the night.

## Make a product a booking

Add the **Booking** part to a product's content type, then on the product, under **Booking**, choose **What's booked**:

- **A date with seats**, such as a tour, an event or a class. Choose **Manage dates** to add its departures, each with its seats and, if you like, its own price, such as a higher one in high season.
- **Nights**, such as a room or a rental. The buyer picks a check-in and a check-out, from the fewest to the most nights you allow. Friday and Saturday nights can have their own price.
- **A time**, such as an appointment. Tick the days you're open, when the first slot starts and the last one ends, and how long a slot is.

**Capacity** is how much each date holds: seats on a departure, rooms a night, or bookings in a time slot. **Days ahead** is how far ahead nights and times can be booked.

Tick **The price is per person** for a price such as a tour's, charged for everyone in the booking. Leave it unticked for a price per room, whoever is in it.

Under **Options**, add what a unit can be, such as a **Double room** for 2 people and a **Single room** for 1. Each option can add to the price once or take off it, such as a single supplement of 300 or a child's price of -150. A double room on a tour takes two seats.

An add-on can be priced **per person**, such as meals for each traveler, or as a **percent of the price**, such as travel insurance. Choose it under the product's **Add-ons**.

## Deposits

Enter a **Deposit (%)**, such as 15, and **Rest due (days before)**, such as 45. At checkout, the buyer can choose **Pay a deposit**: they pay that share now, and the rest is charged to the same card on its date. They can also pay it sooner from **Balances and invoices** in their account. A booking closer to its date than that is paid in full.

The booking's page shows the balance and when it's charged. **Charge the card now** charges it early. If the card is declined, it's tried again the next day, three times, and the buyer can always pay from the reminder's link.

## Cancellation policy

Under **Cancellation policy**, add a rule for each step, such as a 10% fee when canceled at least 60 days before, and 50% at least 30 days before. Closer than the last rule, nothing is refunded. With no rules, a booking can be canceled for a full refund.

Buyers see the policy on the product's page. To cancel a booking, open it under **Commerce > Bookings**. **Cancel** shows how many days before the date it is, what the policy keeps, and what's refunded of what was paid. Change the refund to give back more or less, add a reason, and choose **Cancel the booking**. Here's what happens:

- The money goes back to how they paid.
- What's still owed is let go.
- The date is free again.
- The buyer gets an email.

## Confirm each booking first

Tick **Confirm each booking before it's paid** for bookings you accept one by one. The buyer's cart then offers **Request to book** instead of checkout. They send a message, their dates are held, and the request shows under **Commerce > Bookings** as *Requested*. To accept it, open its order and choose **Send quote**. The buyer gets a link to pay, and the booking is confirmed when they do.

## Travelers

Tick **Ask for each traveler's details at checkout** to ask for each person's details, such as their name as it is on their passport. Add the fields to the **Traveler** content type under **Content Definition > Content Types**. The details show on the booking's page.

## Bookings

**Commerce > Bookings** lists bookings from today on. Filter by status, show any date, or search by buyer or product. **Calendar** shows each day's dates with what's taken.

A paid booking is **Confirmed**, and the buyer gets an email with what they booked and when the rest is due. After the date, mark it **Complete**, or **No-show** if they didn't come.

## Find a date

The **Find a date** page (*/bookings* on your site) lets shoppers enter dates and how many people. It lists what has room then: tours on those days, rooms for those nights, and times on the first day. Each links to its product with the date chosen. Add it to your menu to offer it.

## Bookings on the calendar

With the **Bookings Calendar** feature on (it needs Bookings and Calendars), every booking also goes on a **Bookings** calendar, next to the rest of your calendars:

- A held or requested booking shows as *Tentative*, and a booked one as *Confirmed*.
- When a booking moves to other dates, its event moves too. When it's canceled or its hold runs out, the event is cancelled.
- Each event shows the product and the buyer's name. It can't be changed on the calendar: change the booking under **Commerce > Bookings** instead.

The Bookings calendar is created with the first booking after the feature is turned on. It's private: calendar administrators see it, and they can share it with your staff under **Calendars > Manage Calendars**. See [Calendars](../calendars/calendars.md) for how to share and view calendars.

On **Commerce > Bookings > Calendar**:

- **Sync to calendar** adds every booking from today on to the Bookings calendar, or updates its event there. Use it once after turning the feature on, for bookings made before. Running it again does no harm.
- **Open in calendar** opens the Bookings calendar in the calendar view, on the same month. It appears once the Bookings calendar exists.

### Block dates with a calendar

With the feature on, a booking product's **Booking** settings have **Block with calendar**. Choose a calendar, such as a room's, a guide's or a vehicle's, and dates and times that are busy on it can't be booked: they show as full on the product's page and in **Find a date**. Tentative events count as busy.

- A time slot is closed when anything busy on the calendar overlaps it.
- A departure date or a night is closed when anything busy falls on that day.

For example, two tours that use the same boat can both be blocked with the boat's calendar, and a team's **Out of office** days close the dates its guide is away. The Bookings calendar itself isn't offered, since every booking is busy on it.

## Workflows

With the **Workflows** feature on, the **Commerce** category of the workflow editor has these booking events:

| Event | Starts when |
| --- | --- |
| **Booking Created** | A booking is made, whether it's held, requested or paid. |
| **Booking Confirmed** | A booking is booked, such as when its order is paid. |
| **Booking Rescheduled** | A booking moves to other dates. |
| **Booking Canceled** | A booking is canceled. |
| **Booking Completed** | A booking is marked **Complete**. |
| **Booking No-Show** | A booking is marked **No-show**. |
| **Booking Expired** | A hold or a request runs out. |

For example: text the buyer the meeting point the day a booking is confirmed, or alert the guide when a booking is canceled. The values each event hands to the next steps are listed in the [Technical Manual](../../modules/bookings.md#workflow-events).
