---
sidebar_label: Answer a Ticket
title: Answer a Ticket
description: "Reply to the requester or write internal notes, use reply templates and article suggestions, attach files, change the ticket's values and details, plan the work, and see who else is on the ticket."
technical_manual:
  - modules/tickets
  - modules/tickets/assignment-and-life-cycle
---

Open a ticket from the list. Its thread is on the left, newest first; its values, the requester, the service levels and the links are beside it.

| | |
| --- | --- |
| **Menu** | Service Desk > Tickets, then a ticket |
| **Permission** | **Reply to tickets** to answer the requester, **Add internal notes to tickets** for internal notes, **Edit tickets** to change its values, **Assign tickets** to assign it, on the ticket's desk |
| **Feature** | Tickets |

<AskYourAdmin />

## Write a reply or a note

- Write in the composer and choose **Reply to the requester** (they are told when the desk tells requesters) or **Internal note** for the agents only. Mention a colleague with `@username` to tell them.
- Choose **Then set the status to** to send and move the ticket in one step, such as **Waiting for the requester**.
- Pick a **Reply template** to start from a saved answer; it can also set a status, priority or team.
- With the Knowledge Base on, articles that match what you write show as suggestions under the reply box. Select one to add its link to your reply. A public reply suggests only articles the requester can open; an internal note suggests every article you may read. See [Knowledge Base](./knowledge-base.md).
- Attach files by dragging them onto the reply box, pasting them, or selecting **browse**. Files you add later join the ones already there, and the × next to a file removes it.
- With the AI assistant on the desk, **Suggest a reply** writes a draft for you. See [AI assistant](./ai-assistant.md).

## Read the ticket

The **Conversation** shows what people wrote: replies and internal notes. Every change to the ticket, such as its status, assignee or priority, is listed under **History** below it.

**Files**, between the two, lists every file on the ticket in one place, newest first: those of the requester's messages, your replies and notes, files that came by e-mail, the API or the web form, and those of the tickets merged into it. Each shows a preview for a picture (or the kind of file), its size, who added it and when. Select the name to download it, or **In a message** / **In an internal note** to jump to the message it came with. Files of internal notes carry the **Internal** label and never show to the requester; a redacted message's files don't show at all. A ticket with many files shows the newest 12: select **Show all** to see the rest. Requesters see the files of their request's public messages the same way on the request's page in their account.

When a reply's e-mail hasn't reached the requester yet, the reply shows a "not delivered" notice with the reason. It goes away once the e-mail is sent.

## Change the ticket

- Change the status, priority, desk, team or assignee in the ticket's values, or select **Assign to me**.
- Change the **Category**. Only the category changes; if the new category sends tickets elsewhere, the panel shows **Apply the category's routing**, with what it would change. Click it to move the ticket.
- Use **Actions** to run an action your administrator set up, such as **Escalate to tier 2**.

## The Details card

The **Details** card holds what the ticket's type asks for: its subject and the type's own fields, such as the laptop model or whether others can't work either. If you may edit the ticket, change them there and select **Save**; a value that isn't right is pointed out in the card. Questions that depend on the category or another answer show only when they apply, as on the new ticket form. If you may only read the ticket, you see the values, and **Not set** for a question nobody answered.

## Plan the work

Under **Planning**:

- set the **Impact** (how much of the business it touches) and **Urgency** (how soon it must be dealt with). When the desk has a priority matrix, impact and urgency set the priority. See [Statuses and priorities](./statuses-and-priorities.md#the-priority-matrix).
- set when work should start and how long it should take;
- choose the **Billing type**: billable, covered by the agreement, no charge or not billable;
- with [agreements](../administration/agreements.md) on, **Agreement** is the account's agreement the ticket works under: a new ticket takes the one that covers it. The ticket's time counts against that agreement's hours, and the **Agreement** card beside the ticket shows how many are left.

Once the ticket is solved, the panel says who solved it.

## Who else is on the ticket

While you have a ticket open, a note above it says who else has it open and who is writing on it, so two people don't answer the same ticket. When someone changes the ticket while you have it open, the note says so; select **Reload** to see the change before you answer.

## Solved, reopened and closed

- A reply from the requester on a solved ticket reopens it within the desk's reopen window.
- A desk closes solved tickets a number of days after they were solved. See [Desks](./desks.md).
- A closed ticket is read only; a reply after it opens a follow-up ticket.

The ticket's service-level targets show beside it with **Due soon**, **Late**, **Met** or **Missed**. A status that waits for the requester can pause the clock. See [Service levels](../administration/service-levels.md).
