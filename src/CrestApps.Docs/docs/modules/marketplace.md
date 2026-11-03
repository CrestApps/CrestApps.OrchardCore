---
sidebar_label: Marketplace
title: Marketplace
description: "Marketplace: vendors whose products the store sells, the commission it keeps on each sale, earnings held in escrow until a hold ends and the order is fulfilled, refunds taken back, and payouts to vendors' Stripe Connect accounts."
user_manual: user-manual/commerce/marketplace
---

| | |
| --- | --- |
| **Features** | `CrestApps.OrchardCore.Marketplace` (Marketplace), `CrestApps.OrchardCore.Marketplace.StripeConnect` (Marketplace payouts with Stripe Connect) |
| **Category** | Commerce |
| **Depends on** | Commerce, Orders; Stripe for the payouts feature |
| **Seams it fills** | `IOrderEventHandler` (Orders) |
| **Seams it adds** | `IVendorPayoutProvider` |

<YouTube video="marketplace-classifieds" title="Narrated video overview of the Marketplace and Classifieds modules" />

The **Marketplace** feature sells other sellers' products. A vendor is a seller with the people who run it. The attachable **Vendor** part (`VendorPart.VendorId`) says which vendor a product is sold for. Each paid sale of a vendor's product earns the vendor its price after discounts, less the store's commission. What it earns is held until it's released, and then paid out. Admin screens are described in the [User Manual](../user-manual/commerce/marketplace.md). The **Marketplace sample** recipe adds a Vendor Product type, a vendor, and a mug sold for it.

## Records

Vendors, earnings and payouts live in the `Marketplace` collection:

