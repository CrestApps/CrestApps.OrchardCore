---
sidebar_label: Mailchimp
title: Keep a Mailing List in Mailchimp
description: "Connect your site to Mailchimp and keep a mailing list in a Mailchimp audience: sign-ups go to Mailchimp, Mailchimp can send the confirmation e-mail, and unsubscribes and bounces in Mailchimp come back to the site."
technical_manual:
  - modules/mailing-lists-mailchimp
---

If you send your newsletters with Mailchimp, a mailing list can live in a Mailchimp audience. People still sign up on your site, and the site keeps every person and the proof of their consent. Each change goes to Mailchimp, and what happens in Mailchimp (an unsubscribe, a bounce, a confirmation) comes back to the site.

| | |
| --- | --- |
| **Menu** | Settings > Mailing Lists (the connection), Mailing lists > Lists (a list's **Mailchimp** card) |
| **Permission** | Manage the mailing lists settings (the connection), Manage mailing lists (the lists) |
| **Feature** | Mailing Lists Mailchimp |

<AskYourAdmin />

## Connect your site to Mailchimp

1. In Mailchimp, create an API key: **Profile > Extras > API keys**. Copy it.
2. On your site, save the key as a **text secret** under **Security > Secrets**, for example named *Mailchimp.ApiKey*. A Mailchimp key never expires and opens your whole Mailchimp account, so let only the people who need it see the secret.
3. On **Settings > Mailing Lists**, find the **Mailchimp** card.
4. In **API key secret**, choose the secret.
5. Leave **Server prefix** empty, unless your key doesn't end with a dash and its data center (such as *-us21*). Then enter the data center, which is also at the start of the address of your Mailchimp pages.
6. Click **Save**.
7. Click **Test the connection**. The message *Mailchimp answered at ...: the settings work.* confirms it.

To turn Mailchimp off, empty **API key secret** and save.

## Keep a list in Mailchimp

1. Create the audience in Mailchimp first.
2. On **Mailing lists > Lists**, click **Edit** on the list.
3. In the **Provider** card, choose **Mailchimp** in **Where the list lives**, and click **Save**. The **Mailchimp** card shows.
4. Fill in the **Mailchimp** card (see below), and click **Save**.
5. Click **Register the webhook**, so changes made in Mailchimp come back at once.

| Field | What it does |
| --- | --- |
| **Audience** | The Mailchimp audience the list's people are kept in. Changing it forgets the list's webhook: register it again, and delete the old one in Mailchimp (**Audience > Settings > Webhooks**). |
| **Tags** | Tags added to each person sent to Mailchimp, separated by commas, such as *website-signup*. Mailchimp creates a tag that doesn't exist yet. |
| **Send the name in the FNAME and LNAME merge fields** | The first word of the name goes in FNAME and the rest in LNAME. Name changes made in Mailchimp come back to the site. |
| **Coupon merge tag** | Used only when the list's [welcome coupon](welcome-coupon.md) is sent by Mailchimp: each person's code is written to this merge field, such as *COUPON*. **Create the merge field in Mailchimp** adds it to the audience. |
| **Confirmation e-mail** | Says who sends the confirmation e-mail. To let Mailchimp send it, set **Confirm sign-ups** in the **Confirmation** card to **The provider sends its own confirmation e-mail**. |
| **Webhook** | Whether the webhook is registered. **Register the webhook** (or **Register the webhook again**) sets it up. Mailchimp must reach your site from the internet over HTTPS, at your site's base URL (**Settings > General**). |

## What goes to Mailchimp

| On your site | In Mailchimp |
| --- | --- |
| A person is subscribed | They are added as subscribed, with their name and the tags. |
| A person signs up on a list Mailchimp confirms | They are added as pending, and Mailchimp sends its confirmation e-mail. |
| A person signs up on a list the site confirms | Nothing yet. They are added once they confirm, so they never get two confirmation e-mails. |
| A person unsubscribes | They are marked unsubscribed. |
| You erase a person | They are deleted permanently. Mailchimp says the address can't be imported into the audience again. |

A person who left the list in Mailchimp and signs up again on your site is asked by Mailchimp to confirm before Mailchimp mails them again.

Changes go to Mailchimp at once. When Mailchimp doesn't take one, the site tries again for some hours, then marks the person **Not synced**. See [Members](members.md#people-marked-not-synced). While the Mailchimp feature is off, or the connection is not set up, the changes wait on your site instead, the person shows **Waiting for the provider**, and the changes go to Mailchimp once it is back.

## What comes back from Mailchimp

With the webhook registered, these changes made in Mailchimp come back at once:

- a person confirms Mailchimp's confirmation e-mail, signs up with a Mailchimp form, or is added by a Mailchimp admin: they are **Subscribed** and verified on your site;
- a person unsubscribes, or a Mailchimp admin deletes them: they are **Unsubscribed**;
- mail to a person bounces: they are **Cleaned**;
- a person changes their name or address.

Once a day, the site also reads the changes made in Mailchimp since the last time, in case a webhook was missed. Without the webhook, changes come back only this way.

## Things to know

- **People already on the list aren't copied.** When you move an existing list to Mailchimp, only the changes made after the move go to Mailchimp. People Mailchimp already has come to your site only when they change in Mailchimp.
- **People added in Mailchimp get no welcome coupon from your site.** A person who joins with a Mailchimp form, or whom a Mailchimp admin adds, is added to your list, but your site sends them no welcome e-mail and no welcome code, as for an import. A person who signs up on your site and confirms in Mailchimp gets the code.
- **A code sent by Mailchimp on a list Mailchimp confirms** is written to the merge field as soon as the person confirms.
- **While the Mailchimp feature is off,** sign-ups still work, and every change waits on your site until the feature is on again, then goes to Mailchimp. On a list Mailchimp confirms, the site sends its own confirmation e-mail meanwhile, so a person who has not confirmed yet may also get Mailchimp's once it is back.
- **Erasing is permanent.** The **Erase** confirmation reminds you that Mailchimp deletes the address for good and that it can never be imported into the audience again.
- **A list copied to another site** keeps its Mailchimp settings, but its webhook must be registered again there.

## Troubleshooting

| Message | What to do |
| --- | --- |
| *Mailchimp is not set up...* | Choose the secret that holds the API key on **Settings > Mailing Lists**, and save. |
| *Mailchimp refused the API key...* | Check the secret and the **Server prefix**. |
| *Mailchimp could not be reached from this server...* | Your server can't reach Mailchimp. Ask your administrator. |
| *The webhook address could not be built...* | Set your site's base URL under **Settings > General**. |
| *Mailchimp refused the webhook...* | Mailchimp must reach your site from the internet over HTTPS. |
| *Save the list with Mailchimp as its provider first.* | Choose **Mailchimp** in the **Provider** card and save before you use the card's buttons. |
