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
| `Addresses`, `DefaultBillingAddressId`, `DefaultShippingAddressId` | The address book, below. |

`CustomerIndex` indexes the user id, the email, name and company in upper case for searching, the kind, the status, whether the customer has an account, and the dates.

## Address book

A customer keeps up to 20 `CustomerAddress` entries on the record itself. Each has an `AddressId`, a `Label` (such as *Home*) and an `Address`: the shared, immutable value from the [Addresses](addresses) module, so a customer's address is the same type an order, a tax rule or a content field holds.

- `GetDefaultBillingAddress()` and `GetDefaultShippingAddress()` return the defaults, or `null`. `RemoveAddress(addressId)` also clears a default that named the address.
- `ToAddress()` returns a copy of the `Address`. An order stores that copy, so editing or deleting an address never changes a past order.

The address pages use the Addresses module's editor (`IAddressEditorService` and the `AddressEditor` shape, posted under the `Address` prefix), so they lay the form out for the chosen country and check the address against its rules: the parts the country requires, its postal code pattern, a region from its list, and a postal code within that region. The address book also requires the recipient's name and offers a phone, and a new address starts in the country of the customer's other addresses. Addresses are listed and printed through `IAddressFormatter`, the way each country writes them. Customers depends on the Addresses feature for all of this.

## Services

- **`ICustomerStore`** finds customers by id or user id and pages them with a `CustomerQuery`. A search matches the start of the name, company or email, ignoring case. Saving checks concurrency, so two editors never overwrite each other silently.
- **`ICustomerService.GetOrCreateForUserAsync(userId)`** returns a user's customer, making it from the account the first time. A distributed lock keyed by the user makes two requests from the same user share one customer. `TouchAsync` records activity.
- **`ICustomerContactResolver`** is replaced by `CustomerRecordContactResolver`. A customer with an account is addressed by the name on their customer record and the account's email, so statements, receipts and reminders read the same everywhere. A guest is still addressed from the contact kept at purchase.

## Orders

With Customers enabled, an order placed by a signed-in user belongs to their customer: `Order.CustomerId` is set at checkout, and the customer's record is made on their first order. Orders keep their own copy of the customer's name and email, so an order reads the same after the customer changes.

## The checkout's Contact step

With Orders enabled, Customers adds a **Contact** step to every order's checkout (`CustomersConstants.ContactStepKey`, right after the hidden order step and before payment). `CustomerContactCheckoutHandler` adds it, and `CustomerContactCheckoutFlowDisplayDriver` renders it as the `CustomerContactStep_Edit` shape.

