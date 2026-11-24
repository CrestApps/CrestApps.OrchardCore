---
sidebar_label: Mailing Lists
title: Mailing Lists
description: "Collect newsletter subscribers on your site: create a mailing list, choose how a sign-up is confirmed, write the e-mails people get, and see who unsubscribed."
technical_manual:
  - modules/mailing-lists
---

A mailing list collects the e-mail addresses of people who want your news, such as a weekly newsletter. People join from a sign-up form on your site. You decide, for each list, whether people must confirm their address first, and you can give each new address a welcome coupon.

| | |
| --- | --- |
| **Menu** | **Mailing Lists** → **Lists** |
| **Permission** | View the members of mailing lists (to see the lists), Manage mailing lists (to add and change lists) |
| **Feature** | Mailing Lists |

<AskYourAdmin />

The site keeps every list and every person on it, with the proof of what they agreed to. A list can live on your site or at an outside e-mail marketing service, such as [Mailchimp](mailchimp.md). Mailing lists don't write or send newsletters themselves: you send them from your e-mail marketing service.

## Pages about mailing lists

| Page | What it covers |
| --- | --- |
| [Members](members.md) | Who is on a list, a person's page, adding a person, unsubscribing and erasing |
| [Sign-up Form](signup-form.md) | The form visitors use to join a list |
| [Welcome Coupon](welcome-coupon.md) | A one-time discount code for each new address |
| [Mailchimp](mailchimp.md) | Keeping a list in a Mailchimp audience |
| [E-mail Preferences Page](email-preferences.md) | The page the unsubscribe link in every e-mail opens |

## Create a list

