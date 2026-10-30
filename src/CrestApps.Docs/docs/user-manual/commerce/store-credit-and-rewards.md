---
sidebar_label: Store credit, points and referrals
title: Store credit, loyalty points and referrals
description: Give customers store credit by hand or as a refund, reward what they buy with loyalty points, and give each customer a referral link that rewards them for a friend's first order, with what buyers see at checkout and on their account.
technical_manual:
  - modules/customers
  - modules/checkout
---

Three features reward the customers who come back, and each pays toward later orders at checkout, after tax, like a [gift card](gift-cards.md):

- **Store credit** is money your store owes a customer, such as a refund you give as credit instead of back to their card. It pays toward their next orders.
- **Loyalty points** are earned on what a customer buys and spent toward later orders, at a value you set.
- **Referrals** give each customer a link to share. A friend who opens it can get a discount on their first order, and the customer is rewarded with store credit or points when that order is placed.

Store credit and points belong to a customer's account, so only signed-in buyers earn and spend them; guests don't. A friend who uses a referral link can buy as a guest, but the customer who shared it needs an account to be rewarded.

| | |
| --- | --- |
| **Menu** | Commerce > Customers (open a customer); Settings > Commerce > Rewards; Settings > Commerce > Referrals |
| **Permission** | **Manage customers**. Buyers see their own under **My account** with **Manage own customer profile and addresses**, which signed-in users have by default. |
| **Feature** | Store Credit, Loyalty Points, Referrals (each needs Customers and Orders) |

<AskYourAdmin />

Watch the video for a tour of giving credit and points, spending them at checkout, and referral links.

<YouTube video="store-credit-loyalty-referrals" title="Store credit, points and referrals in CrestApps for Orchard Core" />
<YouTube video="customers-companies" title="Video of giving store credit and loyalty points, and setting up rewards and referrals" start={268} end={319} />

## Store credit

A customer's store credit is kept per currency: credit in US dollars pays only toward orders in US dollars. Each customer's page shows it, and every change is kept with its reason.

### Give or take away store credit

<YouTube video="store-credit-loyalty-referrals" title="Giving store credit" start={77} end={120} />

1. Open **Commerce > Customers** and open the customer.
2. Under **Store credit**, enter an **Amount**, check the **Currency** (it starts with your checkout currency), and give a **Reason**, such as *Late delivery*.
3. Click **Change**.

A positive amount gives credit; a negative one takes it away. *The customer's store credit is now $25.* confirms it. Credit never goes below zero.

| What you see | What it means |
| --- | --- |
| *Store credit is spent when the customer is signed in, so only customers with an account can be given it.* | The customer is a guest. There's no form to give them credit. |
| *Enter an amount to give, or a negative amount to take away.* | The amount was empty or zero. |
| *Enter the three-letter currency code, such as USD.* | The currency isn't a three-letter code. |
| *Say why the credit changes. It's kept with its activity.* | The reason was empty. |
| *The customer has no store credit in USD to take away.* | You took credit away from a customer who has never had credit in that currency. |
| *The customer's store credit is being used at a checkout right now. Try again in a moment.* | The customer is paying with it at this moment. |

Under the form, a table lists the latest changes to the customer's credit: **When**, **What** (the reason, with who made the change), the **Change** and the **Balance** after it. A customer with none shows *No store credit.*

### Refund as store credit

<YouTube video="store-credit-loyalty-referrals" title="Refunding as store credit" start={120} end={143} />

When you refund an order whose buyer has an account, the **Refund** form on the order's page has **Give it as store credit**. Tick it to add the refund to the buyer's store credit, in the order's currency, instead of sending the money back to how they paid.

- The order counts it as refunded, like any refund: it shows *Partially refunded* or *Refunded*, and the notes say *Gave $20 back as store credit.*, with your reason when you gave one.
- The credit's activity says *Refund of order 1042*, with your reason.
- No money goes back to the card or wallet.

