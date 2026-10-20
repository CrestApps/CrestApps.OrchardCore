---
sidebar_label: Companies
title: Companies
description: "Companies: business customers with buyers, roles, locations and payment terms (a deposit at checkout, the rest invoiced in net days, under a credit limit), purchase order numbers, quotes, price lists through the IBuyerPriceProvider seam, approvals and monthly budgets, and OCI punchout from a procurement system."
user_manual: user-manual/commerce/companies
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Companies` (Companies) |
| **Category** | Commerce |
| **Depends on** | Commerce, Orders (`CrestApps.OrchardCore.Orders`) |
| **Seams it fills** | `ICheckoutTenderProvider`, `ICheckoutHandler`, `ICheckoutPaymentGate` (Checkout), `IOrderEventHandler` (Orders), `IBuyerPriceProvider` (Products), `ICartActionProvider` (Carts) |

The **Companies** feature sells to businesses. A company has buyers, some of them admins, the places it ships to, the terms it pays on, a price list, limits and a monthly budget that send orders for approval, and a punchout key for its procurement system. Admin screens are described in the [User Manual](../user-manual/commerce/companies.md).

## Model

Companies and price lists live in the `Companies` YesSql collection.

| `Company` | |
| --- | --- |
| `Name`, `TaxId`, `BillingEmail` | The tax number prints on its invoices. |
| `Buyers` | `CompanyBuyer`: `UserId`, `UserName`, `Email`, `Role` (`Buyer` or `Admin`), `OrderLimit` (store currency, or none). A user buys for one company; `CompanyBuyerIndex` maps one row per buyer, so `ICompanyStore.FindByBuyerAsync(userId)` finds it. |
| `Locations` | `CompanyLocation`: `LocationId`, `Name`, `Address`, offered at checkout. |
| `Terms` | `CompanyTerms`: `NetDays`, `DepositPercent`, `CreditLimit`, `RequirePurchaseOrder`. `OffersTerms` is true when `NetDays` is set. |
| `PriceListId` | The `PriceList` its buyers pay, or none. |
| `MonthlyBudget` | What its buyers may order in a calendar month, in the store's currency, or none. |
| `Punchout` | `CompanyPunchout`: `Enabled`, `UserId` (the buyer the procurement system shops as), `KeyHash` (SHA-256 of the key, hex), `KeyCreatedUtc`. |
| `IsEnabled` | An off company's buyers buy for themselves. |

`CompanyService.GetCurrentAsync()` returns the signed-in user's enabled company and buyer, cached for the request.

## Payment terms and the Company step

`CompanyCheckoutHandler` adds a `company` step (order 3, after contact and shipping) to a company buyer's checkout of an order. `CompanyCheckoutStepService.SaveAsync(session, purchaseOrderNumber, requestApproval)` checks the number (required when `RequirePurchaseOrder`, 64 characters at most), puts it and the `CompanyId` on the order, and saves the step. An order that already has a purchase order number, such as a quote asked for with one, starts with the step done.

`CompanyTermsTenderProvider` (`ICheckoutTenderProvider`, key `company-terms`) puts what the deposit doesn't cover on the company's account: `GrandTotal × (100 − DepositPercent) / 100`, capped by what's left of the credit limit, in the store's currency only, never for a register sale. The account part becomes a transaction due in `NetDays`; `CompanyTermsService.GetOwedAsync` sums what the company owes.

## Quotes

Quotes are an Orders feature (see [Carts and Orders](carts-and-orders.md#quotes)); a company buyer's quote carries their purchase order number, and the order its company.

## Price lists

`PriceList`: `Name`, `PercentOff` (0 to 100, or none), `Currency` (the store's when saved) and `Entries` (`PriceListEntry`: `Sku`, `UnitPrice`), indexed by `PriceListIndex` (`Name`) through `IPriceListStore`.

Products.Core has the seam:

```csharp
public interface IBuyerPriceProvider
{
    ValueTask<decimal?> GetUnitPriceAsync(BuyerPriceContext context, CancellationToken cancellationToken = default);
}

