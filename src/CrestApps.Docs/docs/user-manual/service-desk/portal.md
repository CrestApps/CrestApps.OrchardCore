---
sidebar_label: Tickets Portal
title: Let Customers Open Requests on the Site
description: "Let customers and employees send and follow requests from their account, guests answer through a link, account administrators see their account's requests, and requesters rate solved requests."
technical_manual:
  - modules/tickets/portal-and-web-form
---

With the **Tickets Portal**, the people you serve send requests from their account on your site, follow them and reply, without e-mail back and forth.

| | |
| --- | --- |
| **Menu** | For requesters: **My account** → **Requests**; to set up: **Design** → **Content Definition** → **Content Types and Service Desk** → **Desks** |
| **Permission** | Edit content types and Manage ticket settings to set it up; requesters need nothing but an account |
| **Feature** | Tickets Portal |

<AskYourAdmin />

## Set it up

1. Turn on **Tickets Portal** under **Tools** → **Features**.
2. Under **Content Definition**, edit the ticket type's **Ticket** part and tick **Customers can submit it**. Add a **Description for customers** so they can tell types apart.
3. In each field's settings, tick **Customers fill this in** for the fields customers should answer. Customers see the subject and only those fields; every other field stays your agents' own. See [Ticket types, fields and categories](./ticket-types-and-categories.md).
4. Make sure a desk **Serves** customers (**Service Desk** → **Desks**). Customers can only open requests on those desks. With Staff on, members of staff also see the desks that serve people inside the organization, so employees can ask IT or HR from the same page.

## What requesters do

Signed-in people find **Requests** under **My account**:

- their requests, newest first, with the status as customers see it;
- **New request**: they choose the kind of request, then answer its form (the subject, a category when the type has them, the questions that apply, their message and files);
- each request's conversation with a reply box. They see your public replies and their files, never your internal notes or the history of changes. A reply on a solved request reopens it, as on your side.

While they write, matching [Knowledge Base](../knowledge-base/index.md) articles show as suggestions, so they may find the answer before they send.

## Guests and links

Someone without a login follows **View your request** in their e-mails to read and answer their request. The link works for 90 days, and only for that request: a new requester, a merge or an erasure makes earlier links stop working.

## Account administrators

An account's administrators also see the account's requests, on a tab of their **Requests** page. Any member of an account sends new requests under the account.

## Ratings

To learn how you did, set **Ask requesters to rate a solved ticket after (hours)** on a desk. The requester of a solved ticket then gets one message with a link to rate it from 1 to 5, with a comment if they like, and can also rate it on the request's page. Ratings show on the ticket's history and in the **Tickets** data set for reports.

## Sharing a login or other secret

When customers send a request or a reply, they can open **Share a login or other secret** and give it there rather than in their message. On their request's page they see **What you shared under protection** with the labels; if they're signed in, they can show the secrets with their own password, and they can remove them. See [Protected information](./protected-information.md).

## Limits

To protect the site, a requester can send a limited number of requests and replies in a short time. If they reach it, the page tells them to try again later.