1. On **Mailing Lists** → **Lists**, click **Add mailing list**.
2. Fill in the **General** card. Give the list a **Name** people see, such as *Weekly news*, and a **Technical name**, such as *weekly-news*.
3. In the **Confirmation** card, choose how a sign-up is confirmed (see [How a sign-up is confirmed](#how-a-sign-up-is-confirmed)).
4. In the **Provider** card, choose **Where the list lives**. Keep **This site** unless you use an outside service.
5. Fill in the **Welcome coupon** card if you want to give new people a discount code. The card shows only when the welcome coupon feature is on. See [Welcome Coupon](welcome-coupon.md).
6. Click **Save**. The message *The mailing list has been created.* confirms it.

To put the list on a page, add a [sign-up form](signup-form.md).

### General

| Field | What it does |
| --- | --- |
| **Name** | The name people see on the sign-up form, in the e-mails and on the e-mail preferences page, such as *Weekly news*. |
| **Technical name** | A unique name for recipes and workflows: letters, digits, dashes and underscores, starting with a letter. |
| **Description** | What the list sends and how often, such as *Our news and offers, every Friday*. It shows under the name. |
| **Consent text** | What people agree to when they sign up, shown on the sign-up form. Each person keeps the text they agreed to as proof of consent. |
| **Offer the list to people** | When this is off, the sign-up form doesn't offer the list, the sign-up form and workflows can't add people to it, and it can't be chosen for a Marketing campaign. You can still add and import people yourself, and people can still unsubscribe. |
| **Send a welcome e-mail** | Sends a welcome e-mail when a person is subscribed. A welcome coupon is always e-mailed, even when this is off. |

### Confirmation

| Field | What it does |
| --- | --- |
| **Confirm sign-ups** | **Double opt-in: the site e-mails a confirmation link**, **Single opt-in: people are subscribed at once**, or **The provider sends its own confirmation e-mail**. |
| **Confirmation link works for (days)** | Between 1 and 30 days (7 when you create a list). After that, the link's page offers to send a new link. |
| **Skip the confirmation for signed-in users with a confirmed e-mail** | A signed-in user who signs up with the confirmed e-mail of their account is subscribed and verified at once, without a confirmation e-mail. |

### Provider

| Field | What it does |
| --- | --- |
| **Where the list lives** | **This site**, or an outside service such as **Mailchimp** when its feature is on. The site always keeps every person and the proof of their consent. An outside service gets each change and sends its own changes back. |

When you choose **Mailchimp** and save, a **Mailchimp** card shows. See [Mailchimp](mailchimp.md).

## How a sign-up is confirmed

| Confirm sign-ups | What happens when a person signs up |
| --- | --- |
| **Double opt-in: the site e-mails a confirmation link** | The person is **Pending** and gets an e-mail with a link. The link opens a page with a **Confirm** button. Once they click it, they are **Subscribed**. This proves the person owns the address, and it is expected in many countries. |
| **Single opt-in: people are subscribed at once** | The person is **Subscribed** at once. But if the address unsubscribed from all marketing before, it gets a confirmation e-mail first, so nobody can sign up another person who said stop. |
| **The provider sends its own confirmation e-mail** | The person is **Pending**, and the outside service (such as Mailchimp) sends its own confirmation e-mail. When the person confirms there, the service tells the site, and they are **Subscribed**. A service that can't confirm addresses falls back to the site's confirmation e-mail. |

More things to know:

- The page the confirmation link opens changes nothing until the person clicks **Confirm**. E-mail security scanners that open every link don't subscribe anyone.
- If the link expired, or a newer link was sent, the page says so and offers **Send me a new link**.
- One address gets at most 3 confirmation e-mails per list in 15 minutes.
- The sign-up form shows the same message whether or not the address was already on the list, so nobody can find out who is subscribed.
- An address that bounced (**Cleaned**) must confirm again, whatever the list says.
- Sign-ups never confirmed are deleted after 30 days.
- When a person confirms, they also say yes to your marketing again. If they are a store customer, their **Send me news and offers** box is ticked.

## The e-mails people get

| E-mail | When |
| --- | --- |
| **Confirm a sign-up** | A sign-up must be confirmed. It has a **Confirm my subscription** button. If the welcome coupon is given at sign-up, the code is in it. |
| **Welcome** | A person is subscribed and the list sends a welcome e-mail, or a welcome coupon was given. |
| **Claim the welcome coupon** | A person is subscribed on a list without confirmation, and the welcome coupon waits until they confirm their address. It has a **Confirm and get my gift** button. |

Every e-mail ends with a link to unsubscribe or change their e-mail preferences.

### Write your own e-mails

You can replace the e-mails the site writes with your own.

| | |
| --- | --- |
| **Menu** | **Settings** → **Mailing Lists** |
| **Permission** | Manage the mailing lists settings |
| **Feature** | Mailing Lists |

<AskYourAdmin />

1. On **Settings** → **Mailing Lists**, find the **E-mail templates** card.
2. Optionally, fill in a **Layout**: HTML around every mailing list e-mail, such as your logo and address, with `{{ Content }}` where the e-mail goes.
3. For each e-mail you want to change, tick **Use my template**, then write the **Subject** and the **Body**.
4. Click **Save**.

In a template you can write these values:

| Write | To show |
| --- | --- |
| `{{ Content }}` and `{{ Subject }}` | The e-mail as the site would send it, and its subject. |
| `{{ List.Name }}` and `{{ List.Description }}` | The list's name and description. |
| `{{ Member.Name }}` and `{{ Member.Email }}` | The person's name, when they gave it, and their address. |
| `{{ Links.Verify }}` | The confirmation or claim link. |
| `{{ Links.Unsubscribe }}` | The unsubscribe link. Keep it in every template. |
| `{{ Coupon.Code }}`, `{{ Coupon.Url }}`, `{{ Coupon.Description }}` and `{{ Coupon.Expires }}` | The welcome code, the link that applies it at checkout, the discount in words (such as *15% off*), and when it ends, in your site's time zone. Only when the e-mail carries a code. |

If a template can't be used, the site says why when you save. An e-mail whose template fails is sent as the site built it.

## When people unsubscribe

Every mailing list e-mail has an unsubscribe link. It opens the [e-mail preferences page](email-preferences.md), where the person can leave the list, or stop all your marketing e-mail.

A person is also unsubscribed from every list when they:

- click **Stop all marketing e-mail** on the e-mail preferences page;
- unsubscribe through the link in an older Marketing campaign e-mail;
- untick **Send me news and offers** in their store account, or you untick **Accepts marketing** on their customer record.

A person who stopped all marketing can still join a list again later. They then confirm their address first.

On a list kept at an outside service, people can also unsubscribe there. The service tells the site.

## Delete a list

On **Mailing Lists** → **Lists**, click **Delete** in the list's actions, then confirm. The people on it are kept until you erase them, so a list deleted by mistake can be restored from a recipe without losing the proof of consent.

## Troubleshooting

| Problem | What to do |
| --- | --- |
| Nobody gets the e-mails | Your site's e-mail must be set up. Ask your administrator. |
| The e-mails have no unsubscribe link | Your site needs its base URL (**Settings** → **General**). |
| The list shows **Provider not available** | The outside service's feature is off. Sign-ups still work, and the changes wait on your site until the feature is on again; then they go to the service. Ask your administrator to turn the feature on under **Tools** → **Features**. |
| The list shows **not synced** | Some changes didn't reach the outside service. See [Members](members.md#people-marked-not-synced). |
| The list isn't on the sign-up form | Check that **Offer the list to people** is on, and that the form offers the list. |