- **`Vendor`** (`IVendorStore`): its name and email, the users who run it (`UserIds` and `UserNames`, indexed one row each by `VendorUserIndex`), its own `CommissionPercent` (`null` uses the store's), its `VendorStatus` (Active or Suspended), its country, and its Stripe account (`StripeAccountId`, `PayoutsEnabled`).
- **`VendorEarning`** (`IVendorEarningStore`): one per vendor line of a paid order, or per refund. It holds `Gross`, `Commission` and `Net`, its kind (Sale or Refund), its `VendorEarningStatus` (Held, Available or PaidOut), `HoldUntilUtc`, `AwaitsFulfillment` for goods, and the payout that paid it.
- **`VendorPayout`** (`IVendorPayoutStore`): an amount sent to a vendor for one order's earnings, with its status and the provider's reference.

`MarketplaceSettings` (**Settings > Commerce > Marketplace**, `ManageMarketplace`) holds the commission (10%), the days a sale is held (7), whether goods wait for fulfillment, whether payouts run by themselves, and the country a vendor's Stripe account is made in.

## The ledger

`MarketplaceLedger` does the arithmetic:

- **`GetLineGross`** is a line's total less its share of the order's discounts.
- **`Split`** rounds the commission to the cent and gives the vendor the rest.
- **`GetRefundShare`** is a vendor's share of a refund: the refund's lines when it names them, else its share of the order's total.

`MarketplaceService` keeps the books:

- **`RecordSaleAsync`** writes a held earning for each vendor line of a paid order. Recording an order twice writes nothing more.
- **`RecordRefundAsync`** writes a negative earning for the vendor's share, keyed by the refund, so it isn't taken twice.
- **`ReleaseDueAsync`** makes earnings available once their hold has ended and, for goods, their order is fulfilled. **`ReleaseOrderAsync`** releases an order's earnings early, such as when a service is delivered or a milestone is reached.
- **`PayOutAsync`** pays a vendor what's available, one payout per order, through the `IVendorPayoutProvider`. It refuses a suspended vendor or one whose payouts aren't set up. **`PayOutAllAsync`** pays every vendor.

`MarketplaceOrderEventHandler` records sales when an order is paid and refunds when it's refunded. `MarketplaceBackgroundTask` releases what's due every hour, and pays out when automatic payouts are on.

## Stripe Connect

The **Marketplace payouts with Stripe Connect** feature fills the payout seam:

- `StripeConnectVendorService` makes an Express account for a vendor and an onboarding link. The vendor's page refreshes whether its details are submitted and transfers are active.
- `StripeConnectPayoutProvider` creates a Transfer to the vendor's account, with the order's charge as its `SourceTransaction`, so the money moves when the charge's funds are available.

The store's Stripe account must have Connect turned on in the Stripe dashboard. Until it does, setting up a vendor's payouts says so and nothing is created.

## Pages

**Commerce > Vendors** (`ManageMarketplace`) lists vendors on the admin list. A vendor's page shows what it has earned (held, ready and paid out), its sales and payouts, **Release now** for an order, and **Pay out what's ready**. The storefront account's **Vendor** page (`account/vendor`, the `VendorDashboard` shape) shows the same to the people who run the vendor, with **Set up payouts** for Stripe.

## Report Builder data source

With **Data Pipelines Core** (`OrchardCore.DataPipelines.Core`) on, which the Report Builder needs, the Marketplace feature adds the **Marketplace** data source (`Marketplace`) to the Report Builder and to Orchard Core data pipelines. Its data sets need **Manage the marketplace** (`ManageMarketplace`), the permission of **Commerce > Vendors**. The Stripe Connect feature adds no data set. A vendor's Stripe account, the key used to make it, the provider's reference for a transfer and its error messages are never exposed.

The **Vendors** data set (`Vendors`) has one row per vendor, newest first by when it was added; it has no default date. Joins on the vendor ID and status filters are applied in the database, and simple groups and totals of the vendor ID and status are counted there too.

| Field | Display name | Notes |
| --- | --- | --- |
| `ItemId` | Vendor ID | Identifies the vendor. |
| `Name` | Vendor | |
| `Email` | Email | |
| `Status` | Status | `Active` or `Suspended`. |
| `CommissionPercent` | Commission (%) | Empty when the vendor has the store's default. |
| `UserCount` | People | How many users run the vendor. |
| `UserNames` | Run by | Their usernames, separated by commas, as they were when added. |
| `CreatedUtc` | Added | |
| `ModifiedUtc` | Last changed | |
| `Country` | Country | The country its payout account is made in. |
| `HasPayoutAccount` | Has a payout account | |
| `PayoutsEnabled` | Payouts enabled | Whether its payout account is set up and can take transfers. |

The **Vendor earnings** data set (`VendorEarnings`) has one row per sale or refund of a vendor, newest first by when it was recorded, which is its default date. Date ranges on that date, joins on the earning, vendor and order IDs, and status filters are applied in the database, and simple groups and totals of those fields and of the hold date are counted there too. Money is in the order's currency, which the `Currency` field names; a refund's amounts are negative.

| Field | Display name | Notes |
| --- | --- | --- |
| `ItemId` | Earning ID | Identifies the earning. |
| `VendorId` | Vendor ID | References `Marketplace` > `Vendors`. |
| `OrderId` | Order ID | References `Orders` > `Orders`. |
| `OrderNumber` | Order number | |
| `LineId` | Order line ID | References `Orders` > `OrderLines` > `LineId`. |
| `Title` | Item | |
| `Kind` | Kind | `Sale` or `Refund`. |
| `Status` | Status | `Held`, `Available` or `PaidOut`. |
| `AwaitsFulfillment` | Waits for fulfillment | |
| `PayoutId` | Payout ID | References `Marketplace` > `VendorPayouts`. |
| `Currency` | Currency | |
| `Gross` | Gross | What the buyer paid for the line after its share of the order's discounts. |
| `CommissionPercent` | Commission (%) | |
| `Commission` | Commission | |
| `Net` | Net | What the vendor gets: the gross less the commission. |
| `CreatedUtc` | Recorded | The default date. |
| `HoldUntilUtc` | Held until | |
| `ReleasedUtc` | Released | |

The **Vendor payouts** data set (`VendorPayouts`) has one row per payout, newest first by when it was made, which is its default date. Date ranges on that date and joins on the payout and vendor IDs are applied in the database, and simple groups and totals of those fields are counted there too.

| Field | Display name | Notes |
| --- | --- | --- |
| `ItemId` | Payout ID | Identifies the payout. |
| `VendorId` | Vendor ID | References `Marketplace` > `Vendors`. |
| `OrderId` | Order ID | References `Orders` > `Orders`. |
| `OrderNumber` | Order number | |
| `Status` | Status | `Paid` or `Failed`. |
| `Currency` | Currency | |
| `Amount` | Amount | In the payout's currency. |
| `EarningCount` | Earnings paid | |
| `CreatedUtc` | Made | The default date. |
