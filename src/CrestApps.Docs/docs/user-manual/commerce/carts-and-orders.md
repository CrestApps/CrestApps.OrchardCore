---
sidebar_label: Carts and orders
title: Carts and orders
description: How visitors add products to a cart and check out, and where you and your customers find the orders.
technical_manual:
  - modules/carts-and-orders
---

When products are sold online, visitors can add them to a **cart** from your product listings and product pages, then check out to pay. Each purchase is recorded as an **order** that lists what was bought. Read this page to know what your buyers see and where to find their orders.

| | |
| --- | --- |
| **Menu** | **Commerce > Orders** for you, and **Commerce > My Orders** for your customers. The cart has no menu: buyers reach it from the cart link in your site's navigation bar. |
| **Permission** | **Manage orders** to see every order. **View own orders** for customers to see theirs; signed-in users have it by default. |
| **Feature** | Carts to show **Add to cart** and the cart. Orders, plus a [payment provider](payment-providers.md), to check out. |

<AskYourAdmin />

## What buyers see

- **Add to cart** appears on each product that can be bought online, on its listing card and its page. Buyers choose how many, and the option when a product has several prices. Products that are only listed, and subscription plans, do not show it; plans have their own **Sign up** button.
- The **Cart** link in the navigation bar shows how many items are in the cart.
- The **cart page** lists each product with its price, a quantity box and a remove button, and the subtotal. Discounts and tax are worked out at checkout.
- **Checkout** takes the buyer to the [checkout](checkout.md), where they enter a promotion code and pay. The confirmation shows their **order number**.

A cart always shows today's prices. If a price changed since a product was added, the cart says so. If a product can no longer be bought, the cart says why, and the buyer has to remove it before checking out.

Guests do not need an account to use a cart. If a guest signs in, what they put in the cart is kept and joins anything already in their account's cart. A cart nobody changes is cleared after 30 days for guests and 90 days for signed-in users.

## Shipped orders

With the [Shipping](shipping.md) feature on, an order with goods shows where it ships, how, and its shipments: each with what's in it, the carrier and the tracking link. The orders list and the order page say whether an order is *Unfulfilled*, *Partially fulfilled* or *Fulfilled*. See [Ship an order](shipping.md#ship-an-order).

## Finding orders

**Commerce > Orders** lists the orders placed on your site, newest first. Search by order number or by the customer's email, and filter by status:

| Status | Meaning |
| --- | --- |
| Paid | The checkout completed. |
| Awaiting payment | The buyer started the checkout and has not finished it yet. |
| Canceled | The order was canceled. The order page says why: the cart was checked out again (which made a newer order), the payment was declined, the checkout was canceled, nobody finished it in time, an item was out of stock when the buyer checked out, or you canceled it, with your reason. |

An order you gave money back for also shows **Partially refunded** or **Refunded** next to its status.

Unpaid orders don't stay open for ever. When a checkout is canceled, declined or left unfinished past the checkout's session lifetime (24 hours by default), its order is canceled, and a check every 15 minutes catches any that were missed. An order canceled because the cart was checked out again can still be paid by the checkout that was open for it.

Open an order to see what was bought, the quantities and prices, any discount such as a promotion code, the tax and the total paid, each refund and what's left, when it was placed and paid, who placed it, and the address it was billed to.

Signed-in customers see their own orders under **Commerce > My Orders**, and can open each one from the link on their order confirmation.

## Manage an order

Open an order from **Commerce > Orders**. The **Manage** box on the right does what you need, and every action is written in the order's **Notes** with who did it.

### Refund an order

1. Under **Refund**, enter the **Amount**. It starts at everything that can still be refunded.
2. Optionally give a **Reason**, such as *Scratched strap*. It is kept with the refund and in the notes.
3. Tick **Put the items back in stock** only when you refund everything: after a partial refund the customer keeps the goods.
4. Click **Refund**.

The money goes back the way it was paid: to the card or wallet for an order paid online, or through its transaction for an order paid with Pay Later. The order shows *Partially refunded* or *Refunded*, and the refund in its totals. A payment method that can't refund by itself records the refund for you to settle by hand on the [Refunds page](payments-and-refunds.md). Refunds made from the payment screens show on the order too.

### Cancel an order

1. Under **Cancel the order**, say **Why**, for example *Customer changed their mind*.
2. For a paid order, leave **Refund everything paid** ticked to give the money back, and **Put the items back in stock** ticked to return what it sold to stock.
3. Click **Cancel order** and confirm.

What a Pay Later order still owes is canceled, so the customer isn't reminded to pay it. A promotion code the order used can be used again. A canceled order can't be reopened.

### Send the confirmation again

Click **Send the confirmation again** to email the order confirmation to the customer once more, for example when they can't find it. It needs your site's email to be set up.

### Notes

Write a note under **Notes** and click **Add note** to keep something for the store, such as *Called the customer about the delivery date*. Customers never see notes. Refunds, cancellations and resent confirmations add their own notes, with who did them.

## Guests and their orders

A guest has no account to sign in to, so they reach their order through a **link**:

- The confirmation page and the confirmation email give a guest a link that opens their order: what they bought, the total, the billing address, and a **Pay now** button when something is still owed. A link works for 60 days. A changed or expired link shows *This order link no longer works* and nothing about any order.
- **Find my order** (`/orders/find` on your site) asks for an email and emails a link to each order placed with it in the last year. It always answers the same way, whether or not there are orders, so it never tells anyone who has bought from you. It is limited to a few requests per visitor and per email.
- When a guest later creates an account with the same email and confirms it, their guest orders join the account and appear under **My Orders**. See [Customers](customers.md#guests-who-create-an-account).

Link to **Find my order** from your site's footer or help page so guests can find it.

## Order confirmations

When an order is paid, the customer is emailed a confirmation with the order number, what they bought, any discount, the total and the billing address. It goes to the email given at checkout, once per order.

Under **Settings > Commerce > Orders**, **Send order confirmations** is on by default. Confirmations are sent only when your site's email is set up in Orchard Core's email settings; without it nothing is sent, and the order is placed as usual.
