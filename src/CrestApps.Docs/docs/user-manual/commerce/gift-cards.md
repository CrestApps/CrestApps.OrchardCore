---
sidebar_label: Gift cards
title: Gift cards
description: Sell gift cards as products, issue them by hand, follow and change what's left on each one, email them, and see how buyers pay with them at checkout and what happens on a refund.
technical_manual:
  - modules/carts-and-orders
  - modules/checkout
---

A **gift card** is a code with money on it. A buyer types the code at checkout and the card pays part or all of the order; what's left stays on the card for next time. You can sell gift cards in your store like any other product, and issue them by hand, for example to make up for a late delivery or as a prize.

| | |
| --- | --- |
| **Menu** | Commerce > Gift cards |
| **Permission** | **Manage gift cards** |
| **Feature** | Gift Cards (it needs Orders) |

<AskYourAdmin />

A gift card pays like a second way to pay, not like a discount: tax is worked out on the full price first, and the card then pays toward the total, tax included. The order summary shows the **Total**, what the card pays, and what's still **Due now** by card or another payment method.

## Sell gift cards

Turning on the feature creates a **Gift card** content type, ready to sell. It is a digital product, so it is never shipped.

1. Open **Content > Content Items**, and create a new **Gift card**.
2. Give it a title, such as *Gift card $50*, and a **Price** of 50. The price is what each card holds.
3. Publish it, and add it to your shop as you would any product.

The product's editor says *This product is a gift card. Each one bought issues a gift card worth its price, emailed to the buyer once the order is paid; a discount on the order doesn't make the card worth less.* To sell several amounts, create one gift card item per amount, such as $25, $50 and $100.

To make another product type sell gift cards, add the **Gift card** part to it under **Content > Content Definition > Content Types**.

When an order with gift cards is paid:

- One card is issued for each gift card bought: an order of two $50 cards issues two cards worth $50 each. Each card is worth the item's price, in the order's currency, with a new 16-character code.
- Each card is emailed to the buyer, at the email on the order. The buyer passes it on to whoever it's for.
- Each card is listed under **Commerce > Gift cards**, linked to its order, and the order's **Notes** say *Issued gift card ending 4F2K worth $50 and emailed it to …*. When the email couldn't be sent, the note says so: send the card from its page.

An order paid offline, such as by bank transfer, is placed before the money arrives. Its cards are issued **turned off**, with the note *Turn it on when the order is paid in full, then email it.*, so nobody can spend a card that isn't paid for yet. When the money arrives, open each card, tick **Enabled**, save, and click **Send**.

:::note[Taxes on gift cards]
A gift card is sold like any product, and your [tax setup](taxes.md) applies to it the same way. Check how gift cards should be taxed where you sell, and set the product's tax category to match.
:::

## Issue a gift card by hand

<YouTube video="orders" title="Video of issuing a gift card by hand" start={292} end={331} />

1. Open **Commerce > Gift cards** and click **Issue gift card**.
2. Enter the **Amount**, and check the **Currency** (it starts with your checkout currency).
3. Fill in the recipient and the other fields you need (below).
4. Leave **Email the card** ticked to send it to the recipient now.
5. Click **Issue**.

*The gift card was created and emailed to …* confirms it, and the card's page opens. When the email couldn't be sent, you see *The gift card was created, but it couldn't be emailed. Check the email settings, then email it from its page.*

| Field | What it does |
| --- | --- |
| **Amount** | What the card holds. Required, more than zero. |
| **Currency** | The three-letter currency code, such as *USD*. A card only pays toward orders in its currency. |
| **Code** | Optional. Leave it blank to have a 16-character code made up, which nobody can guess. Or type your own, 8 to 32 letters and digits, such as a code printed on a physical card. Two cards can't share a code. |
| **Recipient's name** | Who the card is for. Used to greet them in the email. |
| **Recipient's email** | Where the card is sent. Required when **Email the card** is ticked. |
| **Expires (UTC)** | Optional date and time, in UTC, after which the card can't be used. Leave blank for a card that never expires; some places require that. |
| **Message** | Optional. Shown in the email with the card, such as *Happy birthday!* |
| **Note** | Optional. For the store only, such as why the card was given. The recipient never sees it. |
| **Email the card** | Sends the code to the recipient's email, with how to use it. |

## Find a gift card

**Commerce > Gift cards** lists the cards, newest first.

- **Search by code, its last 4 characters, or email** at the top: type the whole code, its last four characters, or part of the recipient's email, and press Enter.
- The status filter in the list header (**Filter by status**) shows **Any status**, **Active**, **Used up**, **Expired** or **Disabled** cards.

