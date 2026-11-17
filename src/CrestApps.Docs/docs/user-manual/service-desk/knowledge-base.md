---
sidebar_label: Knowledge Base
title: Knowledge Base
description: "Publish help articles at /help: write them, sort them into categories, choose who can read each one, and see what readers found helpful; agents and requesters get them as suggestions."
technical_manual:
  - modules/knowledge-base
---

The **Knowledge Base** is your help center: articles that answer common questions, at `/help` on your site. Customers find answers on their own, and agents link articles in their replies.

| | |
| --- | --- |
| **Menu** | Service Desk > Knowledge Base > Articles; Service Desk > Knowledge Base > Help center |
| **Permission** | **View staff-only knowledge articles** for the menu and staff articles; the content permissions of the Knowledge Article type to write articles |
| **Feature** | Knowledge Base |

<AskYourAdmin />

## Write an article

1. Turn on **Knowledge Base** under **Tools > Features**.
2. Go to **Service Desk > Knowledge Base > Articles** and select **New Knowledge Article**.
3. Give it a **Title**.
4. Choose **Who can read it**: everyone, people who are signed in, or staff only.
5. Write a short **Summary**: one or two sentences shown in lists, searches and suggestions. Search looks at the title and the summary.
6. Choose its **Category** to list it with similar articles.
7. Write the body in the editor. Its toolbar formats text, adds links, lists, tables and pictures, and shows a preview.
8. The **Permalink** is filled in from the title, under `help/`, such as `help/reset-your-password`. Change it if you want another address.
9. Publish it, or save a draft to finish later.

## Who can read it

| Choice | Who sees the article |
| --- | --- |
| **Everyone** | Anyone, signed in or not |
| **People who are signed in** | Anyone with an account who is signed in |
| **Staff only** | People with the **View staff-only knowledge articles** permission |

The choice holds everywhere the article shows: the help center, searches, suggestions and its own page. Give **View staff-only knowledge articles** to the roles that should read staff articles, under **Users > Roles**. The same permission shows **Knowledge Base** in the **Service Desk** menu.

## Categories

The categories are the terms of the **Knowledge Base Categories** taxonomy. To add, rename or nest them:

1. Go to **Content > Taxonomies** and open **Knowledge Base Categories**.
2. Add its terms, such as *Accounts* and *Billing*. A category can sit under another one.
3. Publish the taxonomy.

A category's page lists its articles and those of the categories under it.

## The help center

Visitors find the articles grouped by category at `/help`, each category with its most helpful articles and a link to the rest, beside the list of categories and how many articles each holds. Each category has its own page with all its articles, and a search box that searches within it. Visitors only ever see the articles they may read.

Each article ends with **Was this helpful?**. The answers show how well it works, and the most helpful articles come first.

## Suggestions in tickets

With Tickets on, matching articles show as suggestions while people write:

- requesters see them on the **New request** form in their account, so they may find the answer before they send;
- agents see them under the reply box, and select one to add its link to their reply. A public reply suggests only articles the requester can open; an internal note suggests every article the agent may read.

See [Answer a ticket](./answer-a-ticket.md) and [Tickets Portal](./portal.md).
