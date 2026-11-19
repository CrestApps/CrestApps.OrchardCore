---
sidebar_label: Notification Templates
title: Notification Templates
description: "Write the messages requesters get when their ticket is received, answered and solved, and when they're asked to rate it, under Settings > Service Desk."
technical_manual:
  - modules/tickets/emails-and-notifications
  - modules/templated-emails
---

Requesters hear from your service desk when their ticket is received, when an agent replies, when it's solved, and when they're asked to rate it. You write those messages once, under **Settings > Service Desk**.

| | |
| --- | --- |
| **Menu** | Settings > Service Desk |
| **Permission** | Manage ticket settings |
| **Feature** | Tickets |

<AskYourAdmin />

## Edit the messages

1. Go to **Settings > Service Desk** and open **Notification templates**.
2. Edit the subject and body of each message: **Ticket received**, **Reply from an agent**, **Ticket solved** and **Rate a solved ticket**. A message can use the ticket's number, subject, status, requester's name and category, and a reply's message and author.
3. Select **Save**.

## How requesters get them

- A requester who signs in to the site gets them as notifications: in their notification center, and by e-mail or text message as they choose.
- Anyone else (a contact or a guest) gets them by e-mail.
- Each message ends with **View your request**, which opens the request in their account, or a signed link for someone without a login.
- A desk sends them only when **Tell the requester** is on. See [Desks](./desks.md).

When a reply's e-mail can't be sent, the site tries again after 5 minutes, 15 minutes, an hour, 4 hours and 12 hours, then gives up. Until it's sent, the reply on the ticket shows a "not delivered" notice with the reason.

A guest who used the request form on a page gets messages with only the request's number, never what they wrote, since anyone can type someone else's e-mail. See [Request form on a page](./web-form.md).

## The AI profile for replies

With AI on, the same page has the **AI profile for replies**. See [AI assistant](./ai-assistant.md).
