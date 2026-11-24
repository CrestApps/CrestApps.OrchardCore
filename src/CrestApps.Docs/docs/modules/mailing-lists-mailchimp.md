---
sidebar_label: Mailchimp
title: Mailing Lists Mailchimp
description: "The Mailchimp provider of mailing lists: members pushed to a Mailchimp audience through the provider outbox, Mailchimp's own double opt-in, webhooks through the provider webhook inbox, a daily reconciliation, and the API key kept in Orchard Core Secrets."
user_manual:
  - user-manual/mailing-lists/mailchimp
---

| | |
| --- | --- |
| **Module** | `CrestApps.OrchardCore.MailingLists.Mailchimp` (Mailing Lists Mailchimp) |
| **Core** | `CrestApps.OrchardCore.MailingLists.Mailchimp.Core` (the API client, the provider, the webhook parser and handler) |
| **Category** | Communication |
| **Provider name** | `Mailchimp` (a list's `ProviderName`) |

The Mailchimp module keeps a [mailing list](mailing-lists.md) in a Mailchimp audience. The site keeps every member and the evidence of consent; each member change is pushed to Mailchimp through the provider outbox, and Mailchimp's webhooks and a daily reconciliation bring its changes back. Mailchimp's own double opt-in can confirm sign-ups, and the welcome coupon code can be written to a merge field for a Mailchimp welcome automation.

The screens are described in the User Manual: [Mailchimp](../user-manual/mailing-lists/mailchimp.md).

## Feature

| Feature | ID | Depends on |
| --- | --- | --- |
| Mailing Lists Mailchimp | `CrestApps.OrchardCore.MailingLists.Mailchimp` | Mailing Lists (`CrestApps.OrchardCore.MailingLists`), Provider Webhook Inbox (`CrestApps.OrchardCore.ContactCenter.ProviderInbox`, see [Webhooks](webhooks.md#provider-webhook-inbox)), Orchard Core Secrets (`OrchardCore.Secrets`) |

The provider has the capabilities `ConfirmsAddresses`, `DeliversWelcomeOffer`, `ReportsChanges` and `SendsCampaigns`.

## Set it up

1. In Mailchimp, create an API key (**Profile** → **Extras** → **API keys**). A Mailchimp API key gives full access to the account and never expires.
2. Save it as an Orchard Core **text secret**, for example named `Mailchimp.ApiKey` (**Security** → **Secrets**, or the `Secrets` recipe step).
3. On **Settings** → **Mailing Lists**, in the **Mailchimp** card, choose the secret in **API key secret**, and fill in **Server prefix** when the key doesn't end with its data center. Save, then click **Test the connection**.
4. On a list's editor, set **Where the list lives** to **Mailchimp**, save, and fill in the **Mailchimp** card: the audience, the tags, and **Register the webhook**.

The connection is `MailchimpSettings` in the site settings, edited on the `mailing-lists` settings group (permission `ManageMailingListSettings`):

| Setting | What it is |
| --- | --- |
| `SecretName` | The name of the Orchard Core text secret that holds the API key. Empty turns Mailchimp off. The key itself is never in the settings. |
| `ServerPrefix` | The Mailchimp data center, such as `us21` (letters, then digits). Empty takes it from the key's suffix (`...-us21`); without either, the API root is `https://api.mailchimp.com/3.0/`. |

```json
{
  "steps": [
    {
      "name": "Settings",
      "MailchimpSettings": {
        "SecretName": "Mailchimp.ApiKey",
        "ServerPrefix": "us21"
      }
    }
  ]
}
```

`IMailchimpCredentialsProvider` reads the secret on each request, so a rotated key is used at once. A secret that is missing, empty or not a text secret turns Mailchimp off and logs a warning.

## A list's Mailchimp settings

The **Mailchimp** card of the list editor (`MailchimpMailingListDisplayDriver`) shows when the list's provider is Mailchimp. Its settings are `MailchimpListSettings` in the list's properties:

| Property | Default | What it is |
| --- | --- | --- |
| `AudienceId` | | The Mailchimp audience (list) id. The card lists the account's audiences (`GET /lists`), or takes the id typed in when Mailchimp can't be reached. Changing it forgets the list's webhook. |
| `Tags` | | Tags added to every member pushed, such as `website-signup`. |
| `SendNameMergeFields` | `true` | Sends the member's name in `FNAME` (the first word) and `LNAME` (the rest), and reads name changes back. |
| `CouponMergeTag` | `COUPON` | The text merge field the welcome code is written to, when the list's welcome coupon is delivered by the provider. Up to 10 capital letters, digits and underscores. **Create the merge field in Mailchimp** adds it (`POST /lists/{id}/merge-fields`, named *Welcome coupon*). |
| `WebhookId`, `WebhookUrlSecret`, `WebhookSigningSecret`, `WebhookRegisteredUtc` | | The registered webhook. The secrets are protected with the tenant's Data Protection keys (purpose `CrestApps.OrchardCore.MailingLists.Mailchimp.Webhook`). |

Whether Mailchimp sends the confirmation e-mail is the list's own **Confirm sign-ups** setting (`MailingListConfirmation.Provider`), not a Mailchimp setting.

A list imported from another site keeps its webhook settings, but the secrets can't be read with the new site's keys: register the webhook again there. Leave the webhook properties out of a recipe you write by hand:

```json
{
  "name": "MailingLists",
  "MailingLists": [
    {
      "ItemId": "4ml00000000000000000000002",
      "Name": "store-news",
      "DisplayText": "Store news",
      "ProviderName": "Mailchimp",
      "Confirmation": "Provider",
      "Properties": {
        "MailchimpListSettings": {
          "AudienceId": "a1b2c3d4e5",
          "Tags": [ "website-signup" ],
          "SendNameMergeFields": true,
          "CouponMergeTag": "COUPON"
        }
      }
    }
  ]
}
```

| Admin action | Route | Permission |
| --- | --- | --- |
| **Test the connection** | `POST Admin/mailing-lists/mailchimp/test` | Manage the mailing lists settings |
| **Create the merge field in Mailchimp** | `POST Admin/mailing-lists/mailchimp/{listId}/merge-field` | Manage mailing lists |
| **Register the webhook** | `POST Admin/mailing-lists/mailchimp/{listId}/webhook` | Manage mailing lists |

## The API client

`MailchimpClient` calls the Marketing API v3 with the named `HttpClient` `Mailchimp`. The API key is sent as a bearer token in the `Authorization` header only, never in a URL, a body, an exception or a log.

- The standard resilience handler gives each attempt up to 60 seconds and a request up to 180, and honors `Retry-After` on a 429. A request is retried only when it is safe to send again: reads, member upserts, status changes, tags and deletes are; creating a webhook or a merge field is not.
- A tenant makes at most 3 Mailchimp calls at the same time (`MailchimpConcurrencyGate`), because Mailchimp allows 10 simultaneous connections per Mailchimp user across every client of the account.

## Pushing changes

`MailchimpMailingListProvider.PushAsync` is called by the provider outbox (see [Mailing Lists: providers](mailing-lists.md#providers-and-the-provider-outbox)) and is safe to call twice. The member's id at Mailchimp is its subscriber hash (the MD5 of the lower-case address).

| Member change | Call |
| --- | --- |
| Pending, on a list whose confirmation is the provider's | `PUT /lists/{audience}/members/{hash}` with `status_if_new = pending`: Mailchimp sends its own confirmation e-mail. |
| Pending, on a list the site confirms | Nothing. The member reaches Mailchimp once subscribed, so the person never gets two confirmation e-mails. |
| Subscribed | `PUT .../members/{hash}` with `status_if_new = subscribed`, the merge fields, then `POST .../members/{hash}/tags`. |
| Unsubscribed, or an `Unsubscribe` push | `PATCH .../members/{hash}` with `status = unsubscribed`, on an existing member only. |
| Cleaned | Nothing. |
| `Erase` | `POST .../members/{hash}/actions/delete-permanent`. |

An upsert sets the status only for a new address, so a retried push changes nothing and never makes Mailchimp send a second confirmation e-mail. When Mailchimp answers that it holds the address as `unsubscribed`, `cleaned` or `archived`, a second call sets it to `pending`, so Mailchimp asks the person to confirm before it mails them again; a `transactional` address gets the status a new one would get. A subscribed or pending address at Mailchimp is left as it is.

A refusal (a 4xx other than 408 and 429) is not tried again; the member is marked not synced with Mailchimp's error. A time-out, a 408, a 429, a 5xx or a broken circuit is tried again by the outbox.

## Webhooks

**Register the webhook** creates one webhook on the audience (`POST /lists/{id}/webhooks`) for the `subscribe`, `unsubscribe`, `profile`, `cleaned` and `upemail` events, from the `user` and `admin` sources and not from `api`, so the site's own pushes don't come back. Its URL is the site's base URL followed by `mailing-lists/mailchimp/webhooks/{listId}/{secret}`, with a new random secret; registering again deletes the old webhook first. Mailchimp must reach the site from the internet over HTTPS.

The endpoint (`MailchimpWebhookEndpoint`, anonymous):

1. Refuses a body over 64 KB (413), and holds back when the provider inbox's ingress limiter is full (429 with `Retry-After`).
2. Answers 404 when the list is gone, is not a Mailchimp list, or the URL secret doesn't match (compared in constant time).
3. When Mailchimp returned a signing secret, checks the `X-Mailchimp-Signature` header (`t={unix seconds},v1={hex}`, the HMAC-SHA256 of `{t}.{raw body}`), and refuses a signature more than 5 minutes from now (401). Without one, the URL secret is the check.
4. Answers 400 for a body without a `type`, or for another audience; answers 200 and ignores the events it doesn't handle.
5. Stores the delivery in the [provider webhook inbox](webhooks.md#provider-webhook-inbox) with the delivery id SHA-256 of the list id and the body (Mailchimp sends no event id), answers 200 for an accepted or duplicate delivery and 503 when the inbox is busy or failed (so Mailchimp sends it again), and applies it after the answer.

`MailchimpWebhookInboxHandler` (naturally idempotent) applies each event through `ApplyProviderChangeAsync`, which ignores a change older than the member's last status change:

| Event | Change |
| --- | --- |
| `subscribe` | Subscribed and verified: Mailchimp's double opt-in, a Mailchimp form, or a member added by a Mailchimp admin. A new address is added to the site's list. |
| `unsubscribe` | Unsubscribed, with Mailchimp's reason, or `deleted` when an admin deleted the member. |
| `cleaned` | Cleaned, with Mailchimp's reason. No consent opt-out is recorded, so the address's other lists and CRM records are not changed. |
| `profile` | The name, when the list sends name merge fields. |
| `upemail` | The new address, when the list has no member with it. |

## Reconciliation

Mailchimp drops or turns off webhooks that fail for long. The daily reconciliation of Mailing Lists calls `GetChangesAsync`, which reads `GET /lists/{id}/members?since_last_changed=...` 500 members a page, and maps each member's status (`archived` reads as unsubscribed) and name. A subscribed member counts as verified only on a list whose confirmation is the provider's, because the site's own pushes also make members subscribed there. A Mailchimp that is not set up fails the run, so the next run reads the same changes again.

## Welcome coupon by Mailchimp

With the list's welcome coupon **Delivered by** the provider, the code is written to the `CouponMergeTag` merge field when the member is pushed, and the site sends no coupon e-mail; the Mailchimp welcome automation of the audience sends it. The audience must have the field. On a list whose confirmation is Mailchimp's, the code is issued when Mailchimp's webhook (or the reconciliation) reports the confirmation; the member service then queues an upsert of the member in the same unit of work, so the merge field gets the code at once.

## Telemetry

The meter `CrestApps.OrchardCore.MailingLists.Mailchimp` counts `crestapps.mailing_lists.mailchimp.webhooks`, with the `result` (`accepted`, `duplicate`, `ignored`, `rejected_url`, `rejected_signature`, `rejected_body`, `rejected_audience`, `busy`, `error`), the event `type` and the tenant. Push failures are counted by Mailing Lists' `crestapps.mailing_lists.sync_failures` with `provider = Mailchimp`.

## Limitations

- **Existing members are not copied.** Switching a list to Mailchimp pushes only the changes made after the switch; there is no step that copies the list's members to the audience, and no batch push. Members Mailchimp already holds reach the site only when they change there.
- **Changes made while the feature is off, or while Mailchimp is not set up,** wait in the provider outbox without using up an attempt, and go out once the feature is on and the connection is set up (see [Mailing Lists: providers](mailing-lists.md#providers-and-the-provider-outbox)).
- **Members added in Mailchimp.** A `subscribe` webhook for an address the site doesn't have adds it to the list as verified. The site sends it no welcome e-mail and gives it no welcome coupon, also when Mailchimp confirms it later: only a person who signed up on the site gets the code.
- **Erasing is permanent.** `delete-permanent` removes the address from the audience for good, and Mailchimp never lets it be imported again. The erase confirmations of Mailing Lists say so.
- **Facts to confirm with Mailchimp.** The behavior below follows Mailchimp's documentation and has not been checked against a live account: that calls work on the key's data-center host; that adding a member as `pending` makes Mailchimp send its own confirmation e-mail; that the API re-subscribes an address it holds as unsubscribed through `pending`; and whether the batch endpoint can copy a list's existing members.

## Related pages

- [Mailing Lists](mailing-lists.md): lists, members, the provider outbox and reconciliation.
- [Webhooks: provider webhook inbox](webhooks.md#provider-webhook-inbox).
- The `crestapps-mailing-list-provider` skill in `.agents/skills`, for writing another provider.
