---
sidebar_label: Coupons
title: Coupons
description: Create promotion codes and automatic discounts, buy X get Y offers, discount links and unique single-use codes, decide which discounts combine, and see how buyers get them at checkout.
technical_manual:
  - modules/checkout
---

A **coupon** is a discount: a promotion code a buyer types at checkout, for example *SPRING10* for 10% off, or an **automatic discount** that applies by itself to every order that qualifies. Each coupon takes a percentage or a fixed amount off, makes shipping free, or is a **buy X get Y** offer such as *buy 2, get the third free*. It applies to the one-time amount or the first billing cycle of a subscription. It can be limited by dates, a usage limit, uses per customer, a minimum or maximum order amount or quantity, and some products or categories.

A buyer can use several codes on one order. Each coupon says which other kinds of discount it **combines with**; when they can't all be used together, the buyer gets the combination that takes off the most. You can also share a code as a **discount link**, or **generate unique codes**, such as single-use codes for cards or emails.

| | |
| --- | --- |
| **Menu** | Commerce > Coupons |
| **Permission** | Manage coupons |
| **Feature** | Checkout |

<AskYourAdmin />

<YouTube video="coupons-and-discounts" title="Coupons and discounts in CrestApps for Orchard Core" />

## Find a coupon

<YouTube video="coupons-and-discounts" title="Finding coupons and their recent uses" start={314} end={355} />

**Commerce > Coupons** lists your coupons, newest first.

- **Search by code or description** at the top: type part of a code or description and press Enter.
- The status filter in the list header (**Filter by status**) shows **Any status**, **Active**, **Not available** or **Disabled** coupons.
- The header counts the coupons on the page and in total.

Each coupon shows its code (click it to edit), or for an automatic discount its description and an **Automatic** badge, then a status badge and a row of details:

| Shown | What it means |
| --- | --- |
| **Active** | Enabled and usable right now. |
| **Not available** | Enabled, but not usable right now: its start date has not come yet, its end date has passed, or its usage limit is used up. |
| **Disabled** | Turned off. Buyers who type it are told it is no longer available. |
| Discount | The percentage, such as *10%*, the currency and amount, such as *USD 5.00*, **Free shipping**, or a buy X get Y offer, such as *Buy 2, get 1 free*. |
| Applies to | **One-time amount**, **First cycle** or **Shipping**. |
| Used | How many times it has been used, for example *3 of 100 used*, or *3 used* when there is no limit. |
| Valid until | The end date, or **No end date**, which is highlighted. |
| Description | Shown when the coupon has one. |

When nothing matches the search or filter, the list shows *Nothing here! There are no coupons that match the current filter.*

:::caution[Codes with no limit]
When at least one enabled coupon has neither a usage limit nor an end date, a warning sits above the list: *A code that leaks will keep discounting until somebody notices.* Give public codes an end date or a usage limit.
:::

## Create a coupon

<YouTube video="coupons-and-discounts" title="Creating a code" start={99} end={151} />

1. Open **Commerce > Coupons** and click **Add coupon**.
2. Under **How it's applied**, choose **The customer enters a code** or **Automatically, on every order that qualifies**.
3. Enter a **Code** and, optionally, a **Description**. An automatic discount needs a description, because that's what the buyer sees; its code is optional.
4. Pick the **Discount type** and fill in **Percentage**, or **Amount** and **Currency**, or what the customer buys and gets.
5. Choose what the coupon **Applies to**, what it **Combines with**, and set any limits.
6. Leave **Enabled** ticked and click **Create**. The message *The coupon was created.* confirms it.

