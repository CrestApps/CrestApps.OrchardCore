---
sidebar_label: Tickets
title: Tickets
description: "Answer requests from customers and employees as tickets: desks, statuses and priorities, replies and internal notes, assignment, service-level targets, saved views, boards, merge and links, reports and the account tab."
technical_manual:
  - modules/tickets
---

A **ticket** is a request someone made: a customer who can't sign in, an employee who needs a laptop. Each ticket belongs to a **desk**, such as Support, IT or HR, which decides who may see it. Agents reply to the requester, write internal notes for each other, and move the ticket through its statuses until it is solved.

| | |
| --- | --- |
| **Menu** | **Service Desk**: Tickets and Boards, and for those who manage the service desk, Desks, Statuses, Priorities, Views and E-mails |
| **Permission** | A view permission on a desk, such as **View the tickets of my teams in Support**; **Manage ticket settings** for the settings; **Manage shared ticket boards** or **Manage my own ticket boards** to make boards; **View ticket reports** for the reports |
| **Feature** | Tickets |

<AskYourAdmin />

## Work the ticket list

Go to **Tickets**. The list shows the tickets you may see, latest change first.

- Pick a **saved view** above the list, such as **My open tickets** or **Unassigned**.
- Search by ticket number or words of the subject.
- Narrow the list by state, status, priority, assignee, team, desk, ticket type or service level (**Past a target**, **Due within 4 hours**), and sort it.
- Select tickets to **Assign to me**, set a status, **Merge** them into the oldest one, or delete them. Each ticket is checked on its own: one you may not change is left as it was, with a message.

## Work tickets on a board

A board shows tickets as cards in columns, one column for each stage of the work, such as **New**, **Open**, **Pending** and **Solved**. Go to **Service Desk → Boards** and open a board.

- **Drag a card** to another column to change the ticket's status. When the column holds several statuses and has no default, you're asked which one.
- To move a card without dragging (with the keyboard, or on a phone), select **⋮** on the card and choose the status.
- If the move isn't allowed, for example because the ticket is closed or waiting for approval, the card goes back and the board says why.
- Search by number or subject, or show only **My tickets** or **Unassigned tickets**. The board keeps up by itself: when someone changes a ticket's status elsewhere, its card moves within half a minute.
- A column's count turns red when it holds more tickets than its limit. A column shows at most 50 cards, with how many there are in all.

A board shows only the tickets you may already see, whoever made it.

### Make a board

1. Go to **Service Desk → Boards** and select **Add board**.
2. Enter a **Name** and, if you want, a **Description**.
3. Choose **Who it is for**: **Only me**, **A team**, or **Everybody who works on tickets**. Sharing a board needs **Manage shared ticket boards**; with **Manage my own ticket boards** you can keep boards for yourself.
4. Under **Which tickets it shows**, choose desks, teams and ticket types to keep the board to, say, IT; leave them empty to show everything you may see. Choose whose tickets and the order of the cards.
5. Shape the **Columns**. A new board starts with a column per status category. Rename columns, tick the statuses each one holds (a status can be in one column only), choose what a card dropped on a column with several statuses gets (**Ask which status**, or one of them), and set a limit if you want. Add columns with **Add column**, remove them with the bin, and drag the handle or use the arrows to put them in order.
6. Select **Save**.

To change a board later, open it and select **Edit board**. A board you share can be changed by anyone with **Manage shared ticket boards**; a personal board only by you.

## Open a ticket for someone

1. Select **New ticket**, or **New ticket** on an account's **Tickets** tab.
2. Choose who it is **for**, or enter a guest's name and e-mail.
3. Enter the **Subject** and the first message, and attach files if you need to.
4. Choose the **Desk** and, if you want, a **Priority**.
5. Select **Publish**.

## Answer a ticket

Open a ticket from the list. Its thread is on the left, newest first; its values, the requester, the service levels and the links are beside it.

