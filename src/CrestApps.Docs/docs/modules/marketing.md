---
sidebar_label: Marketing
title: Marketing
description: "Marketing: customers' purchases kept per customer, segments of customers by orders, spend, recency and country, email campaigns in Liquid to a segment's customers who accept marketing or to a mailing list's members, sent in batches, with a protected unsubscribe link."
user_manual: user-manual/commerce/marketing
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Marketing` (Marketing) |
| **Category** | Commerce |
| **Depends on** | Commerce, [E-mail Preferences](consent.md), Customers, Orders, Orchard Core Email, [Outreach](outreach.md) |
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

`Campaign` (`CampaignIndex`): `Name`, `AudienceKind` (`Segment`, also when empty, or `MailingList`), `SegmentId` or `MailingListId`, `Subject` and `Body` with Liquid, `Status` (`Draft`, `Scheduled`, `Sending`, `Sent`, `Canceled`), `ScheduledUtc`, the `Cursor`, and `Sent`, `Skipped` and `Failed` counts.

The Liquid has:

| Value | |
| --- | --- |
| `Customer` | `FirstName`, `LastName`, `Name`, `Email`, `OrderCount`, `TotalSpent`, `LastOrderUtc` (`CampaignCustomer`, registered with Fluid). For a mailing list's member, only the name the member gave and the address. |
| `Recipient` | `Name` and `Email`: the name and address the e-mail goes to, for both audiences (`CampaignRecipient`). |
| `StoreName` | The site's name. |
| `UnsubscribeUrl` | The unsubscribe link of the address the e-mail goes to, on the site's [e-mail preferences page](consent.md). One is added at the end when the body has none. |

Campaigns are sent by the [outreach engine](outreach.md). `MarketingBackgroundTask` starts a scheduled campaign when its time comes, and `CampaignService.StartSendingAsync` hands a sending campaign to the engine as an outreach run (`Campaign.OutreachRunId`): the `MarketingSegment` audience source pages the segment 50 customers at a time from the cursor and skips a customer who isn't `Active`, doesn't accept marketing, or has no email, and the engine asks the consent service (`Email`, as a `Customer`, for the `Marketing` purpose) before each e-mail, so an unsubscribe recorded mid-run, or a "stop all marketing" kept for the address, still stops it; the `MarketingCampaign` renderer renders the Liquid with the address's unsubscribe link; the `Email` sender sends it with a text part (the HTML without its tags). The run's counts and cursor are copied back onto the campaign, which becomes `Sent` when the run completes. A batch that keeps failing (for example because the site's `BaseUrl` was removed) fails the run, and the campaign is then `Canceled`. Canceling a campaign stops its run. A campaign that was half sent before the engine resumes at its cursor with its counts.

### Sending to a mailing list

While the [Mailing Lists](mailing-lists.md) feature is on, `MailingListsStartup` replaces the default `ICampaignMailingLists`, and the campaign editor's **Audience** card shows **Send to** (**The customers of a segment** or **The members of a mailing list**) and a **Mailing list** select with the enabled lists. A campaign to a list hands the engine a run with the `MailingList` audience source and the list in the run's `MailingListId` property: the list's subscribed members, as the list is when each batch goes out, are e-mailed, and the engine still asks the consent service for the `Marketing` purpose. Each e-mail's unsubscribe link has the scope `MailingList:{listId}` (`CampaignService.GetUnsubscribeScope`), so it unsubscribes the person from that list. A test e-mail of such a campaign is rendered as a member with the test address would get it. With the feature off, the editor offers segments only, and a campaign that already goes to a list says so. See [Mailing Lists: Marketing campaigns](mailing-lists.md#marketing-campaigns).

Marketing depends on Orchard Core's **Email** feature, so campaigns can always be sent once the site's e-mail is set up. Until a default e-mail provider is configured (`IEmailProviderResolver`), `CampaignService.CanSendAsync` is false and the campaign editor says so instead of queueing a campaign that sends nothing.

## Unsubscribing

A campaign e-mail's unsubscribe link opens the site's one [e-mail preferences page](consent.md) (`email-preferences?token=…`), created with `IUnsubscribeLinks` for the address the e-mail goes to and the `AllMarketing` scope (`CampaignService.CreateUnsubscribeUrlAsync`), or, for a campaign to a mailing list, the list's scope, which unsubscribes the list. The page's **Unsubscribe** button, or a mail app's one-click `POST`, records an `Email` opt-out with the `Marketing` purpose: the address's opt-outs keep it (also for an address no customer holds), Customers turns `AcceptsMarketing` off on every customer that holds the address, and Mailing Lists unsubscribes the address from every list. The link needs the site's base URL; without it, a campaign batch fails and is retried, and the run fails if the base URL is still missing. A test e-mail's link is for the test address, so following it never unsubscribes the sample customer.

The links campaigns carried before (`GET` and `POST marketing/unsubscribe?token=`, `UnsubscribeController`, the `MarketingUnsubscribe` shape) keep working until they expire (400 days after they were sent). Their token is the customer id; a `POST` records the customer's opt-out through the consent service with the `Marketing` purpose. It needs no form token, so a mail app's one-click unsubscribe works.

## Admin and permissions

| Screen | Permission | Default roles |
| --- | --- | --- |
| **Commerce** → **Segments** (`SegmentsAdminController`, `marketing/segments`) and **Commerce** → **Campaigns** (`CampaignsAdminController`, `marketing/campaigns`), on Orchard Core's `AdminList` | `ManageMarketing` (**Manage marketing**) | Administrator |

A segment's editor counts its customers and those who accept marketing (up to 10,000). A campaign's editor sends a test, as the segment's first customer would get it.

## Recipes and deployment

The **Marketing segments and campaigns** deployment step (Commerce category) writes a `MarketingSegments` step, then a `MarketingCampaigns` step, so each campaign's segment is imported first. Both are matched by `ItemId`, so a second run adds nothing, and they are checked the way their editors check them. A campaign to a mailing list carries `"AudienceKind": "MailingList"` and its `MailingListId`, and is imported only while the Mailing Lists feature is on and the list exists.

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