If the credit can't be added, the refund isn't made: *The refund couldn't be given as store credit. The buyer needs an account, and their credit may be in use at a checkout right now.* See [Refund an order](carts-and-orders.md#refund-an-order).

### Store credit at checkout

<YouTube video="store-credit-loyalty-referrals" title="Store credit at checkout" start={143} end={170} />

On the **Payment** step, a signed-in buyer with store credit in the order's currency sees a **Store credit** box under the order summary: *You have $25 of store credit.* and **Use my store credit**. Clicking it puts the credit toward the order, up to what's due. The box then shows how much is used (*$25 used*) and what's left (*$0 left after this order*), with **Remove**, and the summary shows **Store credit** under the **Total**. See [Checkout](checkout.md#gift-cards-store-credit-and-points).

When the order is refunded later, what the store credit paid goes back on it, after the money charged to the card.

## Loyalty points

A signed-in customer earns points on every order and spends them toward later ones. You decide what the program is called, how many points an order earns, and what a point is worth.

### Set up the program

<YouTube video="store-credit-loyalty-referrals" title="The loyalty points program" start={170} end={211} />

Open **Settings > Commerce > Rewards**.

| Setting | What it does | Default |
| --- | --- | --- |
| **Program name** | What buyers see, at checkout and on their account, such as *Contoso Rewards*. Leave it blank for *Rewards*. | *Rewards* |
| **Points per unit spent** | Points earned for each dollar (or unit of the order's currency) spent on goods, after discounts, not counting shipping and tax. 1 gives 1 point per dollar; 0 gives none. | 1 |
| **What a point is worth** | When spent at checkout. 0.01 makes 100 points worth 1. Must be more than zero. | 0.01 |
| **Fewest points to spend** | Buyers with fewer points keep earning until they reach it. 1 or more. | 100 |

### How points are earned and taken back

- **Earned.** When a signed-in customer's order is placed, they earn points on its items after discounts. Shipping, tax, and the part of the order that points paid for earn nothing; a free-shipping discount doesn't lower the points. Points are whole: an order of $45.80 at 1 point per dollar earns 45. The order's notes say, for example, *The buyer earned 45 Rewards points.* An order paid offline earns its points when it's placed, while it still reads *Payment due*.
- **Taken back.** When the order is canceled, every point it earned is taken back. When part of it is refunded, a matching share is taken back: refunding half the order takes back half its points. The activity says *Taken back with order 1042*.
- Points never go below zero. A customer who already spent the points they earned keeps what they bought with them.

### Give or take away points

On the customer's page, the section named after your program shows their points and what they're worth, such as *1,250 points worth $12.50*. Enter **Points** and a **Reason**, and click **Change**. A positive number gives points; a negative one takes them away. *The customer now has 1,300 points.* confirms it.

| What you see | What it means |
| --- | --- |
| *Points are earned and spent when the customer is signed in, so only customers with an account have them.* | The customer is a guest. |
| *Enter points to give, or a negative number to take away.* | The number was empty or zero. |
| *Say why the points change. It's kept with their activity.* | The reason was empty. |
| *The customer has no points to take away.* | The customer has never had points. |
| *The customer's points are being used at a checkout right now. Try again in a moment.* | The customer is spending them at this moment. |

A table lists the latest changes: **When**, **What**, **Points** and **Balance**.

### Points at checkout

<YouTube video="store-credit-loyalty-referrals" title="Points at checkout" start={211} end={237} />

On the **Payment** step, a signed-in buyer with points sees a box named after your program:

- With enough points: *You have 1,250 points, worth $12.50.* and **Use my points**. Clicking it pays toward the order with their points, up to what's due. The box then says *$12.50 paid with your points*, with **Remove**, and the summary shows the program's name under the **Total**.
- With fewer than the **Fewest points to spend**: *You have 40 points. Spend them once you have 100.*

Points are worth their value to the cent below, and only the points needed are spent: when the order needs $8 of a $12.50 balance, 800 points are spent at 0.01 each and the rest stay. When the order is refunded, the points it spent are given back, after the money charged to the card.

## Referrals

Each signed-in customer gets a referral link, such as *https://example.com/refer/K7PQ2MXA*, under **Refer a friend** in their account. A friend who opens it can get a discount on their first order, and the customer is rewarded when that order is placed.

### Set up referrals

<YouTube video="store-credit-loyalty-referrals" title="Setting up referrals" start={237} end={268} />

Open **Settings > Commerce > Referrals**.

| Setting | What it does | Default |
| --- | --- | --- |
| **What a friend gets** | **Nothing**, or one of your [coupons](coupons.md) (not an automatic discount). Each friend gets a single-use code of this coupon, taken off at checkout. | **Nothing** |
| **What the customer gets** | **Nothing**, **Store credit** or **Loyalty points**, for each friend's first order. Store credit needs the Store Credit feature, and points the Loyalty Points feature. | **Nothing** |
| **Store credit** | How much store credit, for **Store credit**. More than zero. It's given in the currency of the friend's order. | 10 |
| **Points** | How many points, for **Loyalty points**. 1 or more. | 500 |
| **Least a friend's first order spends** | On goods, after discounts. 0 rewards any first order. | 0 |

:::tip[Keep the friend's coupon to its generated codes]
Create a coupon for referrals, such as *15% off your first order*, and tick **Only the generated codes work** on it, so its own code can't be shared: friends then only get their own single-use codes. Each friend's code is one of the coupon's [generated codes](coupons.md#generate-unique-codes), starting with *REF-*.
:::

### When a friend opens the link

<YouTube video="store-credit-loyalty-referrals" title="The referral link and the reward" start={268} end={308} />

The link opens your home page with a message:

| The friend sees | When |
| --- | --- |
| *Ana sent you a discount: 15% off your first order. It's taken off at checkout.* | You offer a coupon. The discount is the coupon's description, or the friend's code when it has none. |
| *Welcome! Ana sent you.* | You offer no coupon, the coupon can't be used right now (disabled, expired or used up), or this browser already got a code from the link. |
| *That referral link isn't valid.* | The link's code doesn't belong to anyone. |
| *This is your own referral link. Share it with friends: you're rewarded for their first order.* | The customer opened their own link while signed in. |

The browser remembers the referral, and the friend's code, for 30 days. Opening the link again on the same browser gives no new code, and codes are limited per visitor, so nobody collects a pile of them. The code is in the friend's **Promotion code** box at checkout, and the coupon's own rules apply to it, such as its dates and minimum amount.

### When the customer is rewarded

When the friend's order is placed, the customer who shared the link is given the reward, once, when:

- it is the friend's first order: they have no other paid order, under their account or, for a guest, their email;
- the friend hasn't been referred before: a customer is rewarded once per friend, known by their email;
- the friend isn't the customer: not the same account, nor the same email;
- the order spends at least **Least a friend's first order spends** on goods, after discounts;
- the customer has an account, and the reward's feature is on.

The friend's order notes say *Referred by Ana, who was given $10 store credit.*, and the reward's activity on the customer says *Referred Ben, who placed order 1042*. When the friend's order is canceled, the reward that was given is taken back, even if the settings changed since, and the friend can be referred again. A refund doesn't take it back.

## What buyers see in their account

A signed-in buyer opens these from **My account** on your storefront, alongside their orders and addresses (see [The buyer's account](carts-and-orders.md#the-buyers-account)):

| Page | What it shows | Feature |
| --- | --- | --- |
| **Store credit** | Their store credit, *At checkout, click Use my store credit and it pays toward your order.*, and each change. With none, *You have no store credit.* | Store Credit |
| **Rewards** | Their points under your program's name, what they're worth at checkout, how they earn and spend them, and each change. | Loyalty Points |
| **Refer a friend** | Their link with a **Copy** button, what a friend gets and what they get, and the friends who bought through it, by first name, with the reward for each. | Referrals |

## Related pages

- [Customers](customers.md)
- [Gift cards](gift-cards.md)
- [Checkout](checkout.md)
- [Coupons](coupons.md)
