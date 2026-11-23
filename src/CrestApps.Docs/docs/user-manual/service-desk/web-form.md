---
sidebar_label: Request Form on a Page
title: Put a Request Form on a Page
description: "Add a Ticket Form widget to any page so visitors, signed in or not, can send a request, with a challenge and limits to keep robots out."
technical_manual:
  - modules/tickets/portal-and-web-form
---

A **Ticket Form** widget lets anyone send a request from a page of your site, such as a contact page, even without an account.

| | |
| --- | --- |
| **Menu** | **Design** → **Widgets (layers)**, or a page's flow |
| **Permission** | Manage widgets, or edit the page |
| **Feature** | Ticket Web Form |

<AskYourAdmin />

## Add the form

1. Turn on **Ticket Web Form** under **Tools** → **Features**.
2. Add a **Ticket Form** widget where visitors should find it (a layer, or a page's flow).
3. Choose the ticket type and the desk its tickets go to, and, if you like, what visitors read after sending.
4. Publish.

## What visitors do

Visitors give their request, choose a category when the ticket type has them, answer the questions customers fill (the same ones as in the portal, appearing as they answer), give a subject unless the type has its own title, and give their name and e-mail when they aren't signed in. A signed-in visitor is the requester. A category's desk can take over from the widget's desk.

After sending, the page says the request was received, with its number, or what went wrong.

## Keep robots out

- Turn on **ReCaptcha** and set its keys under **Settings** → **Security** so guests have to pass a challenge. The widget warns you until you do.
- Turn on **Rate Limits** too, to cap how many requests one visitor can send.

A guest's e-mail address isn't confirmed, so the e-mails they get only give the request's number, never what they wrote, and their request is never filed under an account by their e-mail's domain. Your agents' replies still reach them.
