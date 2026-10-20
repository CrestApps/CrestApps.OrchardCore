---
sidebar_label: Commerce
title: Commerce at a Glance
description: Learn what the commerce features do for your business (taking payments, selling plans and sites, payment plans, invoices, receipts and refunds), who sets them up, and which page explains each one.
technical_manual:
  - modules/commerce
---

The commerce features let your site take money. You can run an online store that ships goods or delivers downloads, sell subscriptions and whole websites, set up a payment plan for a customer over the phone, and keep track of what every customer owes and has paid. Customers get receipts and invoices, and they can pay online. You set it all up in the admin screens under **Commerce**, **Subscriptions** and **Settings**.

This section explains each feature in plain words. For installation, configuration files and the technical details, see the [Technical Manual commerce overview](../../modules/commerce.md).

This video walks through the commerce features, from connecting Stripe to refunds:

<YouTube video="commerce" title="Narrated video overview of the CrestApps Commerce modules" />

## What each feature does

| Feature | What it does for your business | Page |
| --- | --- | --- |
| **Payment providers** | Connect the site to Stripe to take cards, Apple Pay and Google Pay, or let customers pay later offline. | [Payment providers](payment-providers.md) |
| **Products and currencies** | Put a price on what you sell, choose the currencies you sell in, and decide whether products are sold online or only listed. | [Products and currencies](products-and-currencies.md) |
| **Product listings** | Publish pages that list your products with filters, sorting, search and a page for each product, for a shop, homes, cars or your plans. | [Product listings](product-listings.md) |
| **Taxes** | Charge the right tax for what you sell and where your customers are. | [Taxes](taxes.md) |
| **Coupons** | Give customers a discount code to enter at checkout. | [Coupons](coupons.md) |
| **Gift cards** | Sell gift cards, or give them by hand, and let buyers pay part or all of an order with one. | [Gift cards](gift-cards.md) |
| **Carts and orders** | Let shoppers fill a cart and check out, then follow, refund or cancel each order, print its invoice and packing slip, and create orders for buyers to pay. | [Carts and orders](carts-and-orders.md) |
| **Returns** | Let buyers ask to send items back for a refund or an exchange, and approve, receive and restock them. | [Returns and exchanges](returns.md) |
| **Customers** | Keep the people and businesses who buy from you, with their addresses, orders and tax exemption. | [Customers](customers.md) |
| **Store credit, points and referrals** | Give customers store credit, such as a refund as credit, reward what they buy with loyalty points, and reward them for referring friends. | [Store credit, loyalty points and referrals](store-credit-and-rewards.md) |
| **Inventory** | Count your stock in one or more locations, hold it while a buyer pays, stop selling what has run out, take pre-orders, email shoppers when it's back, and order more from suppliers. | [Inventory](inventory.md) |
| **Shipping** | Choose where you ship, the methods buyers can pick and what each costs, offer store pickup and local delivery, quote carriers' rates and buy their labels, and record shipments. | [Shipping](shipping.md) |
| **Digital downloads** | Sell files such as ebooks; buyers download them after paying through links that expire. | [Digital downloads](digital-downloads.md) |
| **Product reviews** | Let buyers rate what they bought, approve reviews, and sort listings by rating. | [Product reviews](reviews.md) |
| **Checkout** | The pages a buyer goes through to pay, with or without an account. | [Checkout](checkout.md) |
| **Headless API** | Sell from your own storefront, a phone app or a point of sale, and connect back-office systems, over a JSON API. | [Headless commerce API](headless-api.md) |
| **Webhooks** | Tell your other systems, such as a warehouse or accounting, the moment an order is paid or shipped, a customer is created, or stock runs low. | [Webhooks](webhooks.md) |
| **Point of sale** | Sell at your stores' counters from a till app: registers, shifts and their cash drawer, cash and card payments, Stripe card readers, receipts and returns at the till. | [Point of sale](point-of-sale.md) |
| **Subscriptions** | Sell plans that renew every month or year, with free trials, and let subscribers manage their own plans. | [Subscriptions](subscriptions.md) |
| **Selling sites** | Sell a ready-made website: the customer pays and their site is created for them. | [Selling sites](sites.md) |
| **Installment plans** | Sell something on a payment plan: take a down payment by card today, then collect the rest in scheduled payments. | [Installment plans](installment-plans.md) |
| **Transactions** | See every payment customers still owe, record payments taken outside the site, and send reminders. Customers pay their own balances. | [Transactions](transactions.md) |
| **Invoices and receipts** | Send numbered invoices with a link that pays them, and a receipt after every payment. | [Invoices and receipts](invoices-and-receipts.md) |
| **Payments and refunds** | See every payment the site took, and refund one in full or in part. | [Payments and refunds](payments-and-refunds.md) |

