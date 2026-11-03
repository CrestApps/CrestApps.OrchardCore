---
sidebar_label: Accounts B2B
title: Accounts B2B
description: "Accounts B2B: an account that buys as a business, with buyers, roles, locations and payment terms (a deposit at checkout, the rest invoiced in net days, under a credit limit), purchase order numbers, quotes, price lists through the IBuyerPriceProvider seam, approvals and monthly budgets, and OCI punchout from a procurement system."
user_manual: user-manual/commerce/b2b-accounts
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Accounts.B2B` (Accounts B2B) |
| **Category** | Commerce |
| **Depends on** | [Accounts](accounts.md) (`CrestApps.OrchardCore.Accounts`), Commerce, Orders (`CrestApps.OrchardCore.Orders`), Addresses, Transactions |
| **Seams it fills** | `ICheckoutTenderProvider`, `ICheckoutHandler`, `ICheckoutPaymentGate` (Checkout), `IOrderEventHandler` (Orders), `IBuyerPriceProvider` (Products), `ICartActionProvider` (Carts) |

The **Accounts B2B** feature sells to businesses. The business is an [account](accounts.md), the same `Account` content item the CRM and Field Service use; the feature adds `B2BAccountPart` to it. An account that buys as a business has buyers, some of them admins, the places it ships to, the terms it pays on, a price list, limits and a monthly budget that send orders for approval, and a punchout key for its procurement system. The admin screens are described in the [User Manual](../user-manual/commerce/b2b-accounts.md).

## Model

The feature's migration defines `B2BAccountPart` and adds it to the `Account` type at position 10. Another account type can be given the part by hand.

| `B2BAccountPart` | |
| --- | --- |
| `Enabled` | Whether the account buys as a business. While it is off, its buyers buy for themselves. |
| `TaxId`, `BillingEmail` | The tax number prints on its invoices. |
| `Buyers` | `AccountBuyer`: `UserId`, `UserName`, `Email`, `Role` (`Buyer` or `Admin`), `OrderLimit` (store currency, or none), `ContactContentItemId`. A user buys for one account. |
| `Locations` | `AccountLocation`: `LocationId`, `Name`, `Address`, offered at checkout. |
| `Terms` | `AccountTerms`: `NetDays`, `DepositPercent`, `CreditLimit`, `RequirePurchaseOrder`. `OffersTerms` is true when `NetDays` is set. |
| `PriceListId` | The `PriceList` its buyers pay, or none. |
| `MonthlyBudget` | What its buyers may order in a calendar month, in the store's currency, or none. |
| `Punchout` | `AccountPunchout`: `Enabled`, `UserId` (the buyer the procurement system shops as), `KeyHash` (SHA-256 of the key, hex), `KeyCreatedUtc`. |

The account's name is its title. `B2BAccount` reads an account content item together with its part: `AccountId` (the content item id), `Name`, `IsEnabled` (the part is on and the version is published) and the part's values.

### Indexes

| Index | Columns | |
| --- | --- | --- |
| `B2BAccountIndex` | `ContentItemId`, `Enabled`, `PriceListId`, `Published`, `Latest` | One row per version of an account with the part. Backs the `b2b:` content list term, the price list lookup and the report data sets. |
| `B2BAccountBuyerIndex` | `AccountContentItemId`, `UserId`, `Published`, `Latest` | One row per buyer, so `IB2BAccountStore.FindByBuyerAsync(userId)` finds the published account a user buys for. |

`B2BAccountService.GetCurrentAsync()` returns the signed-in user's enabled account and buyer, cached for the request.

### Buyers and contacts

When an account is saved, `B2BAccountPartHandler` links each buyer to the account's contact with the buyer's email, through the registered `IAccountContactFinder`s (see [Accounts](accounts.md)). The CRM registers one that matches the contact's primary email among the contacts in the account. A buyer with no match, or more than one, has no contact.

## Payment terms and the Business account step

`B2BCheckoutHandler` adds a `business` step (order 3, after contact and shipping) to an account buyer's checkout of an order. `B2BCheckoutStepService.SaveAsync(session, purchaseOrderNumber, requestApproval)` checks the number (required when `RequirePurchaseOrder`, 64 characters at most), puts it and the `AccountId` on the order, and saves the step. An order that already has a purchase order number, such as a quote asked for with one, starts with the step done.

`AccountTermsTenderProvider` (`ICheckoutTenderProvider`, key `account-terms`) puts what the deposit doesn't cover on the account's terms: `GrandTotal × (100 − DepositPercent) / 100`, capped by what's left of the credit limit, in the store's currency only, never for a register sale. The tender reference is `"{accountId}:{sessionId}"`. The amount becomes a transaction due in `NetDays`, with source `account-terms`, owned by the buyer; `AccountTermsService.GetOwedAsync` sums what the account owes across its buyers.

`Order.AccountId` (indexed as `OrderIndex.AccountId`, queried with `OrderQuery.AccountId`) is the account an order was placed for.

## Quotes

Quotes are an Orders feature (see [Carts and Orders](carts-and-orders.md#quotes)); an account buyer's quote carries their purchase order number, and the order its account.

## Price lists

`PriceList`: `Name`, `PercentOff` (0 to 100, or none), `Currency` (the store's when saved) and `Entries` (`PriceListEntry`: `Sku`, `UnitPrice`), in the `PriceLists` YesSql collection, indexed by `PriceListIndex` (`Name`) through `IPriceListStore`.

Products.Core has the seam:

```csharp
public interface IBuyerPriceProvider
{
    ValueTask<decimal?> GetUnitPriceAsync(BuyerPriceContext context, CancellationToken cancellationToken = default);
}