- Write in the composer and choose **Reply to the requester** (they are e-mailed when the desk e-mails requesters) or **Internal note** for the agents only. Mention a colleague with `@username` to tell them.
- Choose **Then set the status to** to send and move the ticket in one step, such as **Waiting for the requester**.

The **Conversation** shows what people wrote: replies and internal notes. Every change to the ticket, such as its status, assignee or priority, is listed under **History** below it.
- Pick a **Reply template** to start from a saved answer; it can also set a status, priority or team.
- Change the status, priority, desk, team or assignee in the ticket's values, or select **Assign to me**.
- Use **Actions** to run an action your administrator set up, such as **Escalate to tier 2**.

While you have a ticket open, a note above it says who else has it open and who is writing on it, so two people don't answer the same ticket. When someone changes the ticket while you have it open, the note says so; select **Reload** to see the change before you answer.

A reply from the requester on a solved ticket reopens it within the desk's reopen window. A closed ticket is read only; a reply after it opens a follow-up ticket.

## Merge and link tickets

- **Merge** moves a duplicate into another ticket by its number. The duplicate becomes read only and points to the ticket it was merged into.
- **Links** relate a ticket to another ticket or to any record, such as an order or a work order, with a relation such as **Related** or **Duplicate**.

## Route tickets through the Contact Center

Turn on **Tickets for Contact Center**, then edit a desk and choose its **Contact Center queue**. New tickets on that desk that nobody has yet are offered to the queue's available agents, by their skills and how busy they are; the agent who accepts one gets the ticket. If someone takes the ticket by hand, or it's solved first, the offer is called off. Agents accept offers in the agent desktop.

## Record your time on a ticket

When your administrator has chosen a **Time on tickets** stage for the desk, the ticket has a **Time** panel. Select **Start working on this ticket** when you pick it up and **Stop working on this ticket** when you're done; your time goes back to what you were doing before. The panel shows how long each person worked on the ticket, and the time is part of your day in **My time**.

Forgot the timer? Select **Log time**, enter the **Time spent** (`30`, `45m`, `1h 30m` or `1:30`), when you **Started** (leave it empty if you just finished) and a **Note** on what you did, then select **Log time**. If you were clocked in, the time moves from what you were doing to the ticket, so your day still adds up. When the desk also has a stage for **Time not billed to the customer**, clear **Billable** for time the customer isn't charged for. **Entries** lists every entry on the ticket with its note. To correct or remove an entry, ask someone who may edit everyone's time; they use **Time entries** in Time Tracker.

## Send someone on site

When a request needs a visit, choose a type (and, if you like, a priority) under **Work order** on the ticket and select **Create a work order**. The work order gets the ticket's subject, first message, account and requester, and shows under **Links**. When the work order is completed or canceled, the ticket's conversation says so, as an internal note, so you know to answer the requester. You need the **Manage work orders** permission, and Work Orders must be on.

## Problems and incidents

When many people report the same outage, open one ticket for the cause and link each report to it with **Its problem** under **Links**. The cause's page lists its incidents, and solving it solves every open incident, telling each requester.

## Service levels

A desk's service-level policy sets how soon a ticket must first be answered and how soon it must be solved, at each priority, counted in the desk's business hours. The ticket shows each target with **Due soon**, **Late**, **Met** or **Missed**. A status that waits for the requester can pause the clock. See [Service Levels](service-levels.md).

## Set up desks, statuses, priorities and views

Under the **Service Desk** menu:

| Page | What you set |
| --- | --- |
| **Desks** | Each desk's name, audience (customers or internal), business hours, service-level policy, default team, ticket types, reopen window, how many days after solving a ticket closes, how new tickets are assigned (nobody, the member with the fewest open tickets, or each member in turn) and who is e-mailed or told |
| **Statuses** | The statuses, each in a category (New, Open, Pending, On hold, Solved, Closed), with the label requesters see, a color and whether it pauses the service-level clock |
| **Priorities** | The priorities and their order |
| **Views** | Shared saved views of the list |
| **E-mails** | The e-mails requesters get when a ticket is received, answered and solved |

