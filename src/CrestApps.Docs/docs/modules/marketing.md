---
sidebar_label: Marketing
title: Marketing
description: "Marketing: customers' purchases kept per customer, segments of customers by orders, spend, recency and country, email campaigns in Liquid to a segment's customers who accept marketing, sent in batches, with a protected unsubscribe link."
user_manual: user-manual/commerce/marketing
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Marketing` (Marketing) |
| **Category** | Commerce |
| **Depends on** | Commerce, Customers, Orders |
| **Seams it fills** | `IOrderEventHandler` (Orders) |

The **Marketing** feature emails the store's customers who accept marketing (`Customer.AcceptsMarketing`). It keeps what each customer has bought, groups customers into segments by it, and sends campaigns to a segment. Admin screens are described in the [User Manual](../user-manual/commerce/marketing.md).

## Customers' purchases

`CustomerPurchases` (collection `Marketing`, its id the customer's) holds `OrderCount`, `TotalSpent` (paid orders less refunds, in the store's currency, converted with `IShopperCurrency`), `FirstOrderUtc`, `LastOrderUtc` and `Country` (the last order's billing or shipping country). `CustomerPurchasesIndex` maps them, with the document id.

- `CustomerPurchasesOrderHandler` calls `CustomerPurchasesService.RecordAsync` when an order is paid or refunded. `OrderIds` and `RefundIds` make it idempotent: an order and a refund count once.
- `MarketingBackgroundTask` (every minute) builds purchases from the orders paid before the feature was on, 200 a run, newest first (`PurchasesBackfill` keeps how far it got).

## Segments

`Segment` (`SegmentIndex`) has a name and `SegmentRules`: `MinOrders`, `MaxOrders`, `MinSpent`, `MaxSpent`, `LastOrderAtLeastDaysAgo` (lapsed buyers), `LastOrderWithinDays`, `FirstOrderWithinDays` and `Country`. Every rule given has to hold, on customers with at least one order. `ICustomerPurchasesStore.ListAsync(rules, now, afterId, take)` pages a segment by document id, so customers counted while a campaign sends come after the cursor.

## Campaigns

`Campaign` (`CampaignIndex`): `Name`, `SegmentId`, `Subject` and `Body` with Liquid, `Status` (`Draft`, `Scheduled`, `Sending`, `Sent`, `Canceled`), `ScheduledUtc`, the `Cursor`, and `Sent`, `Skipped` and `Failed` counts.

The Liquid has:

| Value | |
| --- | --- |
| `Customer` | `FirstName`, `LastName`, `Name`, `Email`, `OrderCount`, `TotalSpent`, `LastOrderUtc` (`CampaignCustomer`, registered with Fluid). |
| `StoreName` | The site's name. |
| `UnsubscribeUrl` | The customer's own unsubscribe link. One is added at the end when the body has none. |

`CampaignService.SendBatchAsync` sends 50 customers at a time, at most ten batches a run of `MarketingBackgroundTask`, from the cursor. It skips a customer who isn't `Active`, doesn't accept marketing, or has no email. A scheduled campaign starts when its time comes. Sending needs an `IEmailService` and the site's `BaseUrl`. The text part is the HTML without its tags.

## Unsubscribing

`UnsubscribeController` serves `GET` and `POST marketing/unsubscribe?token=` (`MarketingUnsubscribe`, the `MarketingUnsubscribe` shape). The token is the customer id, protected by a time-limited data protector (400 days). A `POST` turns `AcceptsMarketing` off; it needs no form token, so a mail app's one-click unsubscribe works.

## Admin and permissions

| Screen | Permission | Default roles |
| --- | --- | --- |
| **Commerce > Segments** (`SegmentsAdminController`, `marketing/segments`) and **Commerce > Campaigns** (`CampaignsAdminController`, `marketing/campaigns`), on Orchard Core's `AdminList` | `ManageMarketing` (**Manage marketing**) | Administrator |

A segment's editor counts its customers and those who accept marketing (up to 10,000). A campaign's editor sends a test, as the segment's first customer would get it.

## Related modules

- [Customers](customers.md): `AcceptsMarketing`.
- [Reports](reports.md#report-subscriptions): scheduled report emails.
- [Product Reviews](reviews.md#review-requests): review requests to buyers who accept marketing.