- **What it asks:** a name and an email (required), a phone, and a billing address as `CustomerCheckoutSettings.BillingAddress` says: `Optional` (the default), `Required` or `Off`, under **Settings > Commerce > Customers** with the **Manage customers** permission. The checkout keeps the setting it started with, stored on the session, so changing it never strands a checkout. A signed-in buyer never sees the step when the setting is `Off`.
- **The billing address** uses the Addresses module's `AddressEditor` under the step's prefix, without a recipient name (the buyer named above is the recipient) and with the buyer's phone. A signed-in customer can pick one of their saved addresses and ask for a new one to be saved.
- **What it keeps:** the entries as `CustomerContactStep` in the session's saved steps, and a `CheckoutContactInfo` (name, email, phone, billing address) on the session. The order copies that contact and billing address when it is paid, and Pay Later puts the guest's name and email on its transaction, so receipts and reminders reach them.
- **When the order is paid** (`CompletingAsync`, inside the completion's transaction): the order is linked to the signed-in buyer's customer, or to a new guest customer made with `ICustomerService.CreateGuestAsync`; each guest checkout makes its own, since an email typed at checkout proves nothing. A guest's billing address goes into their address book as the default billing address; a signed-in customer's new address goes in when they asked, never twice.

## Merging guests into an account

`ICustomerMergeService.MergeVerifiedGuestsAsync(userId)` joins a guest's history to their account. It runs when a user signs in (`ILoginFormEvent.LoggedInAsync`), registers (`IRegistrationFormEvents.RegisteredAsync`) or confirms their email (`IUserEventHandler.ConfirmedAsync`), and does nothing unless the user's `EmailConfirmed` is true. Under a per-user distributed lock it takes every active guest customer with the user's email (`ICustomerStore.ListGuestsByEmailAsync`) and, for each:

- calls every `ICustomerMergeHandler`. With Orders on, `OrderCustomerMergeHandler` moves the guest's orders to the account's customer (setting `UserId` too, so they appear under My Orders) and, with Transactions on, turns the guest transactions paying for them into the user's;
- moves the guest's addresses into the account's book, skipping duplicates, up to 20;
- marks the guest `Merged` with `MergedIntoCustomerId` and `MergedUtc`, and adds it to the account customer's `MergedCustomerIds`.

The event handlers resolve the merge service when they run: Orchard Core's user store and user service create them, and the merge service needs the user service, so injecting it would be a circular dependency. A failed merge is logged and tried again on the next sign-in.

## Privacy

`ICustomerPrivacyService` answers access and erasure requests and applies the retention windows.

- **`EraseAsync(customer, by)`** calls every `ICustomerPrivacyHandler.ErasingAsync`, then `Customer.Anonymize`: names, company, tax id, email, phone, addresses, outside ids and marketing consent are cleared, the account is unlinked (`UserId`), and the status becomes `Anonymized`. With Orders on, `OrderCustomerPrivacyHandler` anonymizes the customer's orders (by `CustomerId`, and by `UserId` for an account holder's older orders) through `OrderAnonymizer`: `Order.Anonymize` clears the name, email and phone and keeps only the billing address's country and region, and the guest contact of the transactions paying for the order is cleared. Handlers see the account before it is unlinked (`CustomerPrivacyContext.UserId`). Erasing an anonymized customer does nothing.
- **`ExportAsync(customer, by)`** returns a `JsonObject` with the customer and their addresses; each handler's `ExportingAsync` adds its own section (`orders`, each with its `transactions`).
- **`ExpireAsync(customer)`** erases as the retention task.

Each export and erasure appends a `CustomerPrivacyEntry` (`Utc`, `Action`: `Exported`, `Erased` or `Expired`, `By`) to `Customer.PrivacyLog`.

**Retention.** `CustomerRetentionBackgroundTask` runs daily at 03:30 under the background-task lock and reads `CustomerPrivacySettings` (Settings › Commerce › Customers; zero turns a window off):

- `InactiveGuestMonths` (24): guests (`HasAccount = false`, merged ones included) whose `LastActivityUtc` is older are expired. Customers with an account are not, since the account holds the same data; erase them on request.
- `GuestOrderMonths` (12): orders placed without an account (`OrderQuery.GuestsOnly`), `Paid` or `Canceled`, created before the window, are anonymized. Orders awaiting payment keep their contact.

Each run handles up to 500 of each, and queries leave out what is already anonymized (`CustomerQuery.NotAnonymized`, `OrderQuery.NotAnonymized` on the new `OrderIndex.AnonymizedUtc`, upgrade step 4), so a backlog is worked off over a few days.

**Admin.** `POST customers/{customerId}/erase` and `GET customers/{customerId}/export` (a `customer-{id}.json` download) need **Manage customers**.

## Admin

**Commerce > Customers** lists customers, the most recently active first, with a search by name, company or email and filters for *Active*, *With an account*, *Guests*, *Businesses*, *Disabled* and *All customers*. **Add customer** creates one by hand, for example for an order taken by phone. Each customer opens an editor for their details, with their account, when they became a customer and when they were last active, and their address book (`customers/{customerId}/addresses/...`). An anonymized customer's addresses can't be edited.

**My Account** (`my-account`) is the signed-in user's own page: their customer details and address book. It is an admin-area page marked `[CustomerAdminPage]`, so a customer reaches it without **Access admin panel**. The first visit makes the user's customer record with `GetOrCreateForUserAsync`; a disabled or anonymized customer can't edit it.

## Permissions

| Permission | Allows |
| --- | --- |
| **Manage customers** | Adding, editing, exporting and erasing customers, and the Customers settings. Implies **View customers**. |
| **View customers** | Listing and opening customers, read-only. |
| **Manage own customer profile and addresses** | Using **My Account**. Granted to the Authenticated role by default. |

Administrators have the first two by default.
