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
| **Depends on** | Commerce, Customers, Orders, Orchard Core Email, [Outreach](outreach.md) |
| **Seams it fills** | `IOrderEventHandler` (Orders) |

<YouTube video="marketing" title="Narrated video overview of Marketing" />

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

Campaigns are sent by the [outreach engine](outreach.md). `MarketingBackgroundTask` starts a scheduled campaign when its time comes, and `CampaignService.StartSendingAsync` hands a sending campaign to the engine as an outreach run (`Campaign.OutreachRunId`): the `MarketingSegment` audience source pages the segment 50 customers at a time from the cursor and skips a customer who isn't `Active`, doesn't accept marketing, or has no email, and the engine asks the consent service (`Email`, as a `Customer`) before each e-mail, so an unsubscribe recorded mid-run still stops it; the `MarketingCampaign` renderer renders the Liquid with the customer's unsubscribe link; the `Email` sender sends it with a text part (the HTML without its tags). The run's counts and cursor are copied back onto the campaign, which becomes `Sent` when the run completes. A batch that keeps failing (for example because the site's `BaseUrl` was removed) fails the run, and the campaign is then `Canceled`. Canceling a campaign stops its run. A campaign that was half sent before the engine resumes at its cursor with its counts.

Marketing depends on Orchard Core's **Email** feature, so campaigns can always be sent once the site's e-mail is set up. Until a default e-mail provider is configured (`IEmailProviderResolver`), `CampaignService.CanSendAsync` is false and the campaign editor says so instead of queueing a campaign that sends nothing.

## Unsubscribing

`UnsubscribeController` serves `GET` and `POST marketing/unsubscribe?token=` (`MarketingUnsubscribe`, the `MarketingUnsubscribe` shape). The token is the customer id, protected by a time-limited data protector (400 days). A `POST` turns `AcceptsMarketing` off; it needs no form token, so a mail app's one-click unsubscribe works.

## Admin and permissions

| Screen | Permission | Default roles |
| --- | --- | --- |
| **Commerce > Segments** (`SegmentsAdminController`, `marketing/segments`) and **Commerce > Campaigns** (`CampaignsAdminController`, `marketing/campaigns`), on Orchard Core's `AdminList` | `ManageMarketing` (**Manage marketing**) | Administrator |

A segment's editor counts its customers and those who accept marketing (up to 10,000). A campaign's editor sends a test, as the segment's first customer would get it.

## Recipes and deployment

The **Marketing segments and campaigns** deployment step (Commerce category) writes a `MarketingSegments` step, then a `MarketingCampaigns` step, so each campaign's segment is imported first. Both are matched by `ItemId`, so a second run adds nothing, and they are checked the way their editors check them.

A recipe never sends email. A campaign is exported without its delivery (status, schedule, cursor and counts), a new campaign always imports as a draft, and a campaign that is scheduled or sending on the site is left as it is.

```json
{
  "steps": [
    {
      "name": "MarketingSegments",
      "Segments": [
        { "ItemId": "4sg00000000000000000000001", "Name": "Lapsed buyers", "Rules": { "MinOrders": 1, "LastOrderAtLeastDaysAgo": 180 } }
      ]
    },
    {
      "name": "MarketingCampaigns",
      "Campaigns": [
        { "ItemId": "4cp00000000000000000000001", "Name": "Win back", "SegmentId": "4sg00000000000000000000001", "Subject": "We miss you", "Body": "Come back for 10% off." }
      ]
    }
  ]
}
```

## Related modules

- [Customers](customers.md): `AcceptsMarketing`.
- [Reports](reports.md#report-subscriptions): scheduled report emails.
- [Product Reviews](reviews.md#review-requests): review requests to buyers who accept marketing.
