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
| **Menu** | **Commerce > Orders** for you, and **Commerce > My Orders** for your customers. The cart has no menu: buyers reach it from the cart link in your site's navigation bar, or from **View cart** in the message shown when they add a product. |
| **Permission** | **Manage orders** to see every order. **View own orders** for customers to see theirs; signed-in users have it by default. |
| **Feature** | Carts to show **Add to cart** and the cart. Orders, plus a [payment provider](payment-providers.md), to check out. |

<AskYourAdmin />

This video follows a shopper from the cart to the order confirmation, and shows the checkout settings:

<YouTube video="carts-checkout" title="Video overview of the cart and the checkout" />

## What buyers see

<YouTube video="carts-checkout" title="Video of Add to cart, the mini cart and the cart page" start={93} end={139} />

- **Add to cart** appears on each product that can be bought online, on its listing card and its page. Buyers choose how many, and the option when a product has several prices. Products that are only listed, and subscription plans, do not show it; plans have their own **Sign up** button.
- The **Cart** link in the navigation bar shows how many items are in the cart. Only themes with a navigation bar, such as **TheTheme**, show it; in any theme, the message shown after adding a product has a **View cart** link.
- Clicking **Cart** slides out the **mini cart** from the side: each product with its quantity and price, a **Remove** link, the subtotal, **Checkout** and **View cart**. With the cart link on the page, **Add to cart** adds without leaving the page and opens the mini cart with *added to your cart*.
- The **cart page** lists each product with its price, a quantity box and a remove button, and the subtotal. Discounts and tax are worked out at checkout.
- **Checkout** takes the buyer to the [checkout](checkout.md), where they enter a promotion code and pay. The confirmation shows their **order number**.
- A product sold ahead of its release shows **Pre-order** on its button, and *Pre-order: ships* with the date in the cart, the mini cart and the order. See [Sell before a product is out](inventory.md#sell-before-a-product-is-out).

A cart always shows today's prices. If a price changed since a product was added, the cart says so. If a product can no longer be bought, the cart says why, and the buyer has to remove it before checking out.

Guests do not need an account to use a cart. If a guest signs in, what they put in the cart is kept and joins anything already in their account's cart. A cart nobody changes is cleared after 30 days for guests and 90 days for signed-in users.

## Shipped orders

With the [Shipping](shipping.md) feature on, an order with goods shows where it ships, how, and its shipments: each with what's in it, the carrier and the tracking link. The orders list and the order page say whether an order is *Unfulfilled*, *Partially fulfilled* or *Fulfilled*. See [Ship an order](shipping.md#ship-an-order). An order the buyer picks up from one of your locations shows **Pick up at** instead of where it ships; see [Store pickup](shipping.md#store-pickup).

## Finding orders

**Commerce > Orders** lists the orders placed on your site, newest first. Search by order number or by the customer's email, and filter by status:

| Status | Meaning |
| --- | --- |
| Paid | The checkout completed. |
| Awaiting payment | The buyer started the checkout and has not finished it yet. |
| Draft | An order you created for a buyer, not paid yet. Show them with the **Drafts** filter. See [Create an order for a buyer](#create-an-order-for-a-buyer). |
| Canceled | The order was canceled. The order page says why: the cart was checked out again (which made a newer order), the payment was declined, the checkout was canceled, nobody finished it in time, an item was out of stock when the buyer checked out, or you canceled it, with your reason. |

An order you gave money back for also shows **Partially refunded** or **Refunded** next to its status.

The channel filter shows where orders were made: **Every channel**, **Online** (bought on your site or in an app), **Point of sale** (rung up at one of your stores' registers, which shows as a badge with the register) or **Drafts sent by staff**. See [Point of sale](point-of-sale.md).

Unpaid orders don't stay open for ever. When a checkout is canceled, declined or left unfinished past the checkout's session lifetime (24 hours by default), its order is canceled, and a check every 15 minutes catches any that were missed. An order canceled because the cart was checked out again can still be paid by the checkout that was open for it.

Open an order to see what was bought, the quantities and prices, any discount such as a promotion code, the tax and the total paid, each refund and what's left, when it was placed and paid, who placed it, and the address it was billed to.

When a [gift card](gift-cards.md), [store credit or points](store-credit-and-rewards.md) paid part of the order, a line under the total says so, such as *Paid with Gift card ending 4F2K* and the amount; the rest was charged to the card or the other payment method. The invoice and the confirmation email show the same lines.

Signed-in customers see their own orders under **Commerce > My Orders**, and can open each one from the link on their order confirmation.

An order paid offline, by bank transfer, cash on delivery or Pay Later, reads **Payment due** in the list, on its page and in the buyer's account until you record the payment.

### Export orders

**Export CSV** in the list's search bar downloads the orders the list shows, with its search, status and channel: a row per line, with its add-ons, and the order's subtotal, discounts, shipping, tax, total and refunds on its first line, so adding up a column counts each order once.

## Offer one more thing at checkout

<YouTube video="orders-quotes-disputes" title="The order bump" start={51} end={88} />

Under **Settings > Commerce > Orders**, **Order bump** offers a product on the payment step, such as a small item that goes with most orders. Choose the **Product offered**, an optional **Discount** and a **Headline**. Buyers tick one box to add it, and the total changes before they pay. It isn't offered on orders that already have it, or while it's out of stock. Clear the product to stop the offer.

## Manage an order

Open an order from **Commerce > Orders**. The **Manage** box on the right does what you need, and every action is written in the order's **Notes** with who did it.

### Print an invoice or a packing slip

Above the order's details, **Invoice** and **Packing slip** open a page to print, with **Print invoice** or **Print packing slip** at the top. Your browser's print dialog can also save it as a PDF.

- The **invoice** has your store's name, logo, address and contact details, who it's billed to and where it ships, each item with its SKU, quantity and price, the discounts, shipping, tax and total, what was refunded, and the amount still due, with the order's status (*Paid*, *Payment due*, *Refunded* or *Canceled*).
- The **packing slip** has where the parcel goes, the shipping method, and the goods to pack, with a box to tick per line and no prices. Downloads and services are left off.
- Each shipment in the order's **Shipments** section has its own packing slip button, which lists only that shipment's items and its number.

The store's name, logo, address, contact details and footer come from **Settings > Commerce > Receipts**; without them, the invoice shows your site's name.

Buyers print their own invoice too: an **Invoice** button on their order's page, in their account or on the order link a guest has.

### Refund an order

1. Under **Refund**, enter the **Amount**. It starts at everything that can still be refunded.
2. Optionally give a **Reason**, such as *Scratched strap*. It is kept with the refund and in the notes.
3. Tick **Put the items back in stock** only when you refund everything: after a partial refund the customer keeps the goods.
4. Click **Refund**.

The money goes back the way it was paid: to the card or wallet for an order paid online, or through its transaction for an order paid with Pay Later. When a gift card, store credit or points paid part of the order, the card is refunded first, up to what it was charged, and what's left goes back on the gift card, the store credit or the points last; the notes say, for example, *Put $20 back on Gift card ending 4F2K.* The order shows *Partially refunded* or *Refunded*, and the refund in its totals. A payment method that can't refund by itself records the refund for you to settle by hand on the [Refunds page](payments-and-refunds.md). Refunds made from the payment screens show on the order too.

With the Store Credit feature on, and a buyer who has an account, the form also has **Give it as store credit**: *Added to the buyer's store credit to spend later, instead of going back to how they paid.* Tick it to give the whole amount as store credit, in the order's currency. Nothing goes back to the card; the order counts it as refunded, and the notes say *Gave $20 back as store credit.* See [Refund as store credit](store-credit-and-rewards.md#refund-as-store-credit).

Refunding an order that bought [gift cards](gift-cards.md#refunds-and-cancellations) in full turns off the cards nobody has spent from yet. A refund also takes back the [loyalty points](store-credit-and-rewards.md#how-points-are-earned-and-taken-back) the order earned, in proportion to what was refunded.

### Cancel an order

1. Under **Cancel the order**, say **Why**, for example *Customer changed their mind*.
2. For a paid order, leave **Refund everything paid** ticked to give the money back, and **Put the items back in stock** ticked to return what it sold to stock.
3. Click **Cancel order** and confirm.

What a Pay Later order still owes is canceled, so the customer isn't reminded to pay it. A promotion code the order used can be used again. Refunding everything paid includes putting back what a gift card, store credit or points paid. Gift cards the order bought are turned off when nobody has spent from them, the points it earned are taken back, and so is a [referral reward](store-credit-and-rewards.md#when-the-customer-is-rewarded) it earned someone. A canceled order can't be reopened.

### Send the confirmation again

Click **Send the confirmation again** to email the order confirmation to the customer once more, for example when they can't find it. It needs your site's email to be set up.

### Disputes

<YouTube video="orders-quotes-disputes" title="Disputes and risky payments" start={249} end={294} />

When a buyer disputes a card payment with their bank (a chargeback), Stripe tells your site and the order shows it: a red **Disputed** box under the items, with the amount, why the buyer disputed it (for example *they don't recognize the payment*) and its status, such as *needs your response*. The orders list marks the order **Disputed** while it is open, and a note records each change.

Answer a dispute from your Stripe dashboard before its deadline, with evidence such as the shipping tracking: otherwise the money goes back to the buyer. When it is decided, the box says *won, the money stays with you* or *lost, the money went back to the payer*. It needs Stripe's webhook, which Stripe Connect sets up.

### Fraud risk

Stripe's fraud checks rate every card payment. An order whose payment was rated **elevated** or **highest** risk shows a warning on its page, with the score out of 99 when Stripe gives one, and a **Risky** badge in the orders list. Look at it before shipping: does the shipping address fit the billing address and the email, is it a large first order of easy-to-resell goods? Refund and cancel it if it doesn't look right. A normal rating is a quiet line on the order's page.

### Notes

Write a note under **Notes** and click **Add note** to keep something for the store, such as *Called the customer about the delivery date*. Customers never see notes. Refunds, cancellations and resent confirmations add their own notes, with who did them.

## Create an order for a buyer

Put an order together for a buyer yourself, such as one taken over the phone, and email them an invoice to pay it online.

1. On **Commerce > Orders**, click **Create order**.
2. Under **Buyer**, enter their **Email** and **Name**. The invoice goes to the email. A buyer with an account under that email gets their own prices and sees the order in their account.
3. Under **Add products**, click **Find products**, choose them, and click **Update prices**. Each comes in with a quantity of 1: change the **Quantity**, choose a variant where the product has them, or tick **Remove**. The **Amount** of each line and the **Subtotal** are the buyer's prices.
4. Under **Invoice**, write a **Note for the buyer** if you like, such as *Thanks for your call. Here's the order we discussed.*, and a **Pay by (optional)** date.
5. Click **Save draft**. The order's page opens, with a box that says it's *a draft, not sent to the buyer yet*.
6. Click **Send invoice**. The buyer is emailed *Your order ... is ready to pay*, with the items, the subtotal, your note, the date to pay by and a **Review and pay** button.

Shipping and tax aren't on the draft: the buyer adds them at checkout, which already knows their name and email. Until they pay, the order's page says *Invoice emailed to ... Waiting for payment.*, and **Send again** emails it once more. While it's a draft, **Edit** changes its items and details; while the buyer is paying, it can't be changed. An order you created isn't canceled for being left unpaid, the way a buyer's unfinished checkout is: cancel it yourself if you no longer want it.

The items are held from stock like a buyer's checkout. When you change them and there isn't enough of something, the draft says so and keeps its earlier items.

The buyer sees the order on its link, or in their account, with *This order is waiting for your payment*, your note, the date to pay by, and **Pay now**.

Sending the invoice needs your site's email to be set up; the draft's editor warns you when it isn't. Either way, the order's page shows the draft's **Pay link**: copy it and send it to the buyer yourself, such as in a chat.

### Add items to a paid order

<YouTube video="orders-quotes-disputes" title="Add items to a paid order" start={194} end={221} />

A paid order can't be changed, but on its page **Add items** starts a new order for the same buyer, marked *These items are added to order ...*. Fill it in and send its invoice like any other; its page links back to the paid order. To take items off a paid order, refund them, or let the buyer [return them](returns.md).

## Quotes

<YouTube video="orders-quotes-disputes" title="Quotes" start={88} end={110} />

Let signed-in buyers ask for a quote instead of checking out, such as for a large or trade order.

1. On **Settings > Commerce > Orders**, tick **Take quote requests** and save.
2. A signed-in buyer now sees **Request a quote** under **Checkout** on their cart. They can write a message, such as the quantities they need and by when, and give a purchase order number. Their cart is emptied, and the quote shows under their orders as *Asked for*.
3. The quote is a draft order on **Commerce > Orders**, with the buyer's message. Click **Edit**, and under **Price each** enter the price you'll offer for a line; leave it blank for your usual price. Set **Pay by (optional)**: the quote expires then. Click **Update prices** to see the totals, then **Save draft**.
4. Click **Send quote**. The buyer is emailed *Your quote ... is ready*, with the prices, until when it's offered, and a **Review and accept** button.

The buyer accepts the quote by paying it, from the email or their orders, before it expires. Prices you set are final: automatic promotions don't come off them. They can also **Turn it down**, or **Withdraw the request** before you've sent it; either cancels it. An expired quote can't be paid: change its date to pay by and send it again to offer it again.

Sending a quote needs your site's email to be set up.

## Do something when an order is paid

With Orchard Core's **Workflows** on, the store tells your workflows what happens, so you can email someone, call another system or add a note without code. Start a workflow from **Order Paid**, **Order Canceled** or **Order Refunded**. Orders also adds two ready-made workflows under **Tools > Recipes**: **Automation: thank-you email** (turned on) and **Automation: canceled order alert** (turned off until you set the address it goes to).

The other parts of the store add events of their own: **Subscription Started**, **Renewed**, **Past Due**, **Canceled** and **Expired**; **Stripe Payment Received**, **Payment Failed**, **Payment Refunded** and **Dispute Created**, among others; and the setup wizard's **Wizard Started**, **Step Displayed**, **Completed** and **Failed**. To send the same news to another system as soon as it happens, without a workflow, use [Webhooks](webhooks.md). The technical guides list every event with what it carries: [Orders](../../modules/carts-and-orders.md#workflow-events), [Subscriptions](../../modules/subscriptions.md) and [Payments](../../modules/payments.md#workflow-events).

## Write your own emails

On **Settings > Commerce > Emails**, write the emails your store sends about orders in your own words.

- **Layout** goes around every one of them, such as your logo at the top and your address at the bottom. Put `{{ Content }}` where the email goes.
- Each email has its own **Subject** and **Body**: the order confirmation, the order to pay you send a buyer, the quote, the shipped email, the return emails, the gift card, the email to a buyer who left their checkout, and, with Bookings, the booking emails. Tick **Use my template** to use yours.

In a template, `{{ Content }}` is the email as the store would send it, so a body of *Thanks so much!* followed by `{{ Content }}` adds a line above it. `{{ Order.OrderNumber }}` and `{{ Order.CustomerName }}` put in the order's details, and each email lists its own links, such as `{{ PayUrl }}`. A template that has a mistake can't be saved, and one that can't be filled in sends the store's own email instead.

## Store reports

<YouTube video="commerce-reports-dashboard" title="Store reports" start={84} end={135} />

With the **Reports** feature on, **Reports > Commerce** answers what the store sold. Choose a period at the top, such as *Today* or *Last Month*, and click **Show**; every report exports to CSV, and to Excel with Reports (OpenXml).

| Report | Shows |
| --- | --- |
| **Sales** | The orders paid in the period: how many, the average order value, items per order, what the items came to, discounts, shipping, tax, the total charged, what was refunded and what you kept, day by day with a chart. |
| **Sales by product** | How many of each product and variant sold, and for how much before discounts, and the same for goods, digital products and services. |
| **Sales by category** | What each category sold. A product in two categories counts in both. |
| **Customers** | Who bought: first-time and returning buyers, how many have bought more than once, what buyers have spent in all on average, and your top buyers. |
| **Cohorts and retention** | Buyers grouped by the month of their first order, and how many of each month's buyers came back in each of the six months after. |
| **Checkouts** | The checkouts buyers started and how they ended: paid, payment failed, left or expired, or still open, with the share that were paid. |
| **Sales by country** | Orders and totals for each country they ship to. |
| **Tax on orders** | The tax orders charged, day by day. |
| **VAT one-stop shop (OSS)** | The VAT charged on orders to other EU countries, by country and rate, refunds taken off: the figures of the EU one-stop-shop return. See [Taxes](taxes.md#vat-one-stop-shop-oss-report). |
| **Discounts on orders** | How many orders used each promotion code, and what it took off. |
| **Refunds** | Each refund given in the period, with its order and reason. |
| **Disputes** | The payments buyers disputed with their bank in the period, open ones first: why, their status, and how many were won and lost. |
| **Orders to ship** | Paid orders that haven't shipped in full, oldest first, and how many days each has waited. |
| **Stock on hand** | With Inventory on: every counted item's stock, out of stock and low first. |
| **Stock movements** | With Inventory on: what each item received, sold, got back and was corrected by, and how many days its stock lasts at the rate it sold. |
| **Shipping times** | With Shipping on: how long orders waited from payment to shipping, how long deliveries took, by carrier, and the slowest shipments. |
| **Downloads** | With Digital Downloads on: the download links buyers got, and how many were used, never used, used up, expired or taken back. |
| **Product reviews** | With Product Reviews on: the reviews written, how many wait for approval, the stars given, and each product's rating. |

Amounts in different currencies are never added together: each currency gets its own summary. Days follow your site's time zone, and a period longer than two months is shown by month. Reports count orders by the day they were paid, including orders canceled since, so a refund shows as a refund rather than making a sale disappear.

### On the dashboard

<YouTube video="commerce-reports-dashboard" title="Reports on the dashboard" start={235} end={298} />

With the **Admin Dashboard** feature on, open **Manage Dashboard > Add Widget**. Each report above has a ready-made widget, such as **Sales Overview**, **Sales Trend**, **Top Selling Products**, **Orders to Ship**, **Refunds**, **Payment Disputes** and **Low Stock**, and the **Report** widget shows any report you may view. Set the widget's **Period** (last 30 days moves with the clock), what to **Show** (figures, tables or charts), how many **Rows per table**, and **Keep for (minutes)**, how long it keeps what it shows. A person sees a widget only when they may view its report.

The **Sales Summary** widget shows your sales today, over the last 7 and 30 days, and the orders to ship.

To build your own report, run the **Reports: store reports to adapt** recipe under **Tools > Recipes**. It adds the order reports to **Reports > Report Builder**, in the **Store** group, where you can run, change and save them as your own.

## Wishlists

With **Wishlists** on, a signed-in shopper can click **Save to wishlist** on a product's page, and find what they saved under **Wishlist** in their account, each with its **Add to cart**. A product you stop selling drops off their wishlist.

## The buyer's account

A signed-in buyer opens **My account** from the user menu, or `/account` on your site. The account lists what the enabled features offer, as tiles and as a menu beside every account page (pills across the top on a phone):

| Page | What it shows | Feature |
| --- | --- | --- |
| **Orders** | Their orders, newest first, with the status and total. Each opens the order's page, with an **Invoice** to print; an order awaiting payment has **Pay now**, and with Returns on, a paid order has **Return items**. | Orders |
| **Downloads** | The files of their paid orders, with the downloads left and the date each link expires, and why a link stopped working. | Digital Downloads |
| **Subscriptions** | Their subscriptions, with **Cancel** at the end of the period. | Subscriptions |
| **Balances and invoices** | What they owe and their invoices and receipts. | Transactions |
| **Addresses** | Their address book: add, edit, delete, and pick the default billing and shipping addresses. | Customers |
| **Account details** | Whether they buy for themselves or a business, their name, phone and marketing consent, and a link to change their password. | Customers |
| **Payment methods** | The cards they saved at checkout, to remove one, and the cards kept for automatic payments such as installments, to pick the default one or remove one. | Stripe |
| **Store credit** | Their store credit and each change to it. See [Store credit](store-credit-and-rewards.md#store-credit). | Store Credit |
| **Rewards** | Their loyalty points, what they're worth, and how they earn and spend them. See [Loyalty points](store-credit-and-rewards.md#loyalty-points). | Loyalty Points |
| **Refer a friend** | Their referral link, and the friends who bought through it. See [Referrals](store-credit-and-rewards.md#referrals). | Referrals |

### An account from the confirmation

A guest's confirmation page shows **Save your details for next time** with a password and a confirmation box. The account is made with the email on the order, the order joins it, and the guest is signed in and taken to the order in their account. When your site asks new users to confirm their email or waits for an administrator to approve them, the account follows those settings, and the guest signs in after. An email that already has an account is told to sign in instead. The offer works for two hours after the order.

## Buyers who leave without paying

Under **Settings > Commerce > Orders**, turn on **Email buyers who leave a checkout** to send a **recovery email** to a buyer who started a checkout, gave their email and left without paying: they canceled, the payment failed, or the checkout expired or was left. **Send it after** sets how long to wait, one hour by default. The email lists what they chose and has **Return to your cart**, which opens their checkout when it is still open, or fills a cart with the same products.

- Each order gets one recovery email at most, and only within a week of the order.
- Nobody is emailed who paid for an order since, or whose order you canceled.
- The emails link back to your site through its **Base URL** under **Settings > General**; without one, none is sent. Your site's email must be set up too.

## Guests and their orders

A guest has no account to sign in to, so they reach their order through a **link**:

- The confirmation page and the confirmation email give a guest a link that opens their order: what they bought, the total, the billing address, and a **Pay now** button when something is still owed. A link works for 60 days. A changed or expired link shows *This order link no longer works* and nothing about any order.
- **Track your order** (`/orders/track` on your site) asks for the order number and the email the order was placed with, and opens the paid order's page, with its shipments and where each parcel is. Both must match, and tries are limited, so a number alone shows nothing. **Find my order** links to it.
- **Find my order** (`/orders/find` on your site) asks for an email and emails a link to each order placed with it in the last year. It always answers the same way, whether or not there are orders, so it never tells anyone who has bought from you. It is limited to a few requests per visitor and per email.
- When a guest later creates an account with the same email and confirms it, their guest orders join the account and appear under **My Orders**. See [Customers](customers.md#guests-who-create-an-account).

Link to **Track your order** and **Find my order** from your site's footer or help page so guests can find them.

## Order confirmations

When an order is paid, the customer is emailed a confirmation with the order number, what they bought, any discount, the total, what a gift card, store credit or points paid toward it, and the billing address. It goes to the email given at checkout, once per order.

The confirmation, and the other emails about the order (shipped, returns, gift cards and the reminder of a checkout left unpaid), are written in the language the buyer shopped in. See [Markets](markets.md#languages).

Under **Settings > Commerce > Orders**, **Send order confirmations** is on by default. Confirmations are sent only when your site's email is set up in Orchard Core's email settings; without it nothing is sent, and the order is placed as usual.