The permissions to view and work tickets exist for each desk, so HR tickets can stay with HR. Give a role **View all tickets**, **View the tickets of my teams** or **View the tickets assigned to me** on a desk to decide what its users see there.

## The account tab

An account's page has a **Tickets** tab: its latest tickets you may see, how many are open, **New ticket** for the account, and **View all in the ticket list**, which opens the ticket list showing only that account's tickets, in every state. Remove the **Account** filter there to see every ticket again.

## Sort tickets into categories

Categories say what a ticket is about, such as Hardware > Laptop or Software > Access request. People choose one when they open a ticket, one level at a time, and the category can send the ticket to the right desk and team with the right priority and service level. Categories come from a taxonomy, so you edit them like any other list of terms.

1. Turn on **Taxonomies**, open **Content > Content Items**, choose **New** and **Taxonomy**, and create one, such as *IT categories*, with **Ticket category** as its term type. Add its categories, and sub-categories under them.
2. Edit a category to set what its new tickets start with: **Desk**, **Team**, **Priority** and **Service-level policy**. Leave a value empty to take it from the category above. Tick **Only agents can choose it** for categories customers shouldn't see, and write a **Description for customers** to tell them what to have ready. Changes to categories take effect once you **Publish** the taxonomy.
3. Open **Content Definition > Content Types**, edit the ticket type and, on its **Ticket** part, choose the taxonomy under **Categories**. Tick **A category is required** to make people choose one, and **Only the most specific category** so they pick Hardware > Laptop rather than Hardware.

What a ticket starts with comes first from what the agent chose on the form, then from its category, then from the ticket type. A customer's request moves to the category's desk only when that desk serves customers too.

When an agent changes a ticket's category later, only the category changes. If the new category sends tickets elsewhere, the ticket's panel shows **Apply the category's routing**, with what it would change; click it to move the ticket.

## Ask the questions that apply

A ticket type's fields can show only when they apply: "Laptop model" only for laptop problems, "Who else is affected?" only when someone ticks "Is this an outage?". Edit the field in the ticket type and fill in **Show this field when**, with a condition on the category (**is or is under** covers its sub-categories) or on another field. See [Show a field only when it applies](./content-fields#show-a-field-only-when-it-applies).

The questions appear as people choose the category and answer, on the agents' **New ticket** page, in the portal and on the web form. When the ticket is sent, the site checks the conditions again and keeps nothing a hidden question received.

The **Help desk starter** recipe sets up an example: an **IT categories** taxonomy and an **IT request** type that asks for the laptop model for laptop problems, which system for access requests, and who else is affected for an outage.

## Let customers open requests on the site

Turn on **Tickets Portal** under **Configuration > Features**. Then:

