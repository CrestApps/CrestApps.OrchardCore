---
sidebar_label: Members
title: Mailing List Members
description: "See who is on a mailing list, find a person, read the proof of their consent, add or import people yourself, unsubscribe people, send changes to the outside service again and erase an address for a privacy request."
technical_manual:
  - modules/mailing-lists
---

Each mailing list has a list of its members: the people who signed up, with their status and the proof of what they agreed to.

| | |
| --- | --- |
| **Menu** | Mailing lists > Lists, then **Members** on a list |
| **Permission** | View the members of mailing lists (to see members), Manage the members of mailing lists (to add, unsubscribe and erase) |
| **Feature** | Mailing Lists |

<AskYourAdmin />

## See the members of a list

1. On **Mailing lists > Lists**, click the list's name, or **Members**.
2. To find a person, type their address in **Search by e-mail address** and press Enter.
3. To see only some members, choose in **Show**: **All members**, **Subscribed**, **Pending**, **Unsubscribed**, **Cleaned** or **Not synced**.
4. To see only the people who joined one way, choose in **Joined through**: **All sources**, **Sign-up widget**, **Workflow**, **Added by an admin**, **Import** or **The provider**.

Each row shows the address and name, the status, when the status last changed, and how the person joined (such as *Widget*, *Admin*, *Workflow* or *Provider*). Click **View** to open the person's page.

| Status | What it means |
| --- | --- |
| **Pending** | Signed up, and waits to confirm the address. |
| **Subscribed** | Gets the list's e-mails. |
| **Unsubscribed** | Left the list, or was removed from it. |
| **Cleaned** | Removed because mail to the address bounces. |

| Badge | What it means |
| --- | --- |
| **Verified** | The person proved they own the address. |
| **Syncing** | The last change waits to reach the list's outside service. |
| **Waiting for the provider** | The outside service's feature is off, or the service is not set up. The change waits on the site and goes to the service once it is back. Point at the badge to see why. |
| **Not synced** | The last change didn't reach the outside service. Point at the badge to see why. |

The **Mailing lists > Lists** page shows, for each list, how many people are subscribed, how many are pending, and how many are not synced.

## A person's page

The person's page has these cards:

| Card | What it shows |
| --- | --- |
| **Membership** | **E-mail**, **Name**, **Status**, **Status since**, **Address verified**, **Joined through**, **Last subscribed**, **Last unsubscribed** (with the reason, when there is one), **Welcome coupon** (when a code was given) and **Provider** (where the list lives, whether the last change reached it, and why it waits or failed). |
| **Evidence of consent** | **Agreed** (when), **Where** (the page or form), **From address** (the internet address the sign-up came from) and **Text agreed to**. A person the outside service told the site about may have no evidence. |
| **History** | Every change of the person on this list, when the Timeline feature is on. |

At the bottom of the page:

| Button | What it does |
| --- | --- |
| **Send a new link** | Sends a new confirmation link to a pending person, or a new claim link to a person who still has to confirm their address. Older links stop working. |
| **Unsubscribe** | Removes the person from this list. |
| **Erase** | Erases the address from every list. See [Erase an address](#erase-an-address). |
| **Back** | Goes back to the members. |

## Add a person yourself

Add a person only when you hold their consent, for example a sign-up on paper at a trade fair.

1. On the list's members, click **Add member**.
2. Enter the **E-mail** and, if you like, the **Name**.
3. In **Where the person agreed**, write where they gave their consent, such as *Signed up on paper at the trade fair*. It is kept as the proof of consent.
4. Tick **I hold this person's consent to receive** the list.
5. Click **Add**. The message *The member has been added.* confirms it.

The person is subscribed at once. They get no e-mail and no welcome coupon. If the address is already subscribed, the site tells you and changes nothing. An address that asked never to be e-mailed can't be added.

## Change many members at once

1. Tick the members, or tick the box that selects them all.
2. Choose an action:

| Action | What it does |
| --- | --- |
| **Unsubscribe** | Removes the ticked people from this list. |
| **Send a new confirmation link** | Sends a new link to the ticked people who still have to confirm. |
| **Retry sync** | Sends the ticked **Not synced** people to the outside service again. Only on a list kept at an outside service. |
| **Erase (privacy request)** | Erases each ticked address from every list. |

The site tells you how many memberships changed.

## Import people from a file

With bulk import on, you can add many people who gave their consent elsewhere, such as the sign-ups of another system.

1. On **Content > Import**, choose **Mailing list members**. You need **Manage the members of mailing lists**.
2. Upload a spreadsheet with the columns **List** (the list's technical name or ID), **Email**, **Name** (optional) and **Status** (**Subscribed**, the default, or **Pending**).
3. Start the import. See [Bulk Import and Export](../administration/import-and-export.md).

The import sends no e-mail and gives no welcome coupon. A person already on the list stays as they are. A person who unsubscribed from the list, whose mail bounced, or who asked to stop all marketing e-mail is never subscribed by an import: their row fails with the reason. A pending person gets a confirmation e-mail only when you send one with **Send a new confirmation link**.

To download the members to a spreadsheet, build a report on the **Mailing lists** data source in the [Report Builder](../report-builder/index.md). It needs **View the members of mailing lists**.

## Erase an address

Use **Erase** when a person asks you to delete their data. It:

- removes the address from every list, not only this one;
- deletes the e-mails the site stored for it;
- asks each list's outside service to erase it too.

:::warning
Erasing can't be undone. At Mailchimp, an erased address is deleted permanently, and Mailchimp says it can't be imported into the audience again. When one of the lists is kept at an outside service, the **Erase** confirmation reminds you of this.
:::

While the outside service's feature is off, the erasure waits on the site and reaches the service once it is back.

If the person also clicked **Stop all marketing e-mail**, the site keeps that request after the erasure, so they are never sent marketing again. See [E-mail Preferences Page](email-preferences.md).

## People marked not synced

On a list kept at an outside service, each change is sent to the service at once. When the service doesn't take it, the site tries again for some hours, then marks the person **Not synced**. While the service's feature is off or it is not set up, the changes wait instead (**Waiting for the provider**) and are never marked **Not synced**; they go out once the service is back.

1. On the list's members, choose **Not synced** in **Show**.
2. Point at a **Not synced** badge, or open the person, to read the service's error.
3. Fix the cause, such as the connection settings on **Settings > Mailing Lists**, or the list's settings at the service.
4. Tick the people, choose **Retry sync**, and confirm. Each person's current state is sent to the service again.

The next change of a person is also sent again.
