---
sidebar_label: Help Desk
title: Run a Help Desk
description: Set up desks, categories, service levels, a portal, a request form and a knowledge base so customers and employees get answers, and agents work their tickets on lists and boards.
technical_manual:
  - modules/tickets
  - modules/knowledge-base
---

## Who it's for and what you get

For **administrators** and **desk managers** setting up a help desk for customers, employees or both, and the **agents** who work it.

When it is done:

- customers and employees send requests from their account on your site or a form on a page, and find answers on their own in the help center;
- each request becomes a ticket on the right desk, with the right priority and service level, and goes to a member of the desk's team;
- agents answer from the ticket list and boards, with reply templates, article suggestions and the AI assistant if you want it;
- managers watch response times, service levels and backlog in the reports.

## Before you start

| You need | Who sets it up |
| --- | --- |
| The features **Tickets**, **Tickets Portal**, **Ticket Web Form**, **Knowledge Base**, **Taxonomies** and **Reports**, and if you want them **Ticket Approvals**, **Time Tracker**, **ReCaptcha** and **Rate Limits** | Your administrator, in **Tools** → **Features** |
| Teams for the people who answer each desk | Your administrator; see [Teams](../workforce/teams.md) and [Roles and permissions](../getting-started/roles-and-permissions.md) |
| Roles with desk permissions, such as **View the tickets of my teams** and **Reply to tickets** on each desk, and **Manage ticket settings** for desk managers | Your administrator, under **Users** → **Roles** |

<AskYourAdmin />

## Steps

1. **Start from the recipe.** Run the **Help desk starter** recipe for business hours, a service level, a Support desk and an IT desk, an IT request type with categories, and example workflows. See [Start from the help desk recipe](../service-desk/help-desk-starter.md).
2. **Shape your desks.** Rename the desks or add your own, say who each serves, choose its team and how new tickets are assigned, and decide when solved tickets close and requesters are asked to rate them. See [Desks](../service-desk/desks.md).
3. **Name your statuses and priorities.** Keep the ones the site starts with or add your own, such as *Waiting for a third party*, and fill in each desk's priority matrix. See [Statuses and priorities](../service-desk/statuses-and-priorities.md).
4. **Set your targets.** Write a service level policy for tickets, with first response and resolution targets at each priority, and choose it on each desk. See [Service levels](../administration/service-levels.md).
5. **Ask for what you need.** Add fields to your ticket types, give them show-when rules, sort tickets into categories that route them, and mark the fields customers fill in. See [Ticket types, fields and categories](../service-desk/ticket-types-and-categories.md).
6. **Write the messages requesters get.** Edit the notification templates under **Settings** → **Service Desk**. See [Notification templates](../service-desk/notification-templates.md).
7. **Open the doors.** Let customers send requests from their account, and put a request form on your contact page. See [Tickets Portal](../service-desk/portal.md) and [Request form on a page](../service-desk/web-form.md).
8. **Write your first articles.** Add the help center's categories and articles for the questions you get most. See [Knowledge Base](../knowledge-base/index.md).
9. **Get the agents going.** Agents work the ticket list and boards, answer requesters and record their time. See [Work the ticket list](../service-desk/ticket-list.md), [Answer a ticket](../service-desk/answer-a-ticket.md) and [Boards](../service-desk/boards.md).
10. **Measure.** Use the ticket reports to see volume, response times and service levels. See [Service desk reports](../service-desk/reports.md).

## Check that it works

1. Sign in as a customer, go to **My account** → **Requests** and send a request. You should see it in your list with its number.
2. As an agent, find the ticket in **Service Desk** → **Tickets**, on the right desk and assigned as the desk says.
3. Reply to the requester. The customer should get a notification or an e-mail, and see the reply on the request's page.
4. Solve the ticket, and check the service-level targets show **Met** on it.
5. Open `/help` as a visitor and find a public article under its category.

## Tips

- **Start with one desk**, try it end to end, and add desks after.
- Keep internal desks, such as IT and HR, apart from customer desks with **Serves**, so customers never see them.
- Give agents **View the tickets of my teams** rather than **View all tickets** so each team sees its own work.
- Pin the boards you use every day to your menu with **Pin to menu**.
- To have agents answer from the AI's drafts, see [AI assistant](../service-desk/ai-assistant.md).
