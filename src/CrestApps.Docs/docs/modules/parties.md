---
sidebar_label: Parties
title: Parties
description: "The shared party reference: PartyRef with an open kind (User, Customer, Account, Contact, Guest) and an id, the IPartySource each module registers to find and name its parties, and the IPartyDirectory that reads them all."
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

## Consumers

- **Assets** reads an asset's owner through the party sources. The stored `AssetOwnerKind` keeps its values: `Customer` holds a user id and maps to the `User` party kind (`AssetOwners.ToPartyKind`, `asset.ToParty()`).
- **Work Orders** picks and names a work order's customer, account and contact through `IPartyDirectory`. `WorkOrder.CustomerId` and `ServiceAgreement.CustomerId` keep their stored name and hold a **user id** (party kind `User`).
