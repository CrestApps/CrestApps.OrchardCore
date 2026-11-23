# Mailing Lists Mailchimp

The Mailchimp provider of mailing lists. A list whose provider is Mailchimp keeps its members in a Mailchimp audience: each member change is pushed to Mailchimp through the provider outbox (idempotently, on the member's subscriber hash), Mailchimp's own double opt-in can confirm sign-ups, the welcome coupon code can be written to a merge field for a Mailchimp welcome automation, and Mailchimp's webhooks and a daily reconciliation bring unsubscribes, bounces and confirmations back. The API key stays in Orchard Core Secrets.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Mailing Lists Mailchimp | `CrestApps.OrchardCore.MailingLists.Mailchimp` | The `Mailchimp` provider, the **Mailchimp** card on **Settings > Mailing Lists** and on the list editor, and the webhook endpoint `mailing-lists/mailchimp/webhooks/{listId}/{secret}`. |

## Dependencies

- Mailing Lists (`CrestApps.OrchardCore.MailingLists`).
- Provider Webhook Inbox (`CrestApps.OrchardCore.ContactCenter.ProviderInbox`, in the Webhooks module).
- Orchard Core Secrets (`OrchardCore.Secrets`).

## Installation

Reference the `CrestApps.OrchardCore.MailingLists.Mailchimp` package (or `CrestApps.OrchardCore.Cms.Core.Targets`) and turn on **Mailing Lists Mailchimp** under **Tools > Features**. The site needs a base URL that Mailchimp can reach over HTTPS for the webhooks.

## Configuration

1. Create an API key in Mailchimp (**Profile > Extras > API keys**) and save it as an Orchard Core text secret, such as `Mailchimp.ApiKey`.
2. On **Settings > Mailing Lists**, choose the secret in **API key secret**, set **Server prefix** when the key doesn't end with its data center, save, and click **Test the connection**.

Or in a recipe (create the secret with the `Secrets` step first):

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

## Usage

1. On a list's editor, set **Where the list lives** to **Mailchimp** and save.
2. In the **Mailchimp** card, choose the **Audience**, add **Tags** if you like, and save.
3. Click **Register the webhook**.
4. To let Mailchimp send the confirmation e-mail, set the list's **Confirm sign-ups** to **The provider sends its own confirmation e-mail**.

## Limitations

- Existing members are not copied to the audience when a list is moved to Mailchimp; there is no batch push.
- Some Mailchimp behavior still has to be confirmed against a live account: calls on the key's data-center host, `pending` making Mailchimp send its confirmation e-mail, re-subscribing an unsubscribed address through `pending`, and the batch endpoint.

## Contracts

- `CrestApps.OrchardCore.MailingLists.Mailchimp.Core`: `MailchimpMailingListProvider`, `MailchimpClient`, `MailchimpPushPlanner`, `MailchimpChangeMapper`, `MailchimpWebhookParser`, `MailchimpWebhookSignature`, `MailchimpWebhookInboxHandler`, `MailchimpSettings`, `MailchimpListSettings`, `IMailchimpCredentialsProvider` (`AddMailchimpMailingLists()`).

## Documentation

See [Mailing Lists Mailchimp](../../CrestApps.Docs/docs/modules/mailing-lists-mailchimp.md) in the technical manual and [Mailchimp](../../CrestApps.Docs/docs/user-manual/mailing-lists/mailchimp.md) in the user manual. To write another provider, follow the `crestapps-mailing-list-provider` skill in `.agents/skills`.
