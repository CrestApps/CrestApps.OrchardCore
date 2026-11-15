---
sidebar_label: Tickets
title: Tickets
description: "Answer requests from customers and employees as tickets: desks, statuses and priorities, replies and internal notes, assignment, service-level targets, saved views, merge and links, reports and the account tab."
technical_manual:
  - modules/tickets
---

A **ticket** is a request someone made: a customer who can't sign in, an employee who needs a laptop. Each ticket belongs to a **desk**, such as Support, IT or HR, which decides who may see it. Agents reply to the requester, write internal notes for each other, and move the ticket through its statuses until it is solved.

| | |
| --- | --- |
| **Menu** | Tickets; the settings under **Configuration > Tickets** (Desks, Statuses, Priorities, Views, E-mails) |
| **Permission** | A view permission on a desk, such as **View the tickets of my teams in Support**; **Manage ticket settings** for the settings; **View ticket reports** for the reports |
| **Feature** | Tickets |

<AskYourAdmin />

## Work the ticket list

Go to **Tickets**. The list shows the tickets you may see, latest change first.

- Pick a **saved view** above the list, such as **My open tickets** or **Unassigned**.
- Search by ticket number or words of the subject.
- Narrow the list by state, status, priority, assignee, team, desk, ticket type or service level (**Past a target**, **Due within 4 hours**), and sort it.
- Select tickets to **Assign to me**, set a status, **Merge** them into the oldest one, or delete them. Each ticket is checked on its own: one you may not change is left as it was, with a message.

## Open a ticket for someone

1. Select **New ticket**, or **New ticket** on an account's **Tickets** tab.
2. Choose who it is **for**, or enter a guest's name and e-mail.
3. Enter the **Subject** and the first message, and attach files if you need to.
4. Choose the **Desk** and, if you want, a **Priority**.
5. Select **Publish**.

## Answer a ticket

Open a ticket from the list. Its thread is on the left, newest first; its values, the requester, the service levels and the links are beside it.

- Write in the composer and choose **Reply to the requester** (they are e-mailed when the desk e-mails requesters) or **Internal note** for the agents only. Mention a colleague with `@username` to tell them.
- Choose a **Status after sending** to send and move the ticket in one step, such as **Waiting for the requester**.
- Pick a **Reply template** to start from a saved answer; it can also set a status, priority or team.
- Change the status, priority, desk, team or assignee in the ticket's values, or select **Assign to me**.
- Use **Actions** to run an action your administrator set up, such as **Escalate to tier 2**.

A reply from the requester on a solved ticket reopens it within the desk's reopen window. A closed ticket is read only; a reply after it opens a follow-up ticket.

## Merge and link tickets

- **Merge** moves a duplicate into another ticket by its number. The duplicate becomes read only and points to the ticket it was merged into.
- **Links** relate a ticket to another ticket or to any record, such as an order or a work order, with a relation such as **Related** or **Duplicate**.

## Problems and incidents

When many people report the same outage, open one ticket for the cause and link each report to it with **Its problem** under **Links**. The cause's page lists its incidents, and solving it solves every open incident, telling each requester.

## Service levels

A desk's service-level policy sets how soon a ticket must first be answered and how soon it must be solved, at each priority, counted in the desk's business hours. The ticket shows each target with **Due soon**, **Late**, **Met** or **Missed**. A status that waits for the requester can pause the clock. See [Service Levels](service-levels.md).

## Set up desks, statuses, priorities and views

Under **Configuration > Tickets**:

| Page | What you set |
| --- | --- |
| **Desks** | Each desk's name, audience (customers or internal), business hours, service-level policy, default team, ticket types, reopen window, how many days after solving a ticket closes, how new tickets are assigned (nobody, the member with the fewest open tickets, or each member in turn) and who is e-mailed or told |
| **Statuses** | The statuses, each in a category (New, Open, Pending, On hold, Solved, Closed), with the label requesters see, a color and whether it pauses the service-level clock |
| **Priorities** | The priorities and their order |
| **Views** | Shared saved views of the list |
| **E-mails** | The e-mails requesters get when a ticket is received, answered and solved |

The permissions to view and work tickets exist for each desk, so HR tickets can stay with HR. Give a role **View all tickets**, **View the tickets of my teams** or **View the tickets assigned to me** on a desk to decide what its users see there.

## The account tab

An account's page has a **Tickets** tab: its latest tickets you may see, how many are open, and **New ticket** for the account.

## Let customers open requests on the site

Turn on **Tickets Portal** under **Configuration > Features**. Then:

1. Under **Content Definition**, edit the ticket type's **Ticket** part and tick **Customers can submit it**. Add a **Description for customers** so they can tell types apart.
2. Make sure a desk **Serves** customers (**Tickets > Desks**). Customers can only open requests on those desks.

Signed-in people now find **Requests** under **My account**: their requests, **New request**, and each request's conversation with a reply box. They see your public replies, never your internal notes. Someone without a login follows **View your request** in their e-mails to read and answer their request. An account's administrators also see the account's requests.

To learn how you did, set **Ask requesters to rate a solved ticket after (hours)** on a desk. The requester of a solved ticket then gets one e-mail with a link to rate it from 1 to 5, and can also rate it on the request's page. Ratings show on the ticket's timeline and in the **Tickets** data set for reports.

## Put a request form on a page

Turn on **Ticket Web Form**, then add a **Ticket Form** widget where visitors should find it (a layer, or a page's flow). Choose the ticket type and the desk its tickets go to, and, if you like, what visitors read after sending. Visitors give a subject and their request, and their name and e-mail when they aren't signed in. Turn on **ReCaptcha** and set its keys under **Settings > Security** to add a challenge.

## Reports

Under **Reports**, with **View ticket reports**:

| Report | Shows |
| --- | --- |
| **Ticket Volume and Backlog** | Tickets created and solved each day, the open, overdue and unassigned tickets now, how old the open ones are, and the backlog at the start of each day |
| **Ticket Response and Resolution Times** | How soon tickets got a first response and how long they took to solve, by priority, and how many were solved on first contact |
| **Ticket Service Level Compliance** | For each target, how many ended in the period, met or missed, and how many running now are already late |
| **Ticket Agent and Team Performance** | For each assignee and team: tickets solved, the time they took, how often they were reopened or escalated, and open tickets now |
| **Ticket Distribution** | Tickets by channel, desk, ticket type, priority and where they stand now |

Each report counts only the tickets you may see. To build your own, use the **Tickets** data source in the [Report Builder](../report-builder/index.md).

## Start from the help desk recipe

Run the **Help desk starter** recipe (**Configuration > Recipes**) to set up weekday business hours, a standard service level, the Support desk and an IT desk, and two example workflows that start turned off: one escalates a ticket that misses a target, the other adds a note when a requester reopens a solved ticket.
