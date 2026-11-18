---
sidebar_label: Write Articles
title: Write Knowledge Base Articles
description: "Write a help article, choose its category and who can read it, give it a short summary, and publish it at its own address under /help."
technical_manual:
  - modules/knowledge-base
---

Help articles answer the questions readers ask most. Each one has a title, a summary, a category, a body and its own address under `/help`.

| | |
| --- | --- |
| **Menu** | Knowledge Base > Articles |
| **Permission** | The content permissions of the Knowledge Article type (for example **Edit Knowledge Article** and **Publish Knowledge Article**) |
| **Feature** | Knowledge Base |

<AskYourAdmin />

## Write an article

1. Go to **Knowledge Base > Articles** and select **New Knowledge Article**.
2. Give it a **Title**.
3. In **Which roles can see this content**, pick who may open it. It reads **Any role** until you pick one, and then everyone can read it. Pick **Authenticated** for anyone who is signed in, or your staff roles for an article only your team should read.
4. Write a short **Summary**: one or two sentences shown in lists, searches and suggestions. Search looks at the title and the summary.
5. Choose its **Category** to list it with similar articles.
6. Write the **Body** in the editor. Its toolbar formats text, adds links, lists, tables and pictures, and shows a preview.
7. The **Permalink** is filled in from the title, under `help/`, such as `help/reset-your-password`. Change it if you want another address.
8. Publish it, or save a draft to finish later.

## Who can read an article

| Roles picked | Who sees the article |
| --- | --- |
| None (**Any role**) | Anyone, signed in or not |
| **Authenticated** | Anyone with an account who is signed in |
| Staff roles, such as **Editor** | People in one of those roles |

Administrators read every article. **Which roles can see this content** works as [Restrict Content by Role](../administration/content-access-control.md) does for any content, and holds everywhere the article shows: the help center, its search, related articles, suggestions and the article's own page. People outside the picked roles don't find the article, and get *You do not have access to this resource.* if they open its link.

:::note[Articles written before]
Articles written when the editor offered *Everyone*, *People who are signed in* and *Staff only* keep who could read them: *Everyone* picks no role, *People who are signed in* picks **Authenticated**, and *Staff only* picks the roles that had the old **View staff-only knowledge articles** permission, and **Administrator**.
:::

## Suggestions in tickets

With the [Service Desk](../service-desk/index.md) on too, matching articles show as suggestions while people write:

- requesters see them on the **New request** form in their account, so they may find the answer before they send;
- agents see them under the reply box, and select one to add its link to their reply. A public reply suggests only articles the requester can open; an internal note suggests every article the agent may read.

See [Answer a ticket](../service-desk/answer-a-ticket.md) and [Tickets Portal](../service-desk/portal.md).