| Field | What it does |
| --- | --- |
| **How it's applied** | **The customer enters a code**, or **Automatically, on every order that qualifies**. See [Automatic discounts](#automatic-discounts). |
| **Code** | What the buyer types. Capital letters do not matter: *spring10* and *SPRING10* are the same code. Required for a code the buyer types, and two coupons cannot share a code. For an automatic discount it's optional: nobody types it, and one is made up when you leave it blank. |
| **Only the generated codes work** | For a code the buyer types: the coupon's own code is refused, and only the [unique codes generated for it](#generate-unique-codes) work. Use it to name a campaign whose codes go one to each customer. |
| **Description** | Shown on the buyer's invoice, for example *Spring sale*. |
| **Discount type** | **Percentage** takes a share off. **Fixed amount** takes a set amount off. **Free shipping** takes off the shipping charged at checkout, whatever the method. **Buy X get Y** makes some items cheaper or free for every so many bought; see [Buy X get Y](#buy-x-get-y). |
| **Percentage** | For **Percentage**: a number from 1 to 100. It is taken off the items bought, not off shipping. For **Buy X get Y**: what's taken off each item the customer gets; 100 makes them free. |
| **Customer buys** / **Customer gets** / **Times per order** | For **Buy X get Y**: how many qualifying items are bought, how many items are then discounted, and the most times the offer applies to one order (blank for as often as the order allows). |
| **Amount** | For **Fixed amount**: how much to take off. Never more than the amount it applies to. |
| **Currency** | For **Fixed amount**: the three-letter currency code, such as *USD*. Required for a fixed amount. The coupon takes nothing off a purchase in another currency. |
| **Applies to** | **The one-time amount** discounts the part of the purchase that is paid once. **The first billing cycle** discounts the first payment of a subscription only; later cycles are charged in full. |
| **Minimum amount** | Optional. Below this amount the coupon does nothing. Only the items count toward it, not shipping. |
| **Maximum amount** | Optional. Above this amount the coupon does nothing, for example a code meant for small orders. Only the items count. |
| **Minimum quantity** | Optional. How many of the items the coupon counts the order needs, for example *3* for "buy 3 or more and save 10%". A coupon limited to some products counts only those. |
| **Usage limit** | Optional. How many purchases can use the coupon in total. Leave blank for unlimited. |
| **Uses per customer** | Optional. How many times one customer can use it. A customer is their account, or the email a guest checks out with. |
| **Valid from (UTC)** / **Valid until (UTC)** | Optional dates and times, in UTC, between which the coupon works. The end must come after the start. |
| **Enabled** | Clear it to stop the coupon working without deleting it. |
| **Combines with** | Which other kinds of discount can be used on the same order: **Product discounts**, **Order discounts** and **Shipping discounts**. See [Combine discounts](#combine-discounts). |

A use is counted when a purchase that used the coupon completes, not when the buyer applies the code. While a buyer is paying, one use is **held** for them, so two buyers can't both get a code's last use: the second is told before paying that it ran out, and sees the new total. A use held by a checkout that is canceled or left unfinished is let go, and a use of an order that is canceled is given back.

A coupon never takes anything off shipping. Its discount is shared among the items in proportion to their prices, and tax is charged on what is left of each one, so a buyer is never taxed on money the coupon took off. The order, its page and its confirmation email show the discount next to the code.

### Automatic discounts

<YouTube video="coupons-and-discounts" title="Automatic discounts and buy X get Y" start={203} end={240} />

An automatic discount needs no code. It applies by itself to every checkout that meets its conditions: its dates, minimums, products and categories, and who it's for. The order summary shows it by its description, for example *Summer sale −USD 12.00*, and the order, its page and its confirmation email show it the same way.

Its usage limit and uses per customer work as for a code. A buyer who has had it as often as it allows one customer, or a discount whose last use has gone, doesn't see it at checkout. If its last use goes while the buyer is paying, the payment doesn't go through: the page shows the order again without it, with the new total, and says *The Summer sale discount ran out before you paid, so it was taken off.* It stays off that checkout.

A buyer who types an automatic discount's code is told *That code is not recognized.* It applies anyway when the order qualifies.

### Buy X get Y

A **Buy X get Y** coupon makes some items cheaper or free for every so many bought, for example *buy 2, get 1 free* (**Customer buys** 2, **Customer gets** 1, **Percentage** 100), or *buy a pair of shoes, get socks half price*.

- **What the customer buys** chooses the products and categories that count toward the items bought. Leave both blank to count any item.
- **What the customer gets** chooses the items that are discounted. Leave both blank for any item.
- The cheapest items the customer can get are the ones discounted, and the dearest of the rest pay for them. On three shirts, *buy 2, get 1 free* makes the cheapest one free; on six, the two cheapest, unless **Times per order** is 1.
- An item counts once: it is either bought or got.
- The discount comes off the discounted items only, so their tax is worked out on what's left of them.

The **Minimum amount** and **Minimum quantity** count the items bought.

### Combine discounts

Every discount is one of three kinds: a **product discount** (limited to some products or categories, and every buy X get Y offer), an **order discount** (off the whole order), or a **shipping discount** (free shipping). Under **Combines with**, tick the kinds a coupon can be used with. Two discounts are used together only when each allows the other's kind. A new coupon combines with nothing until you tick something.

When the discounts on an order can't all be used together, the buyer gets the combination that takes off the most. Each code that isn't used stays in the **Promotion code** box with *Not used: it can't be combined with Summer sale.*, naming the discount it gave way to, so the buyer can remove it.

For example, an automatic *buy 2, get the cheapest free* that combines with shipping discounts, and *FREESHIP* that combines with product discounts, are used together. A *WELCOME10* that combines with nothing then gives way to whichever takes off more.

### Share a discount link

<YouTube video="coupons-and-discounts" title="Discount links and unique codes" start={240} end={281} />

A coupon's page shows its **Discount link**, for example *https://example.com/discount/SPRING10*. Whoever opens it gets the code taken off at checkout, without typing it, for the next seven days on that browser. The page says *The code SPRING10 will be taken off at checkout.* A code that isn't available says *The code SPRING10 isn't available.*

The link opens the home page. To land somewhere else, add `?redirect=` and a page on your site, for example *https://example.com/discount/SPRING10?redirect=/cart*. A page on another site is ignored.

### Generate unique codes

To give each customer their own code, for example single-use codes printed on cards or sent one per email, generate them from a coupon:

1. Create the coupon with the discount and rules the codes share. Tick **Only the generated codes work** if its own code shouldn't work.
2. Save it, then under **Share and generate codes** on its page, enter **How many** (up to 5,000 at a time), an optional **Prefix** such as *VIP-*, and **Uses per code** (1 for single-use; blank for no limit).
3. Click **Generate codes**. The message *Generated 100 codes.* confirms it, and the page shows how many codes there are and how many have been used.
4. Click **Download them (CSV)** for a file with every code, how often it was used, its limit and when it was made.

Codes are eight letters and digits after the prefix, without 0, O, 1 or I, which are easy to misread. Each code shares the coupon's discount, dates, limits per customer, products and who it's for, and has its own uses: a single-use code that was used is used up, while the others still work. Changing or disabling the coupon changes or disables all its codes. Deleting it deletes them.

### Limit who and what a coupon is for

Under **Who and what it is for** you can keep a code from working anywhere it was not meant for, for example financial aid for one family, or a discount that only applies to registration fees. Leave these fields blank for a code anyone can use on any checkout. A code that is limited is refused everywhere else with *That code can't be used for this payment.*

| Field | What it does |
| --- | --- |
| **Only for transaction sources** | The code works only when the buyer pays a [transaction](transactions.md) from one of these sources, such as the source of your registration fees. Separate several with commas. The hint under the field lists the sources on your site. |
| **Only for reference types** | The code works only on a checkout for one of these kinds of thing, such as *Transaction*, or when paying a transaction that is for one of these. Separate several with commas. |
| **Only for the customer with ID** | The code works only for this one customer: their user ID, or the guest customer ID on their transaction. |
| **Only for the customer with email** | The code works only for this one customer, matched against the email the checkout collected, the email on the transaction, or the email on the customer's account. Capital letters do not matter. |

### Limit a coupon to some products or categories

With the Orders feature on, **Products and categories** at the bottom of the coupon form limits a code to the products you choose in **Only for products**, and to products in the categories you choose in **Only for categories**. Categories are the terms of your taxonomies, such as a brand or a product category. Type in the box to search, and tick what you want.

The coupon then takes its discount off those products only, and only they count toward its minimum and maximum amounts. For example, *CONTOSO15* limited to the *Contoso* brand takes 15% off a Contoso watch in an order, and nothing off the backpack next to it. An order with none of them gets nothing off.

### Coupons on a payment for a transaction

A customer can use a promotion code when paying a transaction, from **My Transactions** or a pay link. What the code takes off is recorded on the transaction as a **Discount** adjustment, so the customer owes that much less:

- A 100% code pays the whole balance: the transaction becomes **Paid** without any money being collected.
- A smaller code takes its share off, and the customer pays the rest. Once that payment arrives, the transaction is **Paid** in full.

The code is counted as used only when the transaction is settled. If less was collected than the rest of the balance, the code is not used up. See [Adjust or waive what is owed](transactions.md#adjust-or-waive-what-is-owed) for how adjustments show on the transaction and its invoice.

## Change a coupon

1. In the list, click the coupon's code or **Edit**.
2. Change what you need. The page shows how many times the coupon has been redeemed, for example *Redeemed 12 times.*, how many uses are held by buyers who are paying, and **Recent uses**: when, whether it was **Used**, **Held at checkout**, **Let go** or **Given back**, by whom, for which order, and how much it took off.
3. Click **Save**. The message *The coupon was updated.* confirms it.

To pause a coupon, clear **Enabled** and save. To end a campaign on a date, set **Valid until (UTC)**.

## Delete a coupon

1. In the list, click **Delete** next to the coupon.
2. Confirm with **Delete** in the dialog *Are you sure you want to delete this coupon?*

Buyers can no longer use the code, nor any code generated for it. If you may want it again, clear **Enabled** instead.

## Copy coupons to another site

Coupons can travel in a deployment plan: add the **Coupons** step to export every coupon, with its discount, dates, limits, combining rules, products and categories, but not how often it was used, nor the codes generated for it. Importing it on the other site updates the coupons with the same codes, and adds the rest.

## How a buyer applies a coupon

<YouTube video="coupons-and-discounts" title="What the buyer sees at checkout" start={281} end={314} />

On the **Payment** step of the checkout, the buyer types the code in **Promotion code** and clicks **Apply**. The order summary updates before they pay, and the box lists each code with what it took off, for example *$10 off*. A buyer can add up to five codes, and remove each one. An automatic discount needs nothing from the buyer. See [Apply a promotion code](checkout.md#apply-a-promotion-code) for the messages a buyer can see.

## If you see an error when saving

| Message | What to do |
| --- | --- |
| *A code is required.* | Enter a **Code**. |
| *That code is already in use.* | Another coupon has that code. Pick another one. |
| *Enter a percentage between 1 and 100.* | Fix the **Percentage**. |
| *Enter an amount greater than zero.* | Fix the **Amount**. |
| *A fixed amount needs a currency.* | Enter the **Currency**. |
| *The end date must be after the start date.* | Fix **Valid from (UTC)** or **Valid until (UTC)**. |
| *The maximum amount can't be less than the minimum amount.* | Fix **Maximum amount** or **Minimum amount**. |
| *Leave the limit blank for no limit, or enter a number greater than zero.* | Fix **Uses per customer**. |
| *Leave the limit blank for unlimited, or enter a number greater than zero.* | Fix the **Usage limit**. |
| *Enter the customer's email address, or leave it blank.* | Fix **Only for the customer with email**. |
| *An automatic discount needs a description. It's what the buyer sees.* | Enter a **Description**. |
| *Enter how many items the customer buys, 1 or more.* / *Enter how many items the customer gets, 1 or more.* | Fix **Customer buys** or **Customer gets**. |
| *Leave the minimum blank, or enter a number greater than zero.* | Fix **Minimum quantity**. |
| *Generate between 1 and 5000 codes at a time.* | Fix **How many**. |
| *A prefix is up to 12 letters, digits or dashes.* | Fix the **Prefix**. |
