---
sidebar_label: Agreements
title: Agreements with accounts
description: "Keep the support and maintenance agreements you have with accounts: their period, the hours they cover, the tickets and visits they cover, and how many hours are used and left."
---

An agreement is what you agreed with an account: a support contract with ten hours a month, a block of forty prepaid hours, a maintenance contract with a visit every six months, or all of these at once.

| You need | |
| --- | --- |
| **Feature** | Agreements (it turns on Accounts); Tickets for support agreements, Work Orders' Service Agreements for visits, Time Tracker for counting ticket time, Notifications for the warnings |
| **Permission** | **Manage agreements with accounts** to add and change them; **View agreements with accounts and their hours** to see them |
| **Menu** | **Agreements** |

## Add an agreement

1. Go to **Agreements** and select **Add agreement**, or select **New agreement** on the account's page.
2. Enter a name, choose the account, the days it starts and ends (leave the end empty when it runs until canceled), the notice you want before the end and the time zone its days and months are counted in.
3. Under **Covered hours**, choose **A block of hours** (used until it runs out) or **An allowance each month** (it starts again each month and what's left doesn't carry over), the hours, and what each entry of time is rounded up to (none, 6, 15 or 30 minutes).
4. Under **Tickets**, check **Covers the account's tickets**, and choose the desks and ticket types it covers (none for all of them) and the service level of its tickets.
5. Under **Visits**, add the recurring visits it covers, if any (see [Work Orders](../field-service/work-orders.md#recurring-visits-with-a-service-agreement)).
6. Set the status to **Active** and select **Save**.

Only a draft agreement can be deleted; cancel one that was in force instead. **Copy as a draft** starts a new agreement from an existing one.

## Hours used and left

The agreement's page shows how its hours stand in the current period: used, left, and anything past what it covers, which is billable (work goes on). What counts:

- The billable time logged on the tickets that work under it: time in the desk's ticket stage, not in its stage for time not billed, and not on a ticket whose billing type is not billable or no charge.
- The billable labor of the work orders done under it.

Each entry is rounded up as the agreement says. The account's page lists its agreements with the hours used, and a ticket's page shows its agreement with the hours left.

## Tickets and their agreement

A new ticket of the account on a desk and of a type the agreement covers works under it: the ticket's **Agreement** shows under **Planning**, and the agreement's service level runs the ticket before the account's own. An agent can choose another of the account's agreements, or none; the change is in the ticket's **History**.

## Reports

Under **Reports > Billing & Usage**, with **View agreements with accounts and their hours**:

| Report | Shows |
| --- | --- |
| **Agreement Usage** | For each active agreement with covered hours: its period, the hours covered, used and left, the overage, the share used, and when the hours ran out or are expected to run out at the pace so far; then where the hours went (tickets, work orders) |
| **Agreement Burn-down** | A chart of the hours left each day of the period against an even pace; choose an agreement to see its days |

Both show where each agreement stands on the last day you choose (today at the latest), in the period that holds that day. Choose one agreement in **Agreement** to see only it. **Account Health**, under **Reports > Executive**, puts each account's tickets, service levels, satisfaction and agreement hours side by side (see [Tickets](./tickets.md#reports)).

## Warnings

The account's manager (the owner on its account team) gets a notification when an agreement has used 80% and then all of its hours, once each month for an allowance, and when it is about to end. Agreements past their end date become expired.
