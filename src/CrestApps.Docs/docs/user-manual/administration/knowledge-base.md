---
sidebar_label: Knowledge Base
title: Knowledge Base
description: "Publish help articles at /help: who can read each one, categories, search, and readers' \"was this helpful?\" answers."
technical_manual:
  - modules/knowledge-base
---

The **Knowledge Base** is your help center: articles that answer common questions, at `/help` on your site.

## Write an article

1. Turn on **Knowledge Base** under **Configuration > Features**.
2. Go to **Content > New > Knowledge Article**.
3. Give it a title and write the body. In **Who can read it**, pick who may open it:
   - pick no role for everyone, signed in or not;
   - pick **Authenticated** for anyone who is signed in;
   - pick your staff roles (for example **Editor**) for an article only your team should read.
4. Give it a **Category** to list it with similar articles, and a short **Summary**. Search looks at the title and the summary.
5. Publish it, or save a draft to finish later.

Administrators can read every article. **Who can read it** works as [Restrict Content by Role](content-access-control.md) does for any content: people outside the picked roles don't find the article in the help center, its search or suggestions, and get *You do not have access to this resource.* if they open its link.

:::note[Articles written before]
Articles written when the editor offered *Everyone*, *People who are signed in* and *Staff only* keep who could read them: *Everyone* picks no role, *People who are signed in* picks **Authenticated**, and *Staff only* picks the roles that had the old **View staff-only knowledge articles** permission, and **Administrator**.
:::

## The help center

Visitors find the categories, the most helpful articles and a search box at `/help`. Each article asks **Was this helpful?**; the answers show how well it works.
