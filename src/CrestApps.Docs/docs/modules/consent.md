---
sidebar_label: E-mail Preferences (Consent)
title: E-mail Preferences and Consent
description: "The site's one e-mail preferences page and its signed unsubscribe links, the RFC 8058 one-click unsubscribe, consent purposes (marketing or every purpose), and the opt-outs kept for an address that no customer or contact holds."
user_manual:
  - user-manual/mailing-lists/email-preferences
---

| | |
| --- | --- |
| **Module** | `CrestApps.OrchardCore.Consent` (E-mail Preferences) |
| **Core** | `CrestApps.OrchardCore.Consent.Core` |
| **Abstractions** | `CrestApps.OrchardCore.Consent.Abstractions` (namespaces `CrestApps.OrchardCore.Consent` and `CrestApps.OrchardCore.Consent.Unsubscribe`) |
| **Category** | Communication |
| **Used by** | [Mailing Lists](mailing-lists.md), [Marketing](marketing.md#unsubscribing) |

The Consent building block answers one question for every send, dial and campaign: may this person be contacted on this channel, for this purpose? `IConsentService` asks every `IConsentSource` and records every opt-out and opt-in with every source (see [Consent: may we reach them?](../omnichannel/management.md#consent-may-we-reach-them)).

The **E-mail Preferences** module adds to it:

- one e-mail preferences page for the whole solution, which every unsubscribe link opens;
- signed unsubscribe links with a scope (one mailing list, or all marketing e-mail), and the RFC 8058 one-click `POST`;
- the opt-outs kept for an address, so a person who is neither a customer nor a CRM contact can still say "stop all marketing".

What recipients see on the page is described in the User Manual: [E-mail Preferences Page](../user-manual/mailing-lists/email-preferences.md).

## Feature

| Feature | ID | Depends on |
| --- | --- | --- |
| E-mail Preferences (dependency only) | `CrestApps.OrchardCore.Consent` | Nothing |

The feature can't be turned on by itself: Mailing Lists and Marketing depend on it and turn it on. It registers the consent service, the address opt-outs and their consent source, the unsubscribe links, the page and the personal data contributor. With Orchard Core Recipes and Deployment on, it adds the `ConsentAddressOptOuts` step.

## Purposes

A consent question and a change can name a **purpose**: why the site contacts the person. `ConsentQuery.Purpose` and `ConsentChange.Purpose` are open strings; `ConsentPurposes.Marketing` (`Marketing`: newsletters, campaigns, offers) is the one the platform uses. Empty means every purpose, which is what callers that predate purposes ask and record.

- An opt-out **with a purpose** blocks the questions for that purpose only. A person who stopped marketing still gets what they asked for: a sign-up confirmation, an order notice, a reply to their question.
- An opt-out **with no purpose** blocks every question, whatever its purpose. A CRM contact's **Do not email** stops marketing too, but only for a send that names that contact: the flag is kept on the contact, not on the address, so a question by address alone (a mailing list sign-up or send) or as a customer (a Marketing campaign) doesn't see it.
- A question **with no purpose** asks only whether every purpose is blocked.

How the sources ship:

| Source (`ConsentSourceNames`) | Keeps | Purposes |
| --- | --- | --- |
| `contact-preferences` (Omnichannel) | A CRM contact's **Do not call**, **Do not text** and **Do not email** flags, and the opt-outs kept for an address no contact held | Every purpose. It answers questions of any purpose, and records only changes with no purpose, so a "stop all marketing" never sets **Do not email**. An e-mail flag is read from the contact a question names; an address-only question reads only the address opt-outs. |
| `customer-marketing` (Customers) | A customer's **Accepts marketing** | `Marketing`. It answers questions that name a customer (`ContactKind = Customer`), with the Marketing purpose or none: by the customer's id, or by address when no customer at the address accepts marketing. It records Marketing changes on the named customer, or on every customer that holds the address. |
| `address-opt-outs` (this module) | `ConsentAddressOptOut`: one opt-out per channel, address and purpose | A purpose only. It answers a question for a purpose when the address opted out of that purpose on the channel, whoever the question names. An opt-out is saved; an opt-in deletes it. |
| `do-not-call-lists` | The site's own do-not-call lists | Phone only. |
| `mailing-lists` (Mailing Lists) | Nothing | It only reacts: an `Email` opt-out with the Marketing purpose or none unsubscribes the address from every mailing list. See [Mailing Lists: consent](mailing-lists.md#consent). |

Outreach runs pass their purpose too: `OutreachRun.ConsentPurpose` (see [Outreach](outreach.md#consent-and-opt-outs)). Marketing campaigns send with `Marketing`.

## Address opt-outs

`ConsentAddressOptOut` (`Channel`, `Address`, `Purpose`, `Source`, `OptedOutUtc`) is a YesSql document with `ConsentAddressOptOutIndex` (address, channel, purpose). An e-mail address is stored trimmed and in lower case, a phone number in E.164 form (`ConsentAddresses.Normalize`). `IConsentAddressOptOutStore` lists, saves, deletes and pages them. Only opt-outs with a purpose are kept here; an opt-out of every purpose at an address no contact holds stays in the CRM's address opt-outs.

## Unsubscribe links

`IUnsubscribeLinks.CreateUrlAsync(address, scope)` returns the absolute link for an address and an `UnsubscribeScope { Kind, Id }`:

- the token is signed with `SignedTokenService` for the purpose `CrestApps.Consent.Unsubscribe` and holds versioned JSON (the normalized address, the scope kind and id);
- a link keeps working for 400 days (`UnsubscribeLinks.Lifetime`), long enough for an e-mail that sits in an inbox for a year;
- the link is the site's base URL followed by `email-preferences?token=…` (the token rides in the query string: IIS refuses a URL path segment longer than 260 characters, and a token can be longer; links with the token in the path, `email-preferences/{token}`, still work). Without a base URL, or an address, it returns `null`.

`TryRead(token, out ticket)` gives back the `UnsubscribeTicket` (address and scope) when the token was made by this tenant and has not expired.

| Scope kind | Id | Handled by | What it stops |
| --- | --- | --- | --- |
| `AllMarketing` (`UnsubscribeScope.AllMarketing`) | Empty | This module | All marketing e-mail to the address |
| `MailingList` | The list's id | Mailing Lists (`MailingListUnsubscribeScopeHandler`) | One mailing list |

A module adds a kind with an `IUnsubscribeScopeHandler` (see [Extension points](#extension-points)). A link whose kind no enabled feature handles is treated as all marketing, the broadest stop it can be.

## The e-mail preferences page

`EmailPreferencesController` serves the page anonymously: the signed token is the authorization, as an unsubscribe link must work for a person without an account. Every answer carries `X-Robots-Tag: noindex, nofollow`, `Cache-Control: no-store` and `Referrer-Policy: no-referrer`, because the token in the path is a key to the person's preferences.

| Request | What it does |
| --- | --- |
| `GET email-preferences?token=` | Shows the page. It changes nothing, so a mail scanner that follows the link unsubscribes nobody. A scope link shows **Unsubscribe** (the scope) and **Stop all marketing e-mail**; an all-marketing link shows one **Unsubscribe** button. A link that is not valid says so. |
| `POST email-preferences/update?token=` | The page's buttons, with the form's antiforgery token. `stopAll=false` stops the scope; `stopAll=true` also stops all marketing. After a scope is stopped, the page offers **Stop all marketing e-mail** too. |
| `POST email-preferences?token=` | The RFC 8058 one-click unsubscribe. A mail app posts the form value `List-Unsubscribe=One-Click` without an antiforgery token; the link's scope is stopped (all marketing for an all-marketing link or an unknown kind). It answers 200, or 400 for a missing form value or a token that is not valid. |

"Stop all marketing e-mail" records `ConsentChange { OptedOut = true, Purpose = Marketing, Source = "unsubscribe-link" }` for the address on the `Email` channel. Every source hears it: the address opt-outs keep it, Customers turns **Accepts marketing** off on every customer that holds the address, and Mailing Lists unsubscribes the address from every list. A CRM contact's **Do not email** is not set, so the person's own conversations and order notices still come.

:::note[One-click headers]
The page accepts the one-click `POST`, but the site's own e-mails carry the unsubscribe link in the body only. Orchard Core's `MailMessage` can't carry the `List-Unsubscribe` and `List-Unsubscribe-Post` headers that mail apps read to show their own unsubscribe button, so mail apps don't offer one-click for e-mail the site sends through Orchard Core's e-mail service. A sender that can set headers (an outside provider) can point its header at the same link.
:::

The page is the `EmailPreferences/Index` view of the module, so a theme can override it.

## Marketing campaigns

Marketing campaigns link to the page with the `AllMarketing` scope. The links campaigns carried before (`marketing/unsubscribe?token=`) keep working until they expire, and now record their opt-out with the `Marketing` purpose. See [Marketing: unsubscribing](marketing.md#unsubscribing).

## Recipes and deployment

The `ConsentAddressOptOuts` step adds address opt-outs, matched by channel, address and purpose, so running it twice adds nothing; an opt-out already recorded takes the imported source and time. The records are written to the store as they are, without asking the other sources to record them again. An entry needs a `Channel`, an `Address` and a `Purpose`; `OptedOutUtc` is the import time when it is empty.

```json
{
  "steps": [
    {
      "name": "ConsentAddressOptOuts",
      "OptOuts": [
        {
          "Channel": "Email",
          "Address": "john.smith@contoso.com",
          "Purpose": "Marketing",
          "Source": "unsubscribe-link",
          "OptedOutUtc": "2026-11-22T10:00:00Z"
        }
      ]
    }
  ]
}
```

The deployment step **Address opt-outs** (category Communication) exports every address opt-out, read a page at a time. The opt-outs hold e-mail addresses and phone numbers, which are personal data.

## Privacy

`ConsentAddressOptOutPersonalDataContributor` exports the opt-outs kept for the person's e-mail addresses and phone numbers, and **keeps them when the person's data is erased**. The opt-out is what lets the site honor the person's objection: under GDPR Article 21(3), personal data may no longer be processed for direct marketing once the person objects, and laws such as CAN-SPAM and CASL require an unsubscribe to be honored. Erasing the record would let the address be sent marketing again, for example after an import or a new sign-up form. The record holds only the address, the purpose, where it came from and when: the least needed to honor the objection (GDPR Article 17(3)(b) and (e)).

## Extension points

| Contract | Register with | What it is for |
| --- | --- | --- |
| `IConsentSource` | `services.AddConsentSource<T>()` (`Consent.Core`) | A place consent is kept. Answer only the channels, record kinds and purposes you keep; follow the [purpose rules](#purposes). |
| `IUnsubscribeScopeHandler` | `services.AddUnsubscribeScope<T>()` (`Consent.Abstractions`) | One scope kind on the page: `Kind`, `GetTitleAsync(scope)` (the name the page shows, or `null` when it no longer exists) and `UnsubscribeAsync(ticket)`, which must be safe to call twice. |
| `IUnsubscribeLinks` | `services.AddUnsubscribeLinks()` (registered by the feature) | Creates and reads the links. Depend on the E-mail Preferences feature (`ConsentConstants.Features.Consent`) to have it. |
| `ConsentQuery.Purpose`, `ConsentChange.Purpose` | | Name why you contact the person. Ask with `ConsentPurposes.Marketing` before a marketing send, so an address's "stop all marketing" is honored. |

A module that sends marketing e-mail for its own records adds a scope:

```csharp
public sealed class WebinarUnsubscribeScopeHandler : IUnsubscribeScopeHandler
{
    public string Kind => "Webinar";

    public async Task<string> GetTitleAsync(UnsubscribeScope scope, CancellationToken cancellationToken = default)
        => (await _webinars.FindByIdAsync(scope.Id, cancellationToken))?.Title;

    public Task UnsubscribeAsync(UnsubscribeTicket ticket, CancellationToken cancellationToken = default)
        => _attendees.StopRemindersAsync(ticket.Scope.Id, ticket.Address, cancellationToken);
}

// In the feature's startup:
services.AddUnsubscribeScope<WebinarUnsubscribeScopeHandler>();

// When sending:
var url = await unsubscribeLinks.CreateUrlAsync(attendee.Email, new UnsubscribeScope { Kind = "Webinar", Id = webinar.Id });
```