1. Under **Content Definition**, edit the ticket type's **Ticket** part and tick **Customers can submit it**. Add a **Description for customers** so they can tell types apart.
   Customers see the subject and only the fields you mark **Customers fill this in** (in each field's settings); every other field stays your agents' own.
2. Make sure a desk **Serves** customers (**Service Desk > Desks**). Customers can only open requests on those desks. With Staff on, members of staff also see the desks that serve people inside the organization, so employees can ask IT or HR from the same page.

Signed-in people now find **Requests** under **My account**: their requests, **New request**, and each request's conversation with a reply box. They see your public replies, never your internal notes. Someone without a login follows **View your request** in their e-mails to read and answer their request. An account's administrators also see the account's requests.

To learn how you did, set **Ask requesters to rate a solved ticket after (hours)** on a desk. The requester of a solved ticket then gets one e-mail with a link to rate it from 1 to 5, and can also rate it on the request's page. Ratings show on the ticket's timeline and in the **Tickets** data set for reports.

## Let the AI assistant help

With AI turned on, edit a desk and choose an **AI assistant**: a chat AI profile with the instructions and knowledge your agents should answer from. Tickets on that desk then show an **AI assistant** panel:

- **Suggest a reply** writes a reply into the reply box from the conversation. Read it, change it, then send it; nothing is sent until you do, and it stays a public reply or an internal note as you had chosen.
- **Triage** adds an internal note with a short summary and a suggested priority. Tick **A triage sets the priority it suggests** on the desk to have it change the priority too. It may lower the priority, or raise it at most one step above the ticket type's default; anything higher is only noted for an agent, so a requester can't talk their way to the top.

The assistant sends the ticket's conversation, internal notes and names included, to the AI service of the profile you chose. Pick a service whose data terms fit what your tickets hold.

To triage every new ticket, add the **Triage Ticket With AI** task to a workflow that starts with **Ticket Created**.

## Ask for approval before work starts

Turn on **Ticket Approvals** under **Configuration > Features**. Then, under **Content Definition**, edit the ticket type's **Ticket** part and fill in **Approval**: up to three steps, each with a name (such as *Manager* or *Finance*) and its approvers, or **The requester's manager**. Choose a **Status while waiting**, such as a *Waiting for approval* status you add under **Service Desk > Statuses**.

A new ticket of that type then waits. Each approver of the current step is told in their notifications and opens the request's approval page; agents see the steps on the ticket. One approver per step decides: **Approve** moves to the next step, and after the last the ticket goes back to work. **Refuse** needs a comment, which the requester reads; the ticket is then solved. While a ticket waits, its status can't be changed any other way. If an approver can't decide (they left, say), an administrator opens the ticket and uses **Cancel the approval** with a reason; the ticket goes back to work.

## Put a request form on a page

Turn on **Ticket Web Form**, then add a **Ticket Form** widget where visitors should find it (a layer, or a page's flow). Choose the ticket type and the desk its tickets go to, and, if you like, what visitors read after sending. Visitors give their request, choose a category when the ticket type has them, answer the questions customers fill (the same ones as in the portal, appearing as they answer), give a subject unless the type has its own title, and give their name and e-mail when they aren't signed in. A category's desk can take over from the widget's desk. Turn on **ReCaptcha** and set its keys under **Settings > Security** so guests have to pass a challenge; the widget warns you until you do. Turn on **Rate Limits** too, to cap how many requests one visitor can send. A guest's e-mail address isn't confirmed, so the e-mails they get only give the request's number, never what they wrote.

## Reports

Under **Reports**, with **View ticket reports**:

| Report | Shows |
| --- | --- |
| **Ticket Volume and Backlog** | Tickets created and solved each day, the open, overdue and unassigned tickets now, how old the open ones are, and the backlog at the start of each day |
| **Ticket Response and Resolution Times** | How soon tickets got a first response and how long they took to solve, by priority, and how many were solved on first contact |
| **Ticket Service Level Compliance** | For each target and for each account, how many ended in the period, met or missed, and how many running now are already late |
| **Ticket Agent and Team Performance** | For each assignee and team: tickets solved, the time they took, how often they were reopened or escalated, and open tickets now |
| **Ticket Distribution** | Tickets by channel, desk, ticket type, priority and where they stand now |
| **Ticket Time** | With Time Tracker: the time logged on tickets, billable and not billed, by person, account, desk and ticket type, and the tickets with the most time |
| **Ticket Resolution and Work Time** | With Time Tracker: for solved tickets, how long they took against how long people worked on them (work time) and everyone's time added up (effort), people per ticket and the touch ratio |

Each report counts only the tickets you may see. To build your own, use the **Tickets** data source in the [Report Builder](../report-builder/index.md).

## Start from the help desk recipe

Run the **Help desk starter** recipe (**Configuration > Recipes**) to set up weekday business hours, a standard service level, the Support desk and an IT desk, an **IT request** type with **IT categories** and questions that follow them, and two example workflows that start turned off: one escalates a ticket that misses a target, the other adds a note when a requester reopens a solved ticket.