The list shows only the last four characters of each code, such as *•••• 4F2K*; click it to open the card. Each card also shows:

| Shown | What it means |
| --- | --- |
| **Active** | Enabled, with money on it, and not expired. |
| **Used up** | Nothing is left on it. |
| **Expired** | Its expiry date has passed. |
| **Disabled** | Turned off. It pays for nothing until you turn it back on. |
| Balance | What's left of what it started with, such as *$30 of $50*. |
| Expires | *Until* its expiry date, or **Never expires**. |
| Who it's for | *For* the recipient's name and email, and *Bought with order …* for a card that was bought. |

When nothing matches, the list shows *Nothing here! There are no gift cards that match the current filter.*

## A gift card's page

A card's page shows its whole **Code**, its **Balance** (*of* what it started with), when it was **Issued**, and for a card that was bought, a link to the order: *Bought with order …*.

You can change the **Recipient's name**, **Recipient's email**, **Expires (UTC)** and **Note**, and clear **Enabled** to stop the card paying for anything without changing its balance. Click **Save**: *The gift card was updated.* The amount on the card is changed separately, below.

### Change the balance

Under **Change the balance**, enter an **Amount** and a **Reason**, and click **Change balance**. A positive amount adds to the card; a negative one takes off it. The message *The balance is now $40.* confirms it. A balance never goes below zero.

A reason is required: *Say why the balance changes. It's kept with the card's activity.* If the card is being spent at a checkout at that very moment, you see *The card is being used at a checkout right now. Try again in a moment.*

### Email the card again

Under **Email the card**, click **Send** to send the code to the recipient's email again, for example when they lost it: *The gift card was emailed to ….* Enter and save the recipient's email first if the card has none. The email says what's on the card now, the code, how to use it, and when it expires.

### Activity

**Activity** lists every change to the balance, newest first: **When**, **What** (such as *Issued*, *Bought with order 1042*, *Spent at checkout*, a refund, or the reason you gave, with who made the change), the **Change**, and the **Balance** after it. Every change to a card is here, so what's on it can always be explained.

## Paying with a gift card

On the checkout's **Payment** step, the buyer types the code in the **Gift card** box under the order summary and clicks **Apply**. Dashes, spaces and capital letters don't matter. The box then lists the card by its last four characters, with how much it pays (*$30 used*) and what will be left on it (*$20 left after this order*), and the summary shows the new amount **Due now**. The buyer can add another card, or click **Remove** to take one off. See [Checkout](checkout.md#gift-cards-store-credit-and-points).

A card pays the one-time part of an order, up to what's on it. When the cards pay everything, no card payment is needed, and the button reads **Place order**. A card can't pay toward a subscription's recurring charges, a payment of an existing balance (a transaction or a pay link), or a payment plan that saves a card to charge later.

The money is held for the checkout while the buyer pays the rest, so two buyers can't spend the same money, and it comes off the card when the order is paid. A checkout that is canceled or left unfinished lets it go. If the card has less on it by the time the buyer pays, for example because it was spent in another tab, the payment doesn't go through, and the buyer sees what's left to pay.

The order, its page, its invoice and its confirmation email show *Paid with Gift card ending 4F2K* and the amount.

## Refunds and cancellations

**An order paid with a gift card.** When you refund it, the money goes back to the card or wallet first, up to what was charged; what the gift card paid goes back on the gift card last. The card's activity shows the refund, and the order's notes say *Put $20 back on Gift card ending 4F2K.* See [Refund an order](carts-and-orders.md#refund-an-order).

**An order that bought a gift card.** When it is canceled, or refunded in full, each card it bought is turned off, as long as nothing was spent from it; the card's note says why, for example *Turned off because order 1042 was canceled.* A card that was already spent from stays on: decide yourself whether to turn it off or take its balance away. A partial refund leaves the cards as they are.

## Codes

- A code made up for you is 16 letters and digits in groups of four, such as *7KQM-4F2K-XN8D-P3VA*, without 0, O, 1 or I, which are easy to misread.
- Only you and whoever got the email see the whole code. The buyer sees the last four characters once it's applied, and so does the list.
- Trying many codes in a row is stopped: *Too many codes were tried. Wait a few minutes, then try again.*

## Related pages

- [Checkout](checkout.md)
- [Carts and orders](carts-and-orders.md)
- [Store credit, points and referrals](store-credit-and-rewards.md)
