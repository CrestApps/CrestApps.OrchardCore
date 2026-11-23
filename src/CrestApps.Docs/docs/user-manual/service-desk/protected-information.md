---
sidebar_label: Protected Information
title: Protected Information
description: "Keep a login or another secret on a ticket, encrypted and apart from its messages, shown only to the people allowed to see it after they type their password."
technical_manual:
  - modules/tickets/protected-information
---

Requesters sometimes need to give a login or another secret. Instead of writing it in a message, they (or you) add it as **protected information**: a label, such as "VPN login", and the secret. It's kept encrypted, apart from the ticket and its messages, and it never shows in the ticket list, searches, reports, exports, e-mails or notifications, or to the AI assistant.

| | |
| --- | --- |
| **Menu** | **Service Desk** → **Tickets**, then a ticket |
| **Permission** | **View protected information** on the ticket's desk to show and remove it; **Add internal notes to tickets** to add it |
| **Feature** | Tickets |

<AskYourAdmin />

## See a ticket's protected information

- A ticket that holds some shows **Contains protected information** at the top, and a **Protected** badge in the ticket list.
- The **Protected information** card on the ticket lists the labels; the secrets stay hidden. To see them, type your own password and select **Show**. They show for 5 minutes, or until you select **Hide**, and then they're hidden again.
- Only people with **View protected information** on the ticket's desk get **Show**, and the remove buttons. Administrators have it on a new site; on a site that existed before, give it to the roles that need it under **Users** → **Roles**.
- If you sign in to the site without a password of your own (only through an outside sign-in), you can't show protected information: ask an administrator to set a password on your account.
- Each time someone shows the secrets, gives a wrong password, adds or removes one, it's noted in the ticket's **History**, with who and when but never the secret. Too many wrong passwords lock the account for a while, as at sign-in.

## Add or remove it

- Add protected information yourself with **Add protected information** on the ticket, or in **Protected information** on the new ticket form.
- Requesters can add it when they send a request or a reply from the site (**Share a login or other secret**), or with the request form on a page. On their request's page they see **What you shared under protection** with the labels; if they're signed in, they can show the secrets with their own password, and they can remove them. Requesters only ever see what they shared themselves, never what you add.
- Select the bin next to an item to remove it. It can't be brought back.

## What happens to it later

- When a ticket is merged, its protected information moves to the ticket it's merged into.
- Deleting a ticket, or erasing its requester's personal data, deletes it.
- A desk can delete it a number of days after a ticket closes: set **Delete protected information after (days)** on the desk (0 keeps it). See [Desks](./desks.md).
