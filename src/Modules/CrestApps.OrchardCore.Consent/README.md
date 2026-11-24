# E-mail Preferences (Consent)

The site's one e-mail preferences page and its signed unsubscribe links. Every marketing e-mail carries one link to `email-preferences?token=…`, where the person stops the list or campaign the e-mail came from, or all marketing e-mail; mail apps can post the RFC 8058 one-click unsubscribe to the same link. A "stop all marketing" is kept for the address, also when the person is neither a customer nor a CRM contact, and it is kept when the person's data is erased so their objection is still honored.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| E-mail Preferences | `CrestApps.OrchardCore.Consent` | Dependency only. The consent service, the address opt-outs and their consent source, the unsubscribe links, the e-mail preferences page, personal data, and the `ConsentAddressOptOuts` recipe and deployment step. |

## Dependencies

None. Mailing Lists and Marketing depend on it and turn it on. Orchard Core Recipes and Deployment, when on, add the recipe and deployment step.

## Installation

Reference the `CrestApps.OrchardCore.Consent` package (it comes with Mailing Lists, Marketing and `CrestApps.OrchardCore.Cms.Core.Targets`). The feature turns on with the features that need it. Set the site's base URL (**Settings** → **General**): the links are built on it.

## Configuration

Nothing to configure. A link works for 400 days.

## Usage

Create a link for an address and a scope, and put it in the e-mail:

```csharp
var url = await unsubscribeLinks.CreateUrlAsync(email, UnsubscribeScope.AllMarketing);
```

Ask about consent for a purpose before a marketing send:

```csharp
var status = await consentService.GetAsync(new ConsentQuery
{
    Channel = ConsentChannels.Email,
    Address = email,
    Purpose = ConsentPurposes.Marketing,
});
```

Add a scope of your own (one per kind of thing a person can leave) with an `IUnsubscribeScopeHandler` and `services.AddUnsubscribeScope<T>()`.

## Contracts

- `CrestApps.OrchardCore.Consent.Abstractions`: `IConsentService`, `IConsentSource`, `ConsentQuery` and `ConsentChange` (with `Purpose`), `ConsentPurposes`, `ConsentSourceNames`, `ConsentConstants`, and in `CrestApps.OrchardCore.Consent.Unsubscribe`: `IUnsubscribeLinks`, `IUnsubscribeScopeHandler`, `UnsubscribeScope`, `UnsubscribeTicket`, `AddUnsubscribeScope<T>()`.
- `CrestApps.OrchardCore.Consent.Core`: `DefaultConsentService`, `ConsentAddressOptOut` and its store, `AddressOptOutConsentSource`, `UnsubscribeLinks` (`AddConsent()`, `AddConsentSource<T>()`, `AddConsentAddressOptOuts()`, `AddUnsubscribeLinks()`).

## Documentation

See [E-mail Preferences and Consent](../../CrestApps.Docs/docs/modules/consent.md) in the technical manual and [The E-mail Preferences Page](../../CrestApps.Docs/docs/user-manual/mailing-lists/email-preferences.md) in the user manual.
