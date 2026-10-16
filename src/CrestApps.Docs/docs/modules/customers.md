---
sidebar_label: Customers
title: Customers
description: A customer record for everyone who buys from the store, linked to their Orchard Core user when they have an account.
user_manual: user-manual/commerce/customers
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Customers` |
| **Core** | `CrestApps.OrchardCore.Customers.Core` |
| **Category** | Commerce |
| **Depends on** | Commerce, `OrchardCore.Users` |

The **Customers** feature keeps a record of everyone who buys from the store: a person or a business, with an account or as a guest. Orders, transactions and subscriptions can point at one customer, whatever later happens to the account.

## Customers and users

A customer is not a user. Accounts stay with Orchard Core Users, which handles registration, sign-in, password resets, email confirmation and external logins. A customer with an account is linked to its user by `Customer.UserId`. The customer record keeps only what the store needs: how to address the customer, their company and tax id, and how they want to be contacted.

A guest customer has no user. Customers builds no registration of its own; a guest who wants an account registers through Orchard Core.

## The customer record

`Customer` is a `CatalogItem` stored in its own YesSql collection (`Customers`), so it carries the usual extensible properties for other modules.

| Property | |
| --- | --- |
| `Kind` | `Person` or `Business`. |
| `Status` | `Active`, `Disabled` (the store stopped doing business with them; their history stays) or `Anonymized` (their personal data was erased). |
| `UserId` | The Orchard Core user, or `null` for a guest. |
| `DisplayName`, `FirstName`, `LastName` | How the store addresses the customer. `GetName()` returns the display name, then a business's company, then the person's name, then the email. |
| `CompanyName`, `TaxId` | For a business, such as a VAT number. |
| `Email`, `Phone` | For a customer with an account, `Email` is the account's email when the record was made. |
| `AcceptsMarketing` | Whether the customer agreed to news and offers. Order and payment messages are always sent. |
| `ExternalIds` | Ids at outside services, such as a payment provider's customer id, by service name. |
| `CreatedUtc`, `ModifiedUtc`, `LastActivityUtc` | `LastActivityUtc` is when the customer last bought or changed something. |

`CustomerIndex` indexes the user id, the email, name and company in upper case for searching, the kind, the status, whether the customer has an account, and the dates.

## Services

- **`ICustomerStore`** finds customers by id or user id and pages them with a `CustomerQuery`. A search matches the start of the name, company or email, ignoring case. Saving checks concurrency, so two editors never overwrite each other silently.
- **`ICustomerService.GetOrCreateForUserAsync(userId)`** returns a user's customer, making it from the account the first time. A distributed lock keyed by the user makes two requests from the same user share one customer. `TouchAsync` records activity.
- **`ICustomerContactResolver`** is replaced by `CustomerRecordContactResolver`. A customer with an account is addressed by the name on their customer record and the account's email, so statements, receipts and reminders read the same everywhere. A guest is still addressed from the contact kept at purchase.

## Orders

With Customers enabled, an order placed by a signed-in user belongs to their customer: `Order.CustomerId` is set at checkout, and the customer's record is made on their first order. Orders keep their own copy of the customer's name and email, so an order reads the same after the customer changes.

## Admin

**Commerce > Customers** lists customers, the most recently active first, with a search by name, company or email and filters for *Active*, *With an account*, *Guests*, *Businesses*, *Disabled* and *All customers*. **Add customer** creates one by hand, for example for an order taken by phone. Each customer opens an editor for their details, with their account, when they became a customer and when they were last active.

## Permissions

| Permission | Allows |
| --- | --- |
| **Manage customers** | Adding and editing customers. Implies **View customers**. |
| **View customers** | Listing and opening customers, read-only. |

Administrators have both by default.
