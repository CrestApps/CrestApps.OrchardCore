---
sidebar_label: Marketing
title: Marketing
description: "Email your customers who said yes to news and offers: segments by what they've bought, such as lapsed buyers, campaigns written with their name in them, sent now or at a time, with an unsubscribe link in each, and ready-made automations."
technical_manual:
  - modules/marketing
---

Email the customers who said yes to your news and offers. Group them into **segments** by what they've bought, such as buyers who haven't ordered in 90 days, and send a **campaign** to a segment.

| | |
| --- | --- |
| **Menu** | Commerce > Segments, Commerce > Campaigns |
| **Permission** | Manage marketing |
| **Feature** | Marketing (it needs Customers and Orders) |

<AskYourAdmin />

This video shows why to market from your orders, the segments and campaigns, consent and unsubscribing, and the ready-made automations:

<YouTube video="marketing" title="Narrated video overview of Marketing" />

Campaigns only go to customers who ticked **Send me news and offers** in their account, or whom you marked as accepting marketing on **Commerce > Customers**. Emails about their orders aren't affected. Every campaign carries a link to unsubscribe in one click.

When you turn Marketing on, it reads the orders already paid to learn what each customer has bought. On a big store that takes a few minutes.

## Segments

<YouTube video="marketing" title="Video of segments" start={94} end={155} />

1. On **Commerce > Segments**, click **Add segment**.
2. Give it a **Name**, such as *Lapsed buyers*, and a **Description** that only your staff see.
3. Fill in the rules the customers have to meet. Leave a rule blank to ignore it.

| Rule | Such as |
| --- | --- |
| **Haven't ordered in (days)** | 90, for buyers who've lapsed. |
| **Ordered in the last (days)** | 30, for recent buyers. |
| **First ordered in the last (days)** | 30, for new customers. |
| **At least / At most (orders)** | 2 or more, for repeat buyers. |
| **Spent at least / at most** | 500, for your best customers. In your store's currency, less refunds. |
| **Country** | DE: where their last order went. |

Click **Save**. The segment shows how many customers are in it, and how many of them accept marketing and so get its campaigns.

## Campaigns

<YouTube video="marketing" title="Video of campaigns" start={155} end={231} />

1. On **Commerce > Campaigns**, click **Add campaign**.
2. Name it (only staff see the name), and choose its **Segment**.
3. Write the **Subject** and the **Email**, in HTML. Put in the customer's details with Liquid:

| Write | To show |
| --- | --- |
| `{{ Customer.FirstName }}` and `{{ Customer.Name }}` | Their first name, and their full name. |
| `{{ Customer.OrderCount }}` and `{{ Customer.TotalSpent }}` | How many orders they've placed, and what they've spent. |
| `{{ StoreName }}` | Your store's name. |
| `{{ UnsubscribeUrl }}` | Their unsubscribe link. If you leave it out, one is added at the end. |

4. To check it, enter your address under **Send a test to** and click **Send test**. It's filled in as the segment's first customer would get it.
5. Click **Send**. Fill in **Send at** first to send it later, in your site's time zone.

A campaign goes out in batches over the next minutes, to the segment's customers as they are when each batch goes. Its page shows how many it was sent to, how many it skipped (no email, or they don't accept marketing) and how many failed. **Cancel the campaign** stops it.

Campaigns need your site's email to be set up, and your site's base URL for the unsubscribe links.

## When a customer unsubscribes

<YouTube video="marketing" title="Video of consent and unsubscribing" start={231} end={262} />

The unsubscribe link opens a page that asks them to confirm. Once they do, they no longer accept marketing, and no campaign or review request goes to them. They can tick **Send me news and offers** again in their account.

## Ready-made automations

<YouTube video="marketing" title="Video of the ready-made automations" start={262} end={313} />

Under **Tools > Recipes**, three recipes add workflows you can change under **Workflows**:

| Recipe | What it does |
| --- | --- |
| **Automation: thank-you email** | Emails each buyer a short thank-you when their order is paid. It's on once added. |
| **Automation: canceled order alert** | Emails the store when an order is canceled, with why. It's added off: set the address it goes to, then turn it on. |
| **Automation: low stock alert** | Emails the store when a product runs low or sells out. It's added off, like the one above. |

To have a report emailed every week, such as your sales, see [Email a report on a schedule](../reports.md#email-a-report-on-a-schedule).

## Related pages

- [Customers](customers.md): who accepts marketing.
- [Reviews](reviews.md#ask-buyers-for-reviews): asking buyers for reviews.