public sealed record BuyerPriceContext(ContentItem ContentItem, string VariantId, string Sku, decimal UnitPrice, string Currency, int Quantity);
```

`DefaultPriceResolver` asks every provider for a one-time price after converting it to the requested currency, and charges the lowest one below the store's. Volume and role discounts don't come off a buyer's price, and recurring prices and custom amounts are left alone.

`CompanyPriceProvider` answers for the signed-in buyer's company: the entry for the product's or variant's SKU (case-insensitive), converted by `IShopperCurrency` when the buyer shops in another currency (and skipped when it can't be), or else `PercentOff` off the price, rounded to the currency. The admin's **Prices by SKU** box reads a SKU and a price on each line, apart by a comma, semicolon or tab (a decimal comma after a semicolon or tab), at most 5,000 lines; a later line for a SKU replaces an earlier one.

## Approvals and budgets

`CompanyApprovalService.GetReasonAsync(company, buyer, total, currency)` returns why an order needs approval, or `null`:

- a company admin never needs it;
- a buyer's order over their `OrderLimit`;
- an order that takes the company's paid orders this calendar month past `MonthlyBudget` (orders in other currencies converted with `IShopperCurrency`).

`Order.Approval` (Orders.Core `OrderApproval`) records it: `Status` (`Pending`, `Approved`, `Rejected`), `Reason`, `Amount` and `Currency` (the total asked about), `RequestedUtc`, `RequestedBy`, `DecidedUtc`, `DecidedBy`, `Note`. `Covers(total, currency)` is true for an approval of at least that total, so an order that grows needs approval again.

- **Asking.** The Company step reports the reason on `approval` and doesn't save. With `requestApproval`, `RequestAsync` sets the approval pending, gives the order an `OrderInvoice` (so it outlives its checkout, which puts it back to `Draft` when it ends, and is paid from the buyer's orders), and emails the company's admins.
- **Blocking.** `CompanyApprovalPaymentGate` (`ICheckoutPaymentGate`) refuses payment for an order that needs approval it doesn't have, whatever path the buyer took. `QuoteService.CanPay` is false for a pending or rejected approval, so no pay link is offered.
- **Deciding.** `DecideAsync(order, approve, adminName, note)` records it and emails the buyer; a rejected order is canceled with `OrderCancelReason.Rejected`.

## Punchout (OCI)

`PunchoutController` serves:

| Route | |
| --- | --- |
| `GET` or `POST punchout/oci/{companyId}` | Takes the key as `PASSWORD` or `key`, `HOOK_URL` and `~TARGET`. Refuses (401) an unknown or off company, a wrong key (compared by hash in fixed time), a `HOOK_URL` that isn't absolute `https` (or `http` on loopback) or carries user info, a punchout user who isn't one of its buyers, or one in the `Administrator` role. Otherwise signs the punchout buyer in, keeps `{ CompanyId, HookUrl, Target }` in a data-protected `punchout` cookie, and opens the home page. |
| `GET punchout/oci/transfer` (`PunchoutTransfer`) | Renders the `PunchoutTransfer` shape: a form of OCI fields that posts itself to the `HOOK_URL` in the target frame (`_top`, `_parent`, `_self` or `_blank`). |

`PunchoutCartActionProvider` adds **Transfer to your procurement system** to the cart while the cookie is there. `PunchoutService.ToOciFields(pricing)` writes, for each line that can be bought, numbered from 1: `NEW_ITEM-DESCRIPTION[n]` (40 characters, the full title in `NEW_ITEM-LONGTEXT_n:132[]` when longer), `QUANTITY`, `UNIT` (`EA`), `PRICE` (one unit with its add-ons, invariant), `PRICEUNIT` (`1`), `CURRENCY`, `VENDORMAT` (SKU) and `EXT_PRODUCT_ID` (content item id). The cart stays; the order is placed in the procurement system.

The store's sign-in cookie is `SameSite=Lax`, so open the punchout in a top-level window, not a frame of the procurement system. A site with a content security policy has to allow the hook's origin in `form-action`.

## API

| Endpoint | |
| --- | --- |
| `GET account/company` | The buyer's company: role, order limit, terms, what it owes, locations. |
| `GET account/company/approvals` | For an admin: orders waiting for approval (`ApiCompanyApproval`). |
| `POST account/company/approvals/{orderId}` | For an admin: `{ approve, note }`; 204, or 409 when it isn't waiting. |
| `PUT checkout/{id}/company` | `{ purchaseOrderNumber, requestApproval }`; an order that needs approval answers 400 with the reason, or "sent for approval", on `approval`. |
| `POST`/`DELETE checkout/{id}/account` | Puts the rest on the company's account, or takes it off. |

`ApiOrder` carries `companyId`, `purchaseOrderNumber`, `quote` and `approval`.

## Admin and permissions

| Screen | Permission | Default roles |
| --- | --- | --- |
| **Commerce > Companies** (`CompaniesAdminController`, admin route `companies`) and **Commerce > Price lists** (`PriceListsAdminController`, `companies/price-lists`), both on Orchard Core's `AdminList` | `ManageCompanies` (**Manage companies**) | Administrator |
| The account's **Company** page (`account/company`) | A company buyer; approvals for its admins | |

## Related modules

- [Carts and Orders](carts-and-orders.md): quotes, draft orders and the order's purchase order number.
- [Checkout](checkout.md): steps, tenders and payment gates.
- [Products](products.md#resolving-a-price): `IPriceResolver`.
- [Transactions](transactions.md): what the company owes on account.
