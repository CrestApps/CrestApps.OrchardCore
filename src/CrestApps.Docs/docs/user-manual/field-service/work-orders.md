---
sidebar_label: Work Orders
title: Work Orders
description: "Set up the kinds of job you do, then create work orders, move them along, record the parts and time used, answer their checklists, get a signature and complete them, which invoices the customer."
technical_manual:
  - modules/work-orders
---

A work order is a job done for a customer, such as a repair or an installation. Each one is made from a work order type, which gives it what the job usually needs.

| | |
| --- | --- |
| **Menu** | Field Service > Work Orders, Work Order Types, Checklists, Service Levels, Sub-statuses |
| **Permission** | Manage work orders, or View work orders to see them; Manage work order settings for the types, checklists, service levels and sub-statuses |
| **Feature** | Work Orders; Service Agreements for recurring visits; Inventory for parts, Transactions for invoices, Geo for territories, Assets for equipment |

<AskYourAdmin />

## Set up a service level

A service level says how fast work orders are responded to and resolved.

1. Go to **Field Service > Service Levels** and select **Add service level**.
2. Enter a name and choose the time zone.
3. Under **Targets**, enter the hours to respond and to resolve for each priority. Responding means making it ready, scheduling it or starting it; resolving means completing it.
4. Choose when the warning comes, such as at 80% of a target.
5. Under **When the clock runs**, tick **Only in opening hours** and choose the days and hours, or leave it unticked for around the clock. Choose the statuses the clock stops in, such as *On hold*.
6. Select **Save**.

## Make a checklist

1. Go to **Field Service > Checklists** and select **Add checklist**.
2. Enter a name, then a row for each question: its section, the question, the kind of answer, whether it is required, and the choices (one per line), the range and the unit where they apply.
3. To ask a question only after a given answer, such as a photo of the damage only when a check fails, choose the earlier question under **Shown when** and type the answer, such as `Fail`. A new question can be chosen there once the checklist is saved.
4. Select **Save**.

A required question must be answered before the work order is completed.

## Add a work order type

1. Go to **Field Service > Work Order Types** and select **Add work order type**.
2. Enter a name, such as *Furnace repair*, the usual duration, how many people it takes and the usual priority, and choose its service level.
3. Under **Billing**, choose whether it isn't billed, is billed at a fixed price, or is billed for the time and materials used.
4. Under **Finishing the job**, choose whether the customer must sign and how many photos are needed.
5. Under **Who can do it**, choose the skills the job needs, with the lowest level, and the certifications the technician must hold. Dispatch offers only technicians who have them.
6. Tick its checklists, and list the lines every work order starts with, such as an hour of labor or a filter. With Inventory on, choose the stock item a part comes from.
7. Select **Save**.

Sub-statuses, under **Field Service > Sub-statuses**, are finer statuses inside one, such as *Waiting for parts* under *On hold*. A sub-status can stop the service level clock.

## Create a work order

1. Go to **Field Service > Work Orders**, select **Add work order** and choose its type.
2. Enter a summary, such as *No heat upstairs*, and change the priority if you need to.
3. Choose the customer, company or CRM contact it is for, when those can be picked, then enter the contact, the address and how to get in. A CRM contact chosen with no contact name typed fills in the name. With Geo on, click the map to place the job or drag the pin; without a pin, the address is placed on the map in the background. The place gives the work order its territory.
4. Choose when the visit can be, the assets it is for, and change the lines if this job needs others.
5. Select **Create**. The work order's page opens, with its number and the times it must be responded to and resolved by.

Search the list by number or summary, and filter it by status, type and priority. It shows the work orders still open at first.

## Work a work order

On a work order's page:

- **Move** it to another status, with a sub-status and a note. Canceling needs a note saying why.
- **Record what was used** on each line: how many, and for a counted part, which location it was taken from, such as the van. The parts come off that location's stock.
- Answer its **checklists** and select **Save answers**.
- **Record signature** with the name of who signed.
- Add a note. The history shows every change, newest first.

When the work order is in progress, select **Complete**. If something is missing, such as a required answer or the signature, the page says what. A billed work order then gets its invoice, linked under **About**.

To come back for more work, enter what it is for under **Follow-up** and select **Create follow-up**. The new work order is for the same customer, place and assets, and links back to this one.

## From bookings, calls and contacts

- **Bookings.** On a work order type, under **Bookings**, tick the appointment types whose bookings should make a work order of that type. Each confirmed booking then makes one for the customer who booked, with the technician arriving at the booked time; moving the booking moves the work order, and canceling it cancels the work order. With Dispatch on, those appointment types offer only the times a technician who can do the job is on shift and free, and a time is gone once enough bookings fill it.
- **Calls and messages.** In an Interaction Center subject flow, add the **Create Work Order** action to a disposition and choose the type. When an agent chooses that disposition, a work order is made for the contact, with the agent's notes, once per activity.
- **Contacts.** A contact's page lists their work orders. **New work order** starts one for them, with their name filled in.

## Recurring visits with a service agreement

A service agreement makes the work orders of a customer's recurring visits, such as a furnace service every six months.

1. Go to **Field Service > Service Agreements** and select **Add service agreement**.
2. Enter a name, the days it starts and ends, and the notice you want before the end.
3. Under **Visits**, add a row for each recurring visit: its name, the work order type, how often it repeats, how many days ahead its work order is made, and, when the visit is for only some of the assets covered, those assets. For anything else, choose **Custom rule** and enter an iCalendar rule, such as `FREQ=MONTHLY;BYDAY=2SU` for the second Sunday of each month.
4. Under **Pricing**, choose whether the visits are included, so their work orders aren't invoiced, or invoiced at a price per visit.
5. Enter the customer, contact, address and the assets covered, set the status to **Active** and select **Save**.

The agreement's page shows the next visits of each schedule and the work orders made so far. Work orders are made each hour; select **Make due work orders now** to make them at once. Each visit gets one work order, for its whole day. An agreement past its end date becomes expired; a workflow can start when one comes within its notice, such as to offer a renewal.

## Service levels on the page

The **Service level** card shows when the work order must be responded to and resolved by: *Due soon* after the warning, *Late* once missed, and *Met* or *Missed* when done. While the clock is stopped, it says since when. Workflows can start when a warning comes or a target is missed.