public sealed record BuyerPriceContext(ContentItem ContentItem, string VariantId, string Sku, decimal UnitPrice, string Currency, int Quantity);
```

`DefaultPriceResolver` asks every provider for a one-time price after converting it to the requested currency, and charges the lowest one below the store's. Volume and role discounts don't come off a buyer's price, and recurring prices and custom amounts are left alone.

`B2BPriceProvider` answers for the signed-in buyer's account: the entry for the product's or variant's SKU (case-insensitive), converted by `IShopperCurrency` when the buyer shops in another currency (and skipped when it can't be), or else `PercentOff` off the price, rounded to the currency. The admin's **Prices by SKU** box reads a SKU and a price on each line, apart by a comma, semicolon or tab (a decimal comma after a semicolon or tab), at most 5,000 lines; a later line for a SKU replaces an earlier one. Deleting a price list clears it from every account on it.

## Approvals and budgets

`B2BApprovalService.GetReasonAsync(account, buyer, total, currency)` returns why an order needs approval, or `null`:

- an account admin never needs it;
- a buyer's order over their `OrderLimit`;
- an order that takes the account's paid orders this calendar month past `MonthlyBudget` (orders in other currencies converted with `IShopperCurrency`).

`Order.Approval` (Orders.Core `OrderApproval`) records it: `Status` (`Pending`, `Approved`, `Rejected`), `Reason`, `Amount` and `Currency` (the total asked about), `RequestedUtc`, `RequestedBy`, `DecidedUtc`, `DecidedBy`, `Note`. `Covers(total, currency)` is true for an approval of at least that total, so an order that grows needs approval again.

- **Asking.** The Business account step reports the reason on `approval` and doesn't save. With `requestApproval`, `RequestAsync` sets the approval pending, gives the order an `OrderInvoice` (so it outlives its checkout, which puts it back to `Draft` when it ends, and is paid from the buyer's orders), and emails the account's admins.
- **Blocking.** `B2BApprovalPaymentGate` (`ICheckoutPaymentGate`) refuses payment for an order that needs approval it doesn't have, whatever path the buyer took. `QuoteService.CanPay` is false for a pending or rejected approval, so no pay link is offered.
- **Deciding.** `DecideAsync(order, approve, adminName, note)` records it and emails the buyer; a rejected order is canceled with `OrderCancelReason.Rejected`.

## Punchout (OCI)

`PunchoutController` serves:

| Route | |
| --- | --- |
| `GET` or `POST punchout/oci/{accountId}` | Takes the key as `PASSWORD` or `key`, `HOOK_URL` and `~TARGET`. Refuses (401) an unknown account, one that doesn't buy as a business, a wrong key (compared by hash in fixed time), a `HOOK_URL` that isn't absolute `https` (or `http` on loopback) or carries user info, a punchout user who isn't one of its buyers, or one in the `Administrator` role. Otherwise signs the punchout buyer in, keeps `{ AccountId, HookUrl, Target }` in a data-protected `punchout` cookie, and opens the home page. |
| `GET punchout/oci/transfer` (`PunchoutTransfer`) | Renders the `PunchoutTransfer` shape: a form of OCI fields that posts itself to the `HOOK_URL` in the target frame (`_top`, `_parent`, `_self` or `_blank`). |

`PunchoutCartActionProvider` adds **Transfer to your procurement system** to the cart while the cookie is there. `PunchoutService.ToOciFields(pricing)` writes, for each line that can be bought, numbered from 1: `NEW_ITEM-DESCRIPTION[n]` (40 characters, the full title in `NEW_ITEM-LONGTEXT_n:132[]` when longer), `QUANTITY`, `UNIT` (`EA`), `PRICE` (one unit with its add-ons, invariant), `PRICEUNIT` (`1`), `CURRENCY`, `VENDORMAT` (SKU) and `EXT_PRODUCT_ID` (content item id). The cart stays; the order is placed in the procurement system.

The store's sign-in cookie is `SameSite=Lax`, so open the punchout in a top-level window, not a frame of the procurement system. A site with a content security policy has to allow the hook's origin in `form-action`.

## API

| Endpoint | |
| --- | --- |
| `GET account/business` | The buyer's account: role, order limit, terms, what it owes, locations. |
| `GET account/business/approvals` | For an admin: orders waiting for approval (`ApiB2BAccountApproval`). |
| `POST account/business/approvals/{orderId}` | For an admin: `{ approve, note }`; 204, or 409 when it isn't waiting. |
| `PUT checkout/{id}/business` | `{ purchaseOrderNumber, requestApproval }`; an order that needs approval answers 400 with the reason, or "sent for approval", on `approval`. |
| `POST`/`DELETE checkout/{id}/account` | Puts the rest on the account's terms, or takes it off. |

`ApiOrder` carries `accountId`, `purchaseOrderNumber`, `quote` and `approval`.

## Admin and permissions

| Screen | Permission | Default roles |
| --- | --- | --- |
| The **Business buying**, **Buyers**, **Payment terms**, **Locations** and **Punchout (OCI)** cards on the account editor, and the business buying cards on the account's page | `ManageB2BAccounts` (**Manage B2B accounts**) | Administrator |
| **Commerce > B2B Accounts**: Orchard Core's content list of the `Account` type with `b2b:true` | `ListContent` and `ManageB2BAccounts` | Administrator |
| **Commerce > Price lists** (`PriceListsAdminController`, admin route `b2b/price-lists`), on Orchard Core's `AdminList` | `ManageB2BAccounts` | Administrator |
| The storefront **Business account** page (`account/business`) | An account buyer; approvals for its admins | |

The content list's `b2b:` term narrows any content list to the accounts that buy as a business (`b2b:true`) or that have the part turned off (`b2b:false`).

## Recipes and deployment

Accounts are content items, so the `ContentDefinition` and `Content` steps carry them with their `B2BAccountPart`. Price lists have a step of their own:

```json
{
  "name": "PriceLists",
  "PriceLists": [
    {
      "ItemId": "4b7g3ke0q7mnvf3fbzbbyemqtn",
      "Name": "Wholesale",
      "PercentOff": 10,
      "Currency": "USD",
      "Entries": [ { "Sku": "HAT-22069", "UnitPrice": 24.5 } ]
    }
  ]
}
```

A list is matched by `ItemId`, then by name, so running the step again updates rather than duplicates. The **Price Lists** deployment step (`PriceListsDeploymentStep`, under Commerce) exports every list in this shape. With CrestApps Recipes on, both are described by a schema (`PriceListsRecipeStepSchema`, `PriceListsDeploymentStepSchema`).

## Report Builder data source

The **B2B accounts** data source (technical name `B2BAccounts`) lets the [Report Builder](report-builder/index.md) and Orchard Core data pipelines read the accounts that buy as a business, their buyers, and the price lists they buy on. It is registered when the Accounts B2B feature and **Data Pipelines Core** (`OrchardCore.DataPipelines.Core`) are both enabled. The Report Builder enables Data Pipelines Core. Every data set needs **Manage B2B accounts** (`ManageB2BAccounts`). A person without it does not see the data sets, and a report that reads them gets no rows.

| Data set | Display name | Rows |
| --- | --- | --- |
| `B2BAccounts` | B2B accounts | One per published account with the B2B part. The `AccountId` of the Orders data set refers to it. |
| `B2BBuyers` | B2B buyers | One per person who buys for an account. |
| `PriceLists` | Price lists | One per price list. |
| `PriceListEntries` | Price list entries | One per price a price list sets for a SKU. |

Accounts are read from their published version through `B2BAccountIndex`, and price lists newest first by their index rows; the data sets have no main date. A join on **Account ID** is applied in the database; other filters, and grouping, are applied after the read. The credit limit, the monthly budget and a buyer's order limit are in the store's currency, and the **Currency** field next to them names it. The punchout key is never exposed.

### B2B accounts

| Field | Display name | Notes |
| --- | --- | --- |
| `ItemId` | Account ID | The account's content item id. |
| `Name` | Name | The account's title. |
| `TaxId` | Tax or VAT number | |
| `BillingEmail` | Billing email | |
| `IsEnabled` | On | The part is on and the account is published. |
| `BuyerCount` | Buyers | |
| `AdminCount` | Admins | The buyers with the Admin role. |
| `LocationCount` | Locations | |
| `PriceListId` | Price list ID | Refers to `PriceLists`. Empty when the account pays the store's prices. |
| `PunchoutEnabled` | Shops by punchout | |
| `OffersTerms` | Buys on terms | |
| `NetDays` | Net days | |
| `DepositPercent` | Deposit (%) | |
| `RequirePurchaseOrder` | Purchase order required | |
| `Currency` | Currency | The store's currency. |
| `CreditLimit` | Credit limit | In the store's currency. Empty for no limit. |
| `MonthlyBudget` | Monthly budget | In the store's currency. Empty for no budget. |
| `CreatedUtc` | Added | When the account was created. |
| `ModifiedUtc` | Last changed | When the account was last saved. |

### B2B buyers

Each account gives its buyers in their order, buyers without a user included. The buyers are read with their account, so a join on **Account ID** is applied in the database. A join on **User ID** is applied after the read.

| Field | Display name | Notes |
| --- | --- | --- |
| `AccountId` | Account ID | Refers to `B2BAccounts`. |
| `AccountName` | Business account | |
| `B2BAccountEnabled` | Orders on account | |
| `Position` | Buyer number | The buyer's place in the account, from 1. |
| `UserId` | User ID | Refers to the Users data source. |
| `UserName` | User name | |
| `Email` | Email | |
| `Role` | Role | `Buyer` or `Admin`. |
| `Currency` | Currency | The store's currency. |
| `OrderLimit` | Order limit | The most an order the buyer places may come to before an account admin approves it, in the store's currency. Empty for no limit. |

### Price lists and price list entries

All prices by SKU are in the price list's currency, which the **Currency** field names. A join on the price list and a filter on its name are applied in the database.

| Field | Display name | Data set | Notes |
| --- | --- | --- | --- |
| `ItemId` | Price list ID | Price lists | Identifies the price list. |
| `PriceListId` | Price list ID | Price list entries | Refers to `PriceLists`. |
| `Name` | Name | Price lists | |
| `PriceListName` | Price list | Price list entries | |
| `PercentOff` | Percent off | Price lists | The percentage off every product the list does not price by SKU. Empty when there is none. |
| `Currency` | Currency | Both | The currency of the list's prices by SKU. |
| `EntryCount` | Prices by SKU | Price lists | |
| `CreatedUtc` | Added | Price lists | |
| `ModifiedUtc` | Last changed | Price lists | |
| `Position` | Price number | Price list entries | The price's place in the list, from 1. |
| `Sku` | SKU | Price list entries | |
| `UnitPrice` | Unit price | Price list entries | In the price list's currency. |

## Related modules

- [Accounts](accounts.md): the account type, its picker, search and page.
- [Carts and Orders](carts-and-orders.md): quotes, draft orders and the order's purchase order number.
- [Checkout](checkout.md): steps, tenders and payment gates.
- [Products](products.md#resolving-a-price): `IPriceResolver`.
- [Transactions](transactions.md): what the account owes on its terms.
