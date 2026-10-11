---
sidebar_label: Commerce
title: Commerce at a Glance
description: Learn what the commerce features do for your business (taking payments, selling plans and sites, payment plans, invoices, receipts and refunds), who sets them up, and which page explains each one.
technical_manual:
  - modules/commerce
---

The commerce features let your site take money. You can sell subscriptions and whole websites online, set up a payment plan for a customer over the phone, and keep track of what every customer owes and has paid. Customers get receipts and invoices, and they can pay online. You set it all up in the admin screens under **Commerce**, **Subscriptions** and **Settings**.

This section explains each feature in plain words. For installation, configuration files and the technical details, see the [Technical Manual commerce overview](../../modules/commerce.md).

## What each feature does

| Feature | What it does for your business | Page |
| --- | --- | --- |
| **Payment providers** | Connect the site to Stripe to take cards, Apple Pay and Google Pay, or let customers pay later offline. | [Payment providers](payment-providers.md) |
| **Products and currencies** | Put a price on what you sell, and choose the currencies you sell in. | [Products and currencies](products-and-currencies.md) |
| **Taxes** | Charge the right tax for what you sell and where your customers are. | [Taxes](taxes.md) |
| **Coupons** | Give customers a discount code to enter at checkout. | [Coupons](coupons.md) |
| **Checkout** | The pages a buyer goes through to pay, with or without an account. | [Checkout](checkout.md) |
| **Subscriptions** | Sell plans that renew every month or year, with free trials, and let subscribers manage their own plans. | [Subscriptions](subscriptions.md) |
| **Selling sites** | Sell a ready-made website: the customer pays and their site is created for them. | [Selling sites](sites.md) |
| **Installment plans** | Sell something on a payment plan: take a down payment by card today, then collect the rest in scheduled payments. | [Installment plans](installment-plans.md) |
| **Transactions** | See every payment customers still owe, record payments taken outside the site, and send reminders. Customers pay their own balances. | [Transactions](transactions.md) |
| **Invoices and receipts** | Send numbered invoices with a link that pays them, and a receipt after every payment. | [Invoices and receipts](invoices-and-receipts.md) |
| **Payments and refunds** | See every payment the site took, and refund one in full or in part. | [Payments and refunds](payments-and-refunds.md) |

## Where to find it

| Menu | What is there |
| --- | --- |
| **Commerce** | **Transactions**, **My Transactions**, **Payments**, **Refunds**, **Coupons**, **Currencies** and **Taxation**. |
| **Subscriptions** | **Agreements**, **My Plans**, **Installment Plans**, **Site provisioning** and **My Sites**. |
| **Settings** | **Payments > Stripe**, **Subscriptions**, and under **Commerce**: **Transactions** (reminders), **Receipts** and **Pay Later**. |

You only see the menus your role allows, and only for the features your site has turned on.

<AskYourAdmin />

## Who does what

| Role | What they do | Pages for them |
| --- | --- | --- |
| **Administrator** | Turns on the commerce features, connects Stripe, sets up currencies, taxes and settings. | [Payment providers](payment-providers.md), [Products and currencies](products-and-currencies.md), [Taxes](taxes.md) |
| **Store manager** | Creates plans, coupons and payment plans, follows up on what customers owe, and issues refunds. | [Subscriptions](subscriptions.md), [Coupons](coupons.md), [Installment plans](installment-plans.md), [Transactions](transactions.md), [Payments and refunds](payments-and-refunds.md) |
| **Customer** | Buys online, pays invoices from a link, and manages their own plans, sites and balances. | [Checkout](checkout.md), [Invoices and receipts](invoices-and-receipts.md), [Transactions](transactions.md#my-transactions-for-customers) |

## How the pieces fit together

Every way of paying ends up in the same place, so there is one record of what each customer owes and paid:

1. **Something is sold.** A customer buys a subscription or a site online, or a store manager sets up an installment plan.
2. **The customer pays through the checkout.** By card, Apple Pay or Google Pay through Stripe, or later with Pay Later. A coupon and taxes are applied along the way.
3. **What is owed becomes a transaction.** Anything not paid in full, such as a Pay Later order or a scheduled payment of a plan, is listed under **Commerce > Transactions**. The customer sees it under **My Transactions** and is reminded before and after it falls due.
4. **The customer gets paperwork.** An invoice with a link that pays it, and a receipt after each payment.
5. **Every payment is recorded.** Each one is listed under **Commerce > Payments**, where it can be refunded.

## Start here

- To sell plans online, follow [Sell subscriptions online](../use-cases/sell-subscriptions-online.md).
- To sell on a payment plan, follow [Sell on a payment plan](../use-cases/sell-on-a-payment-plan.md).
