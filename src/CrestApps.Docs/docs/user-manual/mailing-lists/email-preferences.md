---
sidebar_label: E-mail Preferences Page
title: The E-mail Preferences Page
description: "What people see when they click the unsubscribe link in a mailing list or campaign e-mail: leave one list, or stop all your marketing e-mail, while the e-mails they asked for still come."
technical_manual:
  - modules/consent
---

Every mailing list e-mail and every Marketing campaign e-mail ends with an unsubscribe link. The link opens your site's e-mail preferences page, where the person decides what to stop. This page explains what they see, so you can answer their questions.

| | |
| --- | --- |
| **Page** | The unsubscribe link at the end of a mailing list or campaign e-mail |
| **Permission** | None: the link itself lets the person in, without an account |
| **Feature** | E-mail Preferences (turned on by Mailing Lists and Marketing) |

<AskYourAdmin />

## What the person sees

Opening the page changes nothing. The person must click a button, so an e-mail security scanner that opens every link unsubscribes nobody.

| The link came from | The page asks | Buttons |
| --- | --- | --- |
| A mailing list e-mail | *Unsubscribe from Weekly news?* | **Unsubscribe** leaves that list. **Stop all marketing e-mail** leaves every list and stops all your marketing. |
| A Marketing campaign e-mail | *Unsubscribe from all marketing e-mail?* | **Unsubscribe** stops all your marketing. |
| A list that was deleted since | *Unsubscribe from our marketing e-mail?* | **Stop all marketing e-mail**. |

After leaving one list, the page offers **Stop all marketing e-mail** too.

When the person stops all marketing, the page tells them that e-mails they asked for, such as order and account notices, still come.

## What "Stop all marketing e-mail" does

- The person leaves every mailing list, also lists kept at an outside service such as Mailchimp.
- No Marketing campaign goes to the address.
- If they are a store customer, their **Send me news and offers** box is unticked.
- The site keeps the request for the address, even when the person is not a customer or a contact, and even after you erase their data for a privacy request.
- Order notices, replies to their questions and other e-mails they asked for still come. A CRM contact's **Do not email** box is not ticked.

A person who stopped all marketing can still join a list again later, with the [sign-up form](signup-form.md). They confirm their address first, and confirming says yes to your marketing again.

## One-click unsubscribe in mail apps

The page also accepts the one-click unsubscribe that mail apps such as Gmail send. The e-mails your site sends itself can't ask mail apps to show their own unsubscribe button, so people use the link at the end of the e-mail. Campaigns sent by an outside service such as Mailchimp carry that service's own unsubscribe link, and the service tells your site when someone uses it.

## When the link doesn't work

A link works for about 400 days. An old or damaged link shows *This link doesn't work any more.* and asks the person to use the link in a newer e-mail.

The links need your site's base URL (**Settings** → **General**). Without it, mailing list e-mails go without the unsubscribe link, and Marketing campaigns don't go out.
