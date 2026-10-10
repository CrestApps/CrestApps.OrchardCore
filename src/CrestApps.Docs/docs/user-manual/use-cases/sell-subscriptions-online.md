---
sidebar_label: Sell Subscriptions Online
title: Sell Subscriptions Online
description: Sell plans that renew every month or year through an online checkout, with card, wallet or pay-later payments, coupons, taxes, receipts, and a page where subscribers manage their own plans.
technical_manual:
  - modules/subscriptions
  - modules/checkout
  - modules/payments
---

## Who it's for and what you get

This use case is for businesses that sell a service by subscription, such as a membership, a support contract or a hosted website.

When it is done:

- visitors pick a plan on your site and pay through a checkout, with or without an account;
- they pay by card, Apple Pay or Google Pay through Stripe, or choose to pay later;
- coupons and taxes are applied at checkout;
- each subscriber gets a receipt after every payment and can manage their plan under **My Plans**;
- you see every subscriber under **Subscriptions > Agreements**, every payment under **Commerce > Payments**, and every unpaid balance under **Commerce > Transactions**.

<video controls preload="metadata" width="100%" poster="/img/docs/commerce-subscriptions.jpg" aria-label="Narrated video of a visitor signing up to a plan, and the Agreements screen">
  <source src="/img/docs/commerce-subscriptions.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/commerce-subscriptions.vtt" srcLang="en" label="English" default />
</video>

## Before you start

| You need | Who sets it up |
| --- | --- |
| The features **Subscriptions**, **Stripe** and **Payment Receipts**, plus **Pay Later**, **Taxation** or **Subscriptions - Sites** if you use them | Your administrator, in **Tools > Features** |
| Stripe connected, with your keys saved | Your administrator; see [Payment providers](../commerce/payment-providers.md) |
| The currencies you sell in | Your administrator; see [Products and currencies](../commerce/products-and-currencies.md) |
| Permissions to manage subscriptions and their settings | Your administrator; see [Roles and permissions](../getting-started/roles-and-permissions.md) |

<AskYourAdmin />

## Steps

1. **Connect Stripe.** Enter and save your Stripe keys, then connect your account. See [Payment providers](../commerce/payment-providers.md).
2. **Choose your currencies.** Under **Commerce > Currencies**, add the currencies you sell in. See [Products and currencies](../commerce/products-and-currencies.md).
3. **Set up taxes, if you charge them.** Under **Commerce > Taxation**, describe what you sell, where you charge tax and at what rate. See [Taxes](../commerce/taxes.md).
4. **Create your plans.** Create a plan for each thing you sell, with its prices, billing period and an optional free trial. See [Subscriptions](../commerce/subscriptions.md).
5. **Offer a discount, if you want.** Under **Commerce > Coupons**, create a coupon code to share with customers. See [Coupons](../commerce/coupons.md).
6. **Try the checkout.** Open a plan on your site and buy it, so you see what your customers see. See [Checkout](../commerce/checkout.md).
7. **Brand your receipts.** Under **Settings > Commerce > Receipts**, enter your business name, logo and footer. See [Invoices and receipts](../commerce/invoices-and-receipts.md).
8. **Sell websites, if that is your business.** With **Subscriptions - Sites**, each purchase creates the customer's own site. See [Selling sites](../commerce/sites.md).
9. **Follow your subscribers.** Use **Subscriptions > Agreements** to see each subscriber and cancel a subscription. Use **Commerce > Payments** to see and refund payments. Use **Commerce > Transactions** for balances still owed, such as Pay Later orders. See [Subscriptions](../commerce/subscriptions.md), [Payments and refunds](../commerce/payments-and-refunds.md) and [Transactions](../commerce/transactions.md).

## Check that it works

Use Stripe's test mode and its test card, so no real money moves.

1. In a private window, open a plan on your site and buy it with the test card, entering a coupon code if you made one.
2. The checkout confirms the purchase and a receipt is sent.
3. Under **Subscriptions > Agreements**, the new subscriber is listed as active.
4. Under **Commerce > Payments**, the payment is listed as succeeded.
5. Signed in as the customer, **My Plans** shows the plan and its next payment.

## Tips

- Test everything in Stripe's test mode first, then switch to live keys.
- A subscriber who cancels keeps access until the end of the period they have already paid for.
- A refund does not cancel a subscription. Cancel the subscription as well when you want to stop it.
- If a customer can't pay online, choose **Pay Later** at checkout: the balance is tracked under **Commerce > Transactions**, and you record the payment when it arrives.
