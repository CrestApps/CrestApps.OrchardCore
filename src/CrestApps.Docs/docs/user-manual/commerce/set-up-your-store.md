---
sidebar_label: Set up your store
title: Set up your store
description: "Go from a new site to your first sale: start from the Online store setup, then follow Commerce > Set up your store through your store's details, currency, payments, shipping, taxes, a first product and a test order, and add roles for your staff."
technical_manual:
  - modules/store-setup
---

A new store takes a few steps before its first sale: its name and contact details, how customers pay, where you ship, and a product to sell. The setup guide lists them in order, shows which are done, and does the simple ones for you.

| | |
| --- | --- |
| **Menu** | Commerce > Set up your store |
| **Permission** | Set up the store |
| **Feature** | Commerce, with the features of each step |

<AskYourAdmin />

## Start a new store

When you set up a new site, choose the **Online store** recipe. Your site starts with:

- Products, the cart and checkout, orders, shipping, taxes and payments turned on.
- A **Product** type with a price, a description and pictures.
- Your catalog as the home page, with a search and filters.
- The **Storefront** theme.

On a site you already have, the guide shows the steps for the features you've turned on.

## Follow the guide

Open **Commerce > Set up your store**. The step to do next is open. Each step says what it's for, and either has a short form to fill in or a link to the page where it's done in full. A tick shows a step that's done. The bar at the top shows how many steps are left.

1. **Your store's details.** Your store's name, the email customers write to, and optionally your phone and address. They're printed on receipts, invoices and order emails.
2. **Your currency.** The currency your prices are in and customers pay in.
3. **Take card payments.** Make an account at stripe.com and copy your keys into the Stripe settings. Start with the test keys, so you can try a sale without real money.
4. **Take bank transfers** (optional). Enter how customers pay you from their bank. Their order waits until you mark it paid.
5. **Ship your products.** Enter one rate for every order, wherever it goes. You can add zones by country, rates by weight or free shipping later, under **Shipping zones**.
6. **Charge tax.** Add a rule for each tax you charge. Skip it if you don't charge tax yet.
7. **Add your first product.** Give it a name, a price and pictures, and publish it. It's on sale as soon as it's published.
8. **Place a test order.** Open your store, add the product to the cart and check out. With Stripe in test mode, pay with the card 4242 4242 4242 4242, any date in the future and any three digits. No money is taken. Then refund or cancel the order under **Commerce > Orders**.
9. **Add roles for your staff** (optional). See below.

Choose **Skip this step** for a step you don't need, such as shipping when you sell only services. **Do this step after all** brings it back. When every step is done or skipped, the guide says your store is set up.

## Roles for your staff

**Add the staff roles** adds four roles, each allowed what that job needs:

| Role | What it can do |
| --- | --- |
| **Store manager** | Products, orders, refunds, stock, shipping, customers, promotions, reviews and reports. |
| **Warehouse** | Pick, pack and ship orders, and receive and count stock. |
| **Customer support** | Look up, change and refund orders, and manage customers, subscriptions and bookings. |
| **Marketing** | Promotions, coupons, reviews and product feeds, and edit products and pages. |

Give a staff member a role under **Security > Users**. Change what a role can do under **Security > Roles**. A role that was there already is left as it is.

## The Storefront theme

The Storefront theme gives your store:

- A header with your store's name or logo, a search box, the cart, the market picker and the customer's account menu.
- Your main menu under the header.
- A footer with your store's address, email and phone.

The name, logo and contact details come from **Settings > Commerce > Receipts**, the same ones on your receipts. Choose a theme under **Design > Themes**.
