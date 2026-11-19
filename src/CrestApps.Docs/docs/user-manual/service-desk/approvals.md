---
sidebar_label: Approvals
title: Ask for Approval Before Work Starts
description: "Make a ticket type wait for up to three approval steps, such as a manager's and then finance's, before work starts."
technical_manual:
  - modules/tickets/approvals
---

Some requests need a yes before work starts, such as a manager's and then finance's for new equipment.

| | |
| --- | --- |
| **Menu** | Design > Content Definition > Content Types, then the ticket type's **Ticket** part |
| **Permission** | Edit content types; **Cancel ticket approvals** to call one off |
| **Feature** | Ticket Approvals |

<AskYourAdmin />

## Set it up

1. Turn on **Ticket Approvals** under **Tools > Features**.
2. Under **Service Desk > Statuses**, add a status such as *Waiting for approval*, in the **Pending** or **On hold** category.
3. Under **Content Definition**, edit the ticket type's **Ticket** part and fill in **Approval**: up to three steps, each with a name (such as *Manager* or *Finance*) and its approvers, or **The requester's manager** (from the requester's staff record, with Staff on).
4. Choose the **Status while waiting**, such as *Waiting for approval*.
5. Save.

## What happens

- A new ticket of that type waits. Each approver of the current step is told in their notifications and opens the request's approval page, even if they can't see the ticket otherwise. Agents see the steps on the ticket.
- One approver per step decides: **Approve** moves to the next step, and after the last the ticket goes back to work.
- **Refuse** needs a comment, which the requester reads as a reply; the ticket is then solved.
- While a ticket waits, its status can't be changed any other way, and a reply from the requester doesn't reopen it.
- A step with no approver (a guest has no manager) is skipped, and the ticket's history says so.
- If an approver can't decide (they left, say), someone with **Cancel ticket approvals** opens the ticket and uses **Cancel the approval** with a reason; the ticket goes back to work.
