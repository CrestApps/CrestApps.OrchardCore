---
sidebar_label: Subscriptions
title: Subscriptions
description: Sell recurring plans, see and manage every subscriber's subscription, and let subscribers see and cancel their own plans from My Plans.
technical_manual:
  - modules/subscriptions
  - modules/checkout
  - modules/payments
---

**Subscriptions** let you sell recurring plans: a membership billed every month, a service billed every year, or anything else paid on a regular cycle. A plan is an ordinary content item that people buy through the site's [checkout](checkout.md). Every purchase creates a **subscription** that records what the subscriber is entitled to, how much they pay, when they renew, and what happened to it over time.

This page covers three screens: setting up the plans you sell, the administrator's list of subscriptions, and the subscriber's own **My Plans** page.

## Set up a plan

| | |
| --- | --- |
| **Menu** | Content > Content Definition > Content Types (to define a plan type), and Content > Content Items (to create plans) |
| **Permission** | The content permissions for your plan type, such as Edit content |
| **Feature** | Subscriptions |

<AskYourAdmin />

### Create a plan type

A plan type is a content type whose items are plans. You create it once, then create as many plans of that type as you sell.

1. Open **Content > Content Definition > Content Types** and click **Create new type**.
2. Enter a **Display Name** such as *Membership Plan* and click **Create**.
3. In the type's settings, set **Stereotype** to *Subscription* and click **Save**. The **Subscription** part is added to the type for you, with the billing fields described below.
4. Click **Add Parts** and add **Title** (so each plan has a name). The **Product** part, which gives each plan its price, is added with the stereotype. To offer the same plan at more than one price, add **Product prices** as well; see [Offer more than one price](#offer-more-than-one-price).
5. Optionally add **Subscription Entitlements** to give subscribers roles while they are subscribed; see [Give subscribers access](#give-subscribers-access).
6. Click **Save**.

### Create a plan

1. Open **Content > Content Items** and create a new item of your plan type, for example *Membership Plan*.
2. Enter the **Title**, the **Price** on the **Product** part, and the billing terms on the **Subscription** part.
3. Click **Publish**. To show your plans to visitors, list them on a page of your site; see [Show your plans on your site](#show-your-plans-on-your-site).

The **Subscription** part has these fields:

| Field | What it does |
| --- | --- |
| **Billing duration** | How often the subscriber is billed: a number and a unit (**Year**, **Month**, **Week** or **Day**). *1 Month* bills monthly; *3 Month* bills every three months. It cannot be less than one. |
| **Initial amount** | An optional one-time charge at sign-up, such as a setup fee. |
| **Initial amount description** | How that charge is named on the invoice, for example *Setup fee*. Required when you enter an **Initial amount**. |
| **Billing cycle limit** | Optionally stop the subscription after this many billing cycles. Leave it empty to bill until the subscription is cancelled. |
| **Free trial days** | Give the subscriber this many days free before the first cycle is billed. The payment method is still collected up front, so the trial converts without asking them to buy again. Leave it empty for no trial. |
| **Subscription day delay** | Delay the first recurring payment by this many days after sign-up. Leave it empty or set it to 0 to start billing immediately. |

The **Product** part has the **Price**, its **Currency** (empty uses the default currency), an optional **SKU**, and **Featured** and **Position**. **Position** is where the plan appears among your plans when a listing sorts them by **Recommended**: lower numbers come first, and plans without a position come after them. See [Products and currencies](products-and-currencies.md).

:::tip[Free trial or delayed start?]
Both collect the subscriber's payment method today and start billing later. A plan in a free trial shows the status **Trial**, and the subscriber has full access during it. A trial does not use up a cycle: a plan limited to three cycles still bills three.
:::

### Offer more than one price

Add the **Product prices** part to the plan type to sell the same plan several ways, for example monthly and yearly, per seat, or for an amount the buyer chooses. Each row under **Prices** is one way to buy, with its own **Name**, **Amount**, whether it is **Charged** once or **Recurring**, how often it renews (**Every** and **Interval**), its **Cycles**, **Trial days**, **Start after**, **Setup fee**, and whether it is **Offered**. Click **Add a price** for another row.

On the plan's card on your site, the buyer picks one under **Choose your plan** before clicking **Sign up**, and is charged the terms of the price they picked. See [Products and currencies](products-and-currencies.md) for every field.

### Show your plans on your site

Plans are products, so you show them to visitors with a [product listing](product-listings.md), like any other product. Each plan shows its price with its billing period, such as *$10 / month*, and its **Sign up** button.

1. Make sure the **Product Listing** feature is on.
2. Open **Commerce > Product Listings**, click **New**, and give the page a **Title** such as *Plans* and a **Permalink** such as *plans*.
3. Under **Products**, choose **Only these product types** and tick your plan type.
4. Plans rarely need filters: untick **Show filters** and **Let visitors change the sort**, and set **Default sort** to **Recommended** to show the plans in the order of their **Position**.
5. Click **Publish**, and add the page to your site's menu.

To give each plan a page of its own with the same details, edit the **Product** part of the plan type, tick **Show product details on the product page**, and add the **Autoroute** part for a readable address. See [Lay out the product pages](product-listings.md#lay-out-the-product-pages).

The **Commerce starter** recipe sets all of this up, with a **Plans** page at */plans*.

### Give subscribers access

<YouTube video="subscriptions-lifecycle" title="Plans and the roles they give" start={88} end={110} />

Add the **Subscription Entitlements** part to a plan type, then on each plan tick the roles under **Member access**. A subscriber holds these roles for as long as their subscription is current. They keep them through the period they have paid for, even after cancelling, and lose them once it lapses. Because roles control what people can see and do across the site, this is how you make content or features members-only.

What a plan grants is copied onto each subscription when it is bought, so changing a plan later does not change what existing subscribers were sold.

### Subscription settings

The site-wide settings are under **Settings > Subscriptions**, with the **Manage subscriptions settings** permission.

| Field | What it does |
| --- | --- |
| **Allow guest sign-up** | By default, people must sign in or register to subscribe. Tick this to let them subscribe without an account. |
| **Currency** | The default currency of your plans. |
| **Default roles to assign for new subscribers** | Roles every new subscriber gets, whatever plan they buy. |

This video shows a visitor signing up to a plan, then the Agreements screen:

<YouTube video="commerce" title="Narrated video of a visitor signing up to a plan, and the Agreements screen" start={160} end={232} />

## Manage subscriptions

<YouTube video="subscriptions-lifecycle" title="Managing a subscription" start={110} end={154} />

| | |
| --- | --- |
| **Menu** | Subscriptions > Agreements |
| **Permission** | Manage subscriptions |
| **Feature** | Subscriptions |

<AskYourAdmin />

The **Agreements** page lists one row per subscription bought on the site, whoever bought it.

### Find a subscription

- Type in **Search by title** and press Enter to find subscriptions by plan name.
- Use the status filter in the list header to show one status, or **Any status** for all.
- Each row shows the status and these badges: **Method** (the payment method, for example *Pay Later*), **Per cycle** (the amount billed each cycle), **Renews** (the next billing date, or **Never**), and **Paid through**.
- Use the pager at the bottom, and **Items per page** to show more or fewer rows.

Click a title or **Manage** to open a subscription.

### What a subscription shows

| Item | What it shows |
| --- | --- |
| **Status** | Where the subscription stands (see [Subscription statuses](#subscription-statuses)). |
| **Billed** | The amount and how often it is billed, for example *USD 20.00 every month*. |
| **Cycles billed** | How many cycles have been billed, and the limit when the plan has one. |
| **Current period** | The period the subscriber has paid for. |
| **Renews** | When the next cycle is billed, or **Never**. |
| **Grace ends** | Shown when a renewal failed: the last day the subscriber keeps access while the payment is retried. |
| **Payment method** | The payment method that bills the subscription. |
| **Gateway reference** | The payment provider's own reference for the subscription, when it has one. Use it to find the subscription in the provider's dashboard. |
| **Owner** | The subscriber's name, or the email address of a guest. |
| **What is billed** | Each line of the subscription with its quantity and price. |
| **History** | Everything that happened to the subscription, newest first, such as **Started**, **Renewed**, **Payment failed**, **Billing paused**, **Billing resumed** and **Canceled**. |

### Cancel a subscription

1. Open the subscription.
2. In **Actions**, optionally enter a **Reason**.
3. Leave **Keep access until the paid period ends** ticked to end the subscription when the period the subscriber already paid for ends, or untick it to end it now.
4. Click **Cancel subscription**, then click **Cancel subscription** again in the confirmation box. Click **Keep it** to back out.

The message says either *The subscription will end when the paid period does.* or *The subscription was canceled.* Future billing stops, including at the payment provider, so the subscriber's card is not charged again.

What the subscriber keeps:

- With **Keep access until the paid period ends**, the subscriber keeps their access, including any roles the plan gives, until the end of the period they paid for.
- Without it, access ends now.
- Payments already taken are not refunded automatically. To refund one, use **Commerce > Payments** (see [Payments and refunds](payments-and-refunds.md)).

### Pause and resume billing

- To suspend billing without ending the subscription, enter an optional **Reason**, click **Pause billing**, and confirm with **Pause billing**. It is offered on **Active** and **Trial** subscriptions. The status becomes **Paused**.
- To restart billing on a **Paused** or **Past due** subscription, click **Resume** and confirm with **Resume**. The status becomes **Active** again.

Each action is written to the subscription's **History**.

## Subscription statuses

<YouTube video="subscriptions-lifecycle" title="The subscription statuses" start={51} end={88} />

| Status | What it means |
| --- | --- |
| **Active** | Paid and current. |
| **Trial** | In its free trial. The subscriber has full access; billing starts when the trial ends. |
| **Past due** | A renewal payment failed. The subscriber keeps access until **Grace ends** while the payment is retried. |
| **Paused** | Billing was suspended by an administrator. |
| **Canceled** | Cancelled by the subscriber, an administrator or the payment provider. When cancelled at the end of the period, access lasts until the paid period ends. |
| **Expired** | Ended because the grace period ran out after a failed payment, or because the plan billed all of its cycles. |
| **Incomplete** | The subscription was created, but its first payment never went through, so nothing is owed to the subscriber yet. |

## My Plans (for subscribers)

<YouTube video="subscriptions-lifecycle" title="My Plans for subscribers" start={154} end={174} />

| | |
| --- | --- |
| **Menu** | Subscriptions > My Plans |
| **Permission** | Manage own subscriptions (given to every signed-in user by default) |
| **Feature** | Subscriptions |

<AskYourAdmin />


**My Plans** lists every plan the subscriber subscribes to, and lets them cancel one themselves.

### See your plans

Each row shows the plan, its status, and these badges:

| Badge | What it shows |
| --- | --- |
| **Per cycle** | What you pay each billing cycle. |
| **Renews** | The date of your next payment, or **Does not renew** when nothing more will be charged. |
| **Access until** | The last day of the period you have paid for, or **Ended** when your access is over. |

Use the status filter in the list header to show one status, or **Any status** for all.

### Cancel your plan

1. Click **Cancel** on the plan. It is offered on plans that are **Active**, in a **Trial**, or **Past due**.
2. The confirmation says that the next payment will not be taken and until when you keep access. Click **Cancel the subscription**, or **Keep it** to change your mind.

The page then says *Your subscription will not renew. You keep access until the end of the period you have paid for.*

At the bottom of the page, the note *Cancelling stops the next payment. You keep access until the end of the period you have already paid for.* explains what cancelling does. It is shown only when at least one of your plans can still be cancelled. Cancelling from **My Plans** never ends your access early and never refunds the current period: you bought that time, so you keep it.

## Tips and troubleshooting

- **A subscriber lost access after a failed payment.** Access lasts through the grace period after a failed renewal. Once it runs out, the subscription is **Expired** and the plan's roles are removed. The subscriber can subscribe again.
- **A subscriber still has access after cancelling.** That is expected when the subscription was cancelled at the end of the period: access lasts until **Paid through**.
- **Visitors can't find the plans.** Plans are shown only on a product listing that lists their type; see [Show your plans on your site](#show-your-plans-on-your-site).
- **The plan has no Sign up button.** Make sure the plan is published and has a price on its **Product** or **Product prices** part.
- **Visitors see *This plan can't be bought right now* after Sign up.** The plan has no price the site can charge: usually it has no currency, and its content type sets no default currency, or its price is charged once rather than every period. Nothing is charged and no account is created. Give the plan a currency (or set a **Default currency** on the **Product** part of its content type) and a price that recurs, then publish it again.
- **Visitors are asked to register before they can subscribe.** Tick **Allow guest sign-up** in **Settings > Subscriptions** to let them subscribe without an account.
- **You want to sell a whole website.** See [Selling Sites](sites.md).
- **You want to split one price into a few payments.** Use an [installment plan](installment-plans.md) instead.
