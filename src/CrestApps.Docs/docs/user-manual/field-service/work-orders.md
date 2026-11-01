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
| **Feature** | Work Orders; Inventory for parts, Transactions for invoices, Geo for territories, Assets for equipment |

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
3. Select **Save**.

A required question must be answered before the work order is completed.

## Add a work order type

1. Go to **Field Service > Work Order Types** and select **Add work order type**.
2. Enter a name, such as *Furnace repair*, the usual duration, how many people it takes and the usual priority, and choose its service level.
3. Under **Billing**, choose whether it isn't billed, is billed at a fixed price, or is billed for the time and materials used.
4. Under **Finishing the job**, choose whether the customer must sign and how many photos are needed.
5. Tick its checklists, and list the lines every work order starts with, such as an hour of labor or a filter. With Inventory on, choose the stock item a part comes from.
6. Select **Save**.

Sub-statuses, under **Field Service > Sub-statuses**, are finer statuses inside one, such as *Waiting for parts* under *On hold*. A sub-status can stop the service level clock.

## Create a work order

1. Go to **Field Service > Work Orders**, select **Add work order** and choose its type.
2. Enter a summary, such as *No heat upstairs*, and change the priority if you need to.
3. Enter the contact, the address and how to get in. With Geo on, the address is placed on the map in the background and gives the work order its territory.
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

## Service levels on the page

The **Service level** card shows when the work order must be responded to and resolved by: *Due soon* after the warning, *Late* once missed, and *Met* or *Missed* when done. While the clock is stopped, it says since when. Workflows can start when a warning comes or a target is missed.
