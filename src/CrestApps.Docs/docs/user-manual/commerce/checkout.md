---
sidebar_label: Checkout
title: Checkout
description: What a buyer sees when they pay on your site, from the first step to the order confirmation, including promotion codes, gift cards, store credit and points, card and wallet payments, and buying without an account.
technical_manual:
  - modules/checkout
  - modules/payments
---

The **checkout** is the page where a buyer reviews what they are buying, fills in what the purchase needs and pays. The same checkout is used for every kind of purchase on your site: signing up to a subscription plan, and paying an outstanding balance through a **Pay now** button or a payment link (see [Transactions](transactions.md)). Read this page to know what your buyers see, so you can help them and test your own setup.

| | |
| --- | --- |
| **Menu** | Settings > Commerce > Checkout (the layout). Buyers reach the checkout itself from your site, for example by signing up to a [subscription](subscriptions.md) or opening a payment link. |
| **Permission** | None. A checkout only opens for the visitor who started it. |
| **Feature** | Checkout, plus at least one [payment provider](payment-providers.md) (Stripe or Pay Later) |

<AskYourAdmin />

A checkout a buyer leaves unfinished is canceled after 24 hours. That time is not a setting in the admin; ask your developer if you need a different one. **Settings > Commerce > Checkout** picks how the checkout is laid out (see [One page or a page per step](#one-page-or-a-page-per-step)). What the checkout offers comes from the purchase itself, from your [payment providers](payment-providers.md), your [coupons](coupons.md) and your [tax setup](taxes.md).

<YouTube video="carts-checkout" title="Carts and checkout: the buyer's journey and the checkout settings" />

## The checkout page

The page has three parts:

- **The steps** across the top, numbered in order. A finished step shows a check mark, and the buyer can click it to go back and change it. Steps not reached yet cannot be opened, so nobody reaches payment with an earlier step unfilled. When the checkout has only one step, the row is not shown.
- **The current step** on the left, with **Back** (to the previous step) and **Continue**. On the last step the button reads **Pay now**, or **Place order** when nothing is left to pay, for example when a gift card or a code covers the whole order. While the payment is being taken it shows **Processing…**. When the buyer started the checkout from a page of your site that asks to be returned to, such as a payment page or a list of payments, the **Payment** step (and the first step) also has a link back to that page, for example **Back to the payment page**. It reads **Back**, or **Leave checkout** when there is also a **Back** to the previous step, unless the page named its own wording.
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
- **One page**: every step is a numbered section of one page, with the order summary beside them (below them on a phone). The buyer fills in their details and presses **Continue**. Once every step is complete, **Payment** appears below them on the same page, with **Pay now** (or **Place order** when nothing is left to pay). If the buyer then changes a detail, such as the shipping method, **Pay now** gives way to **Update order** with *You changed your details. Update your order to see the total before you pay.* Updating saves the change and shows the new total, so what they pay is what they see.

Opening the settings page takes the **Manage checkout settings** permission.

### The order summary

The **Order summary** card lists each item with its price. A recurring item shows how often it is billed underneath, for example *Every 1 month*. When tax applies, the card shows a **Subtotal** and one line for each tax at each rate, using the tax's display name (or **Tax**) and its rate, such as *VAT 19%*. A tax taken out of prices that include it says *(included)*; see [Markets](markets.md#prices-that-include-tax). The last line, **Due now**, is what the buyer pays today.

When a gift card, store credit or points pay part of the order, the card shows the **Total** first, then a line for each of them with what it pays, such as *Gift card ending 4F2K −$30*, *Store credit* or your rewards program's name, and then **Due now**: what's left to pay by card or another payment method. See [Gift cards, store credit and points](#gift-cards-store-credit-and-points).

## Pay

On the **Payment** step, the buyer:

1. Optionally enters a promotion code, and pays part or all of the order with a gift card, store credit or points (see below).
2. Picks a **Payment method**. Each method is a large button: **Credit or debit card (Stripe)** (*Pay securely online and confirm immediately.*), **Pay Later** (*Confirm now and pay later.*), **Bank transfer** and **Cash on delivery**, as you turned them on (see [Offline payments](payment-providers.md#offline-payments-pay-later-bank-transfer-and-cash-on-delivery)). Only methods that can pay for this purchase are shown: cash on delivery only for an order that is delivered. On a test or development site there may also be **Test payment**, which confirms the payment without taking any money; see [Try the checkout with test payments](payment-providers.md#try-the-checkout-with-test-payments).
3. For card: picks one of their [saved cards](payment-providers.md#saved-cards), or enters the **Name on card** and the **Card details**, optionally ticking **Save this card for next time**, or taps an Apple Pay or Google Pay button when the device offers one.
4. Clicks **Pay now**. When a gift card, store credit or points pay everything, the page says *No payment is required to complete this checkout.* instead of listing payment methods, and the buyer clicks **Place order**.

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

1. Type the code in **Enter a code** (or **Add another code** once one is applied). Capital letters do not matter.
2. Click **Apply**. The page refreshes the order summary; no payment is taken.
3. The box lists each code with what it took off, for example *$10 off*, and the summary shows the new total.

The summary lists a code's discount as *Discount (CODE)*, and an automatic discount by its description, such as *Summer sale*; an automatic discount needs no code. A discount comes off the items, unless it is free shipping, and tax is worked out on the discounted prices.

A buyer can apply up to five codes; a sixth shows *A checkout takes up to 5 codes. Remove one to add another.* When codes can't all be used together, the buyer gets the combination that takes off the most, and a code that isn't used shows *Not used: it can't be combined with* and the discount it gave way to. A code accepted that takes nothing off this order shows *Nothing off this order.* A buyer who opened a [discount link](coupons.md#share-a-discount-link) finds its code already in the box.

To remove a code, click **Remove** next to it. A code that is wrong shows *That code is not recognized.*; a code that is disabled, expired, not started yet or used up shows *That code is no longer available.* A code limited to other payments or another customer shows *That code can't be used for this payment.* A buyer who has used a code as often as it allows one customer sees *You have already used that code as many times as it allows.* Trying many codes in a row shows *Too many codes were tried. Wait a few minutes, then try again.*

If a code runs out or stops working while the buyer is paying, for example someone else took its last use, the payment does not go through. The page shows the order again without the code, with the new total, and says why, for example *The code LAST1 ran out before you paid, so it was taken off. Check your new total, then pay again.*

When the buyer is paying an existing balance (a transaction, from **My Transactions** or a pay link), what the code takes off is taken off the balance: a 100% code pays it without a card, and a smaller code leaves the rest to pay. See [Coupons](coupons.md#coupons-on-a-payment-for-a-transaction).

## Gift cards, store credit and points

With the [Gift Cards](gift-cards.md), [Store Credit](store-credit-and-rewards.md#store-credit) or [Loyalty Points](store-credit-and-rewards.md#loyalty-points) features on, the **Payment** step has a box for each under the promotion code box. They pay like a second way to pay, not like a discount: tax is worked out on the full price, and they then pay toward the total, tax included. Whatever they don't cover is paid by card or another payment method.

| Box | Who sees it | What the buyer does |
| --- | --- | --- |
| **Gift card** | Every buyer, while there's something left to pay. | Types the code in **Enter a gift card code** (or **Add another gift card**) and clicks **Apply**. The card is listed by its last four characters, with what it pays (*$30 used*) and what will be left on it (*$20 left after this order*). |
| **Store credit** | A signed-in buyer with store credit in the order's currency. | Reads *You have $25 of store credit.* and clicks **Use my store credit**. The box then shows *$25 used* and what's left after this order. |
| Your rewards program's name, such as **Rewards** | A signed-in buyer with points. | Reads *You have 1,250 points, worth $12.50.* and clicks **Use my points**. The box then shows *$12.50 paid with your points*. With too few points it says *You have 40 points. Spend them once you have 100.* |

Each one pays up to what's on it, in the order the buyer applied them, and **Remove** takes it off again. The page refreshes the order summary each time; nothing is charged until the buyer pays. They pay only the one-time part of an order: never a subscription's recurring charges, a payment of an existing balance (a transaction or a pay link), or a payment that saves a card for later charges, such as installments. On those checkouts the boxes aren't shown.

| What the buyer sees | What it means |
| --- | --- |
| *That gift card code is not recognized.* | No gift card has that code. |
| *That gift card can't be used.* | The card is turned off. |
| *That gift card has expired.* | Its expiry date has passed. |
| *That gift card has nothing left on it.* | It is used up. |
| *That gift card is in EUR, and this order is in USD.* | A card pays only toward orders in its own currency. |
| *That gift card is being used at another checkout right now, or there's nothing left to pay.* | Another checkout holds what's on the card, or the order is already covered. |
| *Too many codes were tried. Wait a few minutes, then try again.* | Many gift card codes were tried in a row from the same place. |

While the buyer pays the rest, what each balance pays is held for this checkout, so the same money can't be spent twice, and it's taken off when the order is paid. A checkout that is canceled, fails or expires lets it go. If a balance changed in the meantime, for example the card was spent in another tab, the payment doesn't go through, the page shows the order again with what's left to pay, and says why:

- *Gift card ending 4F2K has less on it than when it was applied, so it pays less. Check what's left to pay, then pay again.*
- *Gift card ending 4F2K can't be used any more, so it was taken off. Check what's left to pay, then pay again.*
- *We couldn't reserve Gift card ending 4F2K just now. Please try again.*

A buyer who began paying by card and then applied a gift card isn't charged the old total: a card payment they didn't finish is set aside, and the next one is for what's due now. One they did finish, for example in another tab, stands.

The order, its page, its invoice and its confirmation show each one as *Paid with Gift card ending 4F2K* (or *Paid with Store credit*) and the amount. When the order is refunded, the card is refunded first and the balances last; see [Refund an order](carts-and-orders.md#refund-an-order).

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
