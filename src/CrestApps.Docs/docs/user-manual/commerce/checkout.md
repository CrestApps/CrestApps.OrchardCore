---
sidebar_label: Checkout
title: Checkout
description: What a buyer sees when they pay on your site, from the first step to the order confirmation, including promotion codes, card and wallet payments, and buying without an account.
technical_manual:
  - modules/checkout
  - modules/payments
---

The **checkout** is the page where a buyer reviews what they are buying, fills in what the purchase needs and pays. The same checkout is used for every kind of purchase on your site: signing up to a subscription plan, and paying an outstanding balance through a **Pay now** button or a payment link (see [Transactions](transactions.md)). Read this page to know what your buyers see, so you can help them and test your own setup.

| | |
| --- | --- |
| **Menu** | None. Buyers reach the checkout from your site, for example by signing up to a [subscription](subscriptions.md) or opening a payment link. |
| **Permission** | None. A checkout only opens for the visitor who started it. |
| **Feature** | Checkout, plus at least one [payment provider](payment-providers.md) (Stripe or Pay Later) |

<AskYourAdmin />

**Settings > Commerce > Checkout** picks how the checkout is laid out (see [One page or a page per step](#one-page-or-a-page-per-step)). What the checkout offers comes from the purchase itself, from your [payment providers](payment-providers.md), your [coupons](coupons.md) and your [tax setup](taxes.md).

<YouTube video="commerce" title="Narrated video of a visitor signing up to a plan, and the Agreements screen" start={160} end={232} />

## The checkout page

The page has three parts:

- **The steps** across the top, numbered in order. A finished step shows a check mark, and the buyer can click it to go back and change it. Steps not reached yet cannot be opened, so nobody reaches payment with an earlier step unfilled. When the checkout has only one step, the row is not shown.
- **The current step** on the left, with **Back** (to the previous step) and **Continue**. On the last step the button reads **Pay now**. While the payment is being taken it shows **Processing…**. When the buyer started the checkout from a page of your site that asks to be returned to, such as a payment page or a list of payments, the **Payment** step (and the first step) also has a link back to that page, for example **Back to the payment page**. It reads **Back**, or **Leave checkout** when there is also a **Back** to the previous step, unless the page named its own wording.
- **The order summary** on the right, on every step, so the total is never a surprise.

### Steps a buyer may see

Which steps appear depends on what is being bought. **Payment** is always last.

| Step | When it appears |
| --- | --- |
| **Contact** | Buying from the cart, when the [Customers](customers.md) feature is on. Asks who the order is for and, as your settings say, where it is billed, and any [fields of your own](#your-own-checkout-fields). A signed-in buyer skips it when it has nothing to ask. See [Who the order is for](#who-the-order-is-for). |
| **Shipping** | Buying goods from the cart, when the [Shipping](shipping.md) feature is on. Asks where the order goes and how, from the methods of the zone the address is in, with what each costs. Orders of downloads and services skip it. |
| **Plan** | Signing up to a subscription plan. |
| **Registration** | Signing up to a subscription while not signed in. See [Buy without an account](#buy-without-an-account). |
| One step per content type | When a plan asks the buyer to fill in content, such as details about their organization. The step is named after the content type. |
| **Your new site** | When the plan creates a new site for the buyer. See [Sites](sites.md). |
| **Outstanding payment** | Paying an outstanding balance. |
| **Choose how to pay** | Paying an outstanding balance that the site lets customers pay in installments. The buyer picks **Pay in full** or one of the installment choices, such as *3 monthly payments*. See [Pay in installments](transactions.md#pay-in-installments). |
| **Payment** | Always, as the last step. |

### One page or a page per step

Under **Settings > Commerce > Checkout**, **Checkout layout** is one of:

- **A page per step**, the default: the steps described above, one page each.
- **One page**: every step is a numbered section of one page, with the order summary beside them (below them on a phone). The buyer fills in their details and presses **Continue**. Once every step is complete, **Payment** appears below them on the same page, with **Pay now**. If the buyer then changes a detail, such as the shipping method, **Pay now** gives way to **Update order** with *You changed your details. Update your order to see the total before you pay.* Updating saves the change and shows the new total, so what they pay is what they see.

Opening the settings page takes the **Manage checkout settings** permission.

### The order summary

The **Order summary** card lists each item with its price. A recurring item shows how often it is billed underneath, for example *Every 1 month*. When tax applies, the card shows a **Subtotal** and one line per tax, using the tax's display name (or **Tax**). The last line, **Due now**, is what the buyer pays today.

## Pay

On the **Payment** step, the buyer:

1. Optionally enters a promotion code (see below).
2. Picks a **Payment method**. Each method is a large button: **Credit or debit card (Stripe)** (*Pay securely online and confirm immediately.*), **Pay Later** (*Confirm now and pay later.*), **Bank transfer** and **Cash on delivery**, as you turned them on (see [Offline payments](payment-providers.md#offline-payments-pay-later-bank-transfer-and-cash-on-delivery)). Only methods that can pay for this purchase are shown: cash on delivery only for an order that is delivered. On a test or development site there may also be **Test payment**, which confirms the payment without taking any money; see [Try the checkout with test payments](payment-providers.md#try-the-checkout-with-test-payments).
3. For card: picks one of their [saved cards](payment-providers.md#saved-cards), or enters the **Name on card** and the **Card details**, optionally ticking **Save this card for next time**, or taps an Apple Pay or Google Pay button when the device offers one.
4. Clicks **Pay now**.

If the card's bank asks the cardholder to approve the payment, the approval window opens on top of the page. When the payment is confirmed, the buyer lands on the confirmation page.

| What the buyer sees | What it means |
| --- | --- |
| *No payment is required to complete this checkout.* | Nothing is owed today, for example a free plan or a trial. The buyer just confirms. |
| *This purchase requires a payment, but no payment method is available. Please contact the site administrator.* | No payment provider can take this payment. Check your [payment providers](payment-providers.md). |
| *Card payment is not fully configured.* | Stripe is missing its publishable key. See [Connect Stripe](payment-providers.md#connect-stripe). |
| *Stripe is in test mode. No real payment will be taken.* | Stripe is using your test environment. |
| *Please choose a payment method.* | The buyer clicked **Pay now** without picking a method. |
| *Your payment is still being processed.* | The provider has not confirmed yet. The buyer does not need to pay again; the purchase completes on its own once the provider confirms, even if the buyer closes the browser. |
| *Your payment could not be completed. Please try again.* | The payment failed, for example a declined card. The buyer stays on the payment step and can try again or pick another method. |
| *The card you pay with now is saved, and the remaining payments are charged to it automatically on their due dates.* | The buyer chose to pay in installments. Only payment methods that can save a card are shown. |

## Apply a promotion code

The **Promotion code** box sits under the order summary on the **Payment** step.

1. Type the code in **Enter a code**. Capital letters do not matter.
2. Click **Apply**. The page refreshes the order summary; no payment is taken.
3. When the code takes money off, the box shows the amount, for example *USD 10.00 off applied.*, and the summary shows the new total.

The summary lists the discount as *Discount (CODE)*. It comes off the items, never shipping, and tax is worked out on the discounted prices.

To remove a code, clear the box and click **Apply**. A code that is wrong shows *That code is not recognized.*; a code that is disabled, expired, not started yet or used up shows *That code is no longer available.* A code can be accepted and still take nothing off, for example when the order is below the coupon's minimum amount; the box then shows no amount. A code limited to other payments or another customer shows *That code can't be used for this payment.* A buyer who has used a code as often as it allows one customer sees *You have already used that code as many times as it allows.* Trying many codes in a row shows *Too many codes were tried. Wait a few minutes, then try again.*

If a code runs out or stops working while the buyer is paying, for example someone else took its last use, the payment does not go through. The page shows the order again without the code, with the new total, and says why, for example *The code LAST1 ran out before you paid, so it was taken off. Check your new total, then pay again.*

When the buyer is paying an existing balance (a transaction, from **My Transactions** or a pay link), what the code takes off is taken off the balance: a 100% code pays it without a card, and a smaller code leaves the rest to pay. See [Coupons](coupons.md#coupons-on-a-payment-for-a-transaction).

## Who the order is for

With the Customers feature on, the **Contact** step of an order asks for:

- **Full name** and **Email**, so the order and its messages reach the buyer, and a **Phone** if they want to give one. A signed-in buyer starts with their own details, and can change them for this order.
- A **Billing address**, when your settings ask for one. It starts with the country and lays itself out the way that country writes addresses, with its list of states or provinces where it has one. A signed-in buyer can pick one of their saved addresses instead, and tick **Save a new address to my address book**.

Under **Settings > Commerce > Customers**, **Billing address at checkout** is **Optional** (asked for, may be left blank; the default), **Required** (for example for invoices or tax) or **Off**. A checkout keeps the setting it started with.

When the order is paid, it keeps its own copy of the name, email, phone and billing address, so later changes never change it. A guest becomes a customer of their own, listed under **Commerce > Customers** with the address they entered.

## Your own checkout fields

To ask buyers for something of your own, such as a gift message, a delivery date or a purchase order number:

1. Enable the **Checkout details** feature (**Tools > Features**).
2. Go to **Design > Content Types**, open **Checkout details** and add the fields you need, for example a **Text Field** named *Gift message*. Tick **Required** in a field's settings to make buyers fill it in.

The fields appear under **Order details** on the **Contact** step, in the order the content type lists them. What the buyer enters is kept on the order and shown under **Order details** on the order's page, for you and for the buyer. A field left out while required keeps the buyer on the step with *A value is required for …*. With no fields on the content type, the checkout asks for nothing extra.

When a buyer picks one of their saved addresses on the **Contact** or **Shipping** step, the form for a new address is hidden until they choose **A new address, entered below**.

## Buy without an account

When someone signs up to a subscription without being signed in, the **Registration** step asks them to create an account. It starts with *Already registered?* and a **Click here to log in.** link that brings the buyer back to the checkout after signing in. A buyer who is already signed in never sees this step.

If your site allows it (the **Allow guest sign-up** subscription setting, see [Subscriptions](subscriptions.md)), the step also offers **Subscribe as guest**, with the note *Subscribing as a guest does not include the ability to manage your subscription.* Ticking it hides the **Create an account** form.

Buying from the cart needs no account: a guest gives their name and email on the **Contact** step.

Paying an outstanding balance through a payment link needs no account.

## The confirmation page

A guest's confirmation page offers **Save your details for next time**: they choose a password, and an account is made with the email on the order, which joins the account. See [Carts and orders](carts-and-orders.md#an-account-from-the-confirmation).

After a successful checkout the buyer sees **Order confirmation** with *Thank you — your purchase is complete.*, followed by the order summary of what they bought.

A checkout that is not an order can carry its own heading instead. Paying from a payment link, for example, shows **Payment received** with *Thank you. A confirmation is shown below.* When the buyer started the checkout from a page that asks to be returned to, the confirmation ends with a button back to it: **Continue**, or the page's own wording such as **Back to the payment page**.

## A checkout that has ended

A checkout that was canceled, expired before it was paid, or could not be completed can't be paid any more. Opening it shows *This checkout is no longer available.* and the reason: *This checkout was canceled.*, *This checkout expired before it was completed.* or *This checkout could not be completed.* When the buyer started the checkout from a page that asks to be returned to, the page has a **Back** link to it (or the page's own wording). Opening a checkout that was already paid shows its confirmation.