## Where to find it

| Menu | What is there |
| --- | --- |
| **Commerce** | **Orders**, **My Orders**, **Returns**, **Customers**, **Inventory**, **Purchase orders**, **Shipping**, **Reviews**, **Transactions**, **My Transactions**, **Payments**, **Refunds**, **Coupons**, **Gift cards**, **Webhooks**, **Point of sale**, **Currencies** and **Taxation**. |
| **Subscriptions** | **Agreements**, **My Plans**, **Installment Plans**, **Site provisioning** and **My Sites**. |
| **Settings** | **Payments > Stripe**, **Subscriptions**, and under **Commerce**: **Orders**, **Returns**, **Inventory**, **Shipping**, **Downloads**, **Reviews**, **Customers**, **Rewards**, **Referrals**, **Transactions** (reminders), **Receipts** and **Pay Later**. |

You only see the menus your role allows, and only for the features your site has turned on. Customers open **My Plans**, **My Sites** and **My Transactions** with their own account; they need no other admin access, and the rest of the admin stays closed to them.

<AskYourAdmin />

## Who does what

| Role | What they do | Pages for them |
| --- | --- | --- |
| **Administrator** | Turns on the commerce features, connects Stripe, sets up currencies, taxes and settings. | [Payment providers](payment-providers.md), [Products and currencies](products-and-currencies.md), [Taxes](taxes.md) |
| **Store manager** | Creates plans, coupons, gift cards and payment plans, follows up on what customers owe, and issues refunds, to the card or as store credit. | [Subscriptions](subscriptions.md), [Coupons](coupons.md), [Gift cards](gift-cards.md), [Store credit, loyalty points and referrals](store-credit-and-rewards.md), [Installment plans](installment-plans.md), [Transactions](transactions.md), [Payments and refunds](payments-and-refunds.md) |
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
- To see a working store before you build your own, run a store sample.

## Try a store sample

Each sample sets up a working store on a site, under **Configuration > Recipes**. Run one on a test site first, because it adds content types, products and settings.

| Your store sells | Run | What you get |
| --- | --- | --- |
| **Goods you ship** | **Product listing: online store sample** | 30 products with a **Shop** page at */shop*, stock, shipping zones, two customers, product reviews, and the coupons **WELCOME10** (10% off, once per customer) and **FREESHIP75** (free shipping on orders of $75 or more). |
| **Files to download** | **Digital downloads sample** | Two ebooks with their PDF files and an **Ebooks** page at */ebooks*, download links that work 3 times for 14 days, and the coupons **WELCOME10** and **EBOOKS5** ($5 off orders of $15 or more). |
| **Both** | The online store sample, then the digital downloads sample | One store selling both. A cart can hold goods and ebooks together: the buyer chooses shipping for the goods and gets download links for the ebooks. **WELCOME10** is added once. |

The samples don't turn on a way to pay. Connect one under [Payment providers](payment-providers.md) before you sell; on a test site, your administrator can turn on **Test payments**, which confirms every payment without moving money.
