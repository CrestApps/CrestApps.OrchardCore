# Mailing Lists

Mailing lists people sign up to, with or without commerce: double opt-in by e-mail (or single opt-in, or the provider's own confirmation), the evidence of each person's consent, a newsletter sign-up widget, a one-time welcome coupon, unsubscribe links on the shared e-mail preferences page, and a provider seam that keeps a list at an outside e-mail marketing service such as Mailchimp. The module manages lists and members; it does not write or send newsletters.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Mailing Lists | `CrestApps.OrchardCore.MailingLists` | Lists and members, verification links, the confirmation, welcome and coupon claim e-mails, the members admin, the provider outbox and daily reconciliation, retention of unconfirmed sign-ups, personal data, recipes and deployment, workflows. |
| Mailing List Sign-up Widget | `CrestApps.OrchardCore.MailingLists.SignupWidget` | The `MailingListSignup` widget, protected by a trap field, the site's ReCaptcha and a rate limit. |
| Mailing List Welcome Coupon | `CrestApps.OrchardCore.MailingLists.WelcomeCoupon` | A one-time code of a Checkout coupon for each new address, once per list and address, ever. |

## Dependencies

- Mailing Lists: E-mail Preferences (`CrestApps.OrchardCore.Consent`), CrestApps Resources (`CrestApps.OrchardCore.Resources`), Orchard Core Email (`OrchardCore.Email`).
- Mailing List Sign-up Widget: Mailing Lists, Orchard Core Widgets, Orchard Core Rate Limits.
- Mailing List Welcome Coupon: Mailing Lists, Checkout (`CrestApps.OrchardCore.Checkout`).
- Optional: Timeline (member history), Orchard Core Workflows (events and tasks), Orchard Core Recipes and Deployment, CrestApps Recipes (schemas).

## Installation

Reference the `CrestApps.OrchardCore.MailingLists` package (or `CrestApps.OrchardCore.Cms.Core.Targets`), then turn on **Mailing Lists** under **Tools** → **Features**, and the sign-up widget and welcome coupon features when you need them. Set up the site's e-mail and its base URL (**Settings** → **General**): unsubscribe links are built on it.

## Configuration

Optional, in `OrchardCore:CrestApps:MailingLists` (a tenant's own `appsettings.json`: `CrestApps:MailingLists`):

```json
{
  "OrchardCore": {
    "CrestApps": {
      "MailingLists": {
        "SyncBatchSize": 25,
        "SyncMaxAttempts": 8,
        "PendingRetentionDays": 30,
        "ConfirmationCap": 3,
        "ConfirmationCapMinutes": 15
      }
    }
  }
}
```

The e-mail templates are under **Settings** → **Mailing Lists** (permission `ManageMailingListSettings`).

## Usage

1. Create a list under **Mailing Lists** → **Lists**: its name, consent text, how sign-ups are confirmed, and where it lives.
2. Add a **Mailing List Sign-up** widget to a page, or sign people up from a workflow with the **Subscribe to Mailing List** task.
3. See and manage members on the list's **Members** page.

From code, sign people up through `IMailingListMemberService`:

```csharp
var result = await memberService.SubscribeAsync(new MailingListSubscribeRequest
{
    ListId = list.ItemId,
    Email = email,
    Source = "Checkout",
    ClientAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
    ConsentForm = "/checkout",
});
```

## Contracts

- `CrestApps.OrchardCore.MailingLists.Abstractions`: `MailingList`, `MailingListMember`, `IMailingListManager`, `IMailingListMemberService`, `IMailingListMemberObserver` (`AddMailingListMemberObserver<T>()`), `IMailingListProvider` (`AddMailingListProvider<T>()`), `IMailingListProviderResolver`, `MailingListsConstants`, `MailingListsPermissions`.
- `CrestApps.OrchardCore.MailingLists.Core`: the stores and indexes, `DefaultMailingListMemberService`, the Local provider, the provider outbox and reconciliation, the e-mails, the consent source, the unsubscribe scope handler, the Outreach audience source, retention and personal data (`AddMailingListsCore()`).

## Documentation

See [Mailing Lists](../../CrestApps.Docs/docs/modules/mailing-lists.md) in the technical manual and [Mailing Lists](../../CrestApps.Docs/docs/user-manual/mailing-lists/index.md) in the user manual. Providers: [Mailchimp](../../CrestApps.Docs/docs/modules/mailing-lists-mailchimp.md).
