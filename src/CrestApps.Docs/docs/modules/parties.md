---
sidebar_label: Parties
title: Parties
description: "The shared party reference: PartyRef with an open kind (User, Customer, Account, Contact, Guest) and an id, the IPartySource each module registers to find and name its parties, the IPartyDirectory that reads them all, and the billing profile and members of a party."
---

A **party** is the person or organization a record belongs to: the owner of an asset, the customer of a work order. The same word "customer" has meant a user id in one module and a Customers record id in another, so records name parties with a kind and an id instead.

The contracts are in `CrestApps.OrchardCore.Parties.Abstractions`. There is no feature; the modules that keep parties register sources.

## PartyRef

`PartyRef { Kind, Id }` is a value type. `Kind` is an open string; `PartyKinds` names the well-known ones:

| Kind | The id is | Source | Registered by |
| --- | --- | --- | --- |
| `User` | An Orchard Core user id | `UserPartySource` (`IUserSearchService`, named by `IDisplayNameProvider`) | The CrestApps Users module, and Assets when `OrchardCore.Users` is on |
| `Customer` | A Customers module customer record id | `CustomerPartySource` | Customers |
| `Account` | An account content item id | `AccountPartySource` | Accounts |
| `Contact` | A CRM contact content item id | Assets' contact source | Assets, when Omnichannel Management is on |
| `Guest` | A guest's id | none yet | |

`PartyRef.ToString()` writes `Kind:Id`, and `PartyRef.TryParse` reads it back.

## Finding and naming parties

Inject `IPartyDirectory` (register it with `services.AddPartyDirectory()`):

- `Kinds` lists the kinds that have a source, and `GetSource(kind)` returns it (kinds compare without case; the first source registered for a kind answers).
- `SearchAsync(kind, query, count)` finds parties of a kind for a picker; each result is a `PartyOption { Id, Name }`.
- `GetNameAsync(party)` and `GetNamesAsync(parties)` name parties, with one lookup per kind.

## Adding a kind of party

Implement `IPartySource` (`Kind`, `SearchAsync`, `GetNamesAsync`) in the module that keeps the records, or its `.Core` project, and register it:

```csharp
services.AddPartySource<VendorPartySource>();
```

## Billing a party

A business document billed to a party, such as an invoice, needs the party's billing details without a checkout. `IPartyBillingProfileService.GetAsync(party)` returns a `PartyBillingProfile`:

| Property | |
| --- | --- |
| `Party` | The party. |
| `Contact` | A `ContactInfo` (name, e-mail, E.164 phone and billing address); never `null`. |
| `ShippingAddress` | Where goods go, or `null` when it is the billing address. |
| `TaxId`, `TaxExempt` | The tax or VAT number, and whether the party pays no tax. |
| `Currency`, `Culture` | The ISO 4217 currency and the culture of its documents, or `null` for the site's. |
| `PaymentTermId`, `NetDays` | The id of a payment term record, or the days a document is due in where the owner keeps only a day count. |
| `CreditLimit` | The most the party may owe at once, or `null`. |
| `CopyTo` | Extra e-mail addresses that get its billing documents; `AddCopyTo(email)` leaves out blanks, repeats and the contact's own address. |

Every `IPartyBillingProfileSource` of the party's kind adds what it knows, in registration order: a later source fills or overrides what an earlier one set. When no source names the party, the name comes from `IPartyDirectory`. The service returns a profile for any party with a kind and an id, even when nothing is known about it, and `null` for an empty reference.

| Kind | Sources, in order | Registered by |
| --- | --- | --- |
| `User` | `UserBillingProfileSource`: the display name from `IDisplayNameProvider`, the e-mail, and the user's phone in E.164 form (a phone typed another way, such as with spaces, is normalized when the Phone Numbers feature is on) | The CrestApps Users module |
| `Customer` | `CustomerBillingProfileSource`: the record's name, e-mail, phone (normalized to E.164 with the billing address's country when the Phone Numbers feature is on), default billing address, default shipping address when it is another place, tax id and exemption | [Customers](customers.md) |
| `Account` | `AccountBillingProfileSource`: the account's name and its [billing location](accounts.md#account-locations); then `B2BAccountBillingProfileSource`: the tax number, the billing e-mail and the terms | [Accounts](accounts.md), then [Accounts B2B](accounts-b2b.md#billing-profile) |
| `Contact` | `ContactBillingProfileSource`: the contact's name, first e-mail, the number it is called on and the first address field on its type that holds an address, in editor order (part position, then field position, then stored order) | Omnichannel Management |

A module that keeps more about a kind registers a source in its own startup:

```csharp
services.AddPartyBillingProfileSource<VendorBillingProfileSource>();
```

`AddPartyBillingProfileSource<T>()` also registers the service; `services.AddPartyBillingProfiles()` registers it alone, for a feature that reads profiles without keeping parties. The [tax profile of a party](taxation.md#tax-profile-of-a-party) is built from the billing profile.

## Who may act for a party

`IPartyMembershipService` answers who may act for a party, across every kind, through the `IPartyMemberSource` of each kind:

- `GetMemberUserIdsAsync(party)` returns the user ids of the party's members, each once.
- `IsMemberAsync(party, userId)` tells whether a user is one of them.
- `GetPartiesAsync(userId)` returns the parties a user may act for, always starting with the user themselves as `PartyRef(User, userId)`.

A user acts for themselves even when no source of users is registered.

| Kind | Members | Source | Registered by |
| --- | --- | --- | --- |
| `User` | The user | `UserPartyMemberSource` | The CrestApps Users module |
| `Customer` | The user the record belongs to; a guest's record has none | `CustomerPartyMemberSource` | Customers |
| `Account` | The account's members | `AccountPartyMemberSource` (`IAccountMemberService.GetMemberUserIdsAsync`) | Account Members |
| `Contact` | None | | |

Register a source with `services.AddPartyMemberSource<T>()`, or the service alone with `services.AddPartyMembership()`.

## Consumers

- **Assets** reads an asset's owner through the party sources. The stored `AssetOwnerKind` keeps its values: `Customer` holds a user id and maps to the `User` party kind (`AssetOwners.ToPartyKind`, `asset.ToParty()`).
- **Work Orders** picks and names a work order's customer, account and contact through `IPartyDirectory`. `WorkOrder.CustomerId` and `ServiceAgreement.CustomerId` keep their stored name and hold a **user id** (party kind `User`).
