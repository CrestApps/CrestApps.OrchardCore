---
sidebar_label: Marketplace
title: Marketplace
description: "Sell other sellers' products: add vendors, choose each product's vendor, keep a commission on every sale, hold what vendors earn until it's safe to pay, and pay them out to their own Stripe accounts."
technical_manual:
  - modules/marketplace
---

Sell products for other sellers, called vendors. Your store takes the payment, keeps a commission, and pays each vendor the rest once the sale is safe from returns.

| | |
| --- | --- |
| **Menu** | Commerce > Vendors; Settings > Commerce > Marketplace |
| **Permission** | Manage the marketplace |
| **Feature** | Marketplace, with Marketplace payouts with Stripe Connect to pay vendors |

<AskYourAdmin />

To try it, run the **Marketplace sample** recipe under **Configuration > Recipes**. It adds a vendor and a Handmade Ceramic Mug sold for it.

## Set your commission and hold

Under **Settings > Commerce > Marketplace**:

- **Commission (%)**: what the store keeps of each vendor's sale, after discounts. A vendor can have its own.
- **Days held**: how long a sale is held after it's paid, such as for returns, before the vendor can be paid for it.
- **Hold a sale of goods until its order is fulfilled**: goods wait for shipping as well as the days held. Services and downloads wait for the days only.
- **Pay vendors out by themselves**: what's released is paid within the hour. Untick it to pay out from each vendor's page.

## Add a vendor

1. Under **Commerce > Vendors**, choose **Add vendor**.
2. Enter its **Name** and **Email**, and under **People who run it**, the usernames of the people who sell through it.
3. Leave **Commission (%)** empty to use the store's, or enter the vendor's own.

On each of its products, choose it under **Vendor**. Add the **Vendor** part to the product's content type first, if it isn't there.

The people who run a vendor see its sales, what it has earned and its payouts under **Vendor** in their account.

## Pay vendors

Vendors are paid to their own Stripe accounts. Each vendor sets theirs up once, from **Set up payouts** under **Vendor** in their account, through Stripe's own pages.

:::note[Stripe Connect]
Your Stripe account needs **Connect** turned on, in your Stripe dashboard, before vendors can set up payouts.
:::

A sale is ready to pay once its hold ends and, for goods, its order is fulfilled. To pay one sooner, such as a service already delivered, open the vendor and choose **Release now** next to its order. **Pay out what's ready** pays the vendor everything that's ready.

When you refund an order, the vendor's share of the refund comes off what they're owed.

To stop paying a vendor, set it to **Suspended: nothing is paid out**.
