---
sidebar_label: Coupons
title: Coupons
description: Create promotion codes that take a percentage or a fixed amount off a purchase, limit when and how often they work, and see how buyers apply them at checkout.
technical_manual:
  - modules/checkout
---

A **coupon** is a promotion code a buyer types at checkout to get a discount, for example *SPRING10* for 10% off. Each coupon takes either a percentage or a fixed amount off, applies to the one-time amount or the first billing cycle of a subscription, and can be limited by dates, a usage limit and a minimum order amount.

| | |
| --- | --- |
| **Menu** | Commerce > Coupons |
| **Permission** | Manage coupons |
| **Feature** | Checkout |

<AskYourAdmin />

<video controls preload="metadata" width="100%" poster="/img/docs/commerce-coupons.jpg" aria-label="Narrated video of creating a coupon">
  <source src="/img/docs/commerce-coupons.mp4" type="video/mp4" />
  <track kind="captions" src="/img/docs/commerce-coupons.vtt" srcLang="en" label="English" default />
</video>

## Find a coupon

**Commerce > Coupons** lists your coupons, newest first.

- **Search by code or description** at the top: type part of a code or description and press Enter.
- The status filter in the list header (**Filter by status**) shows **Any status**, **Active**, **Not available** or **Disabled** coupons.
- The header counts the coupons on the page and in total.

Each coupon shows its code (click it to edit), a status badge and a row of details:

| Shown | What it means |
| --- | --- |
| **Active** | Enabled and usable right now. |
| **Not available** | Enabled, but not usable right now: its start date has not come yet, its end date has passed, or its usage limit is used up. |
| **Disabled** | Turned off. Buyers who type it are told it is no longer available. |
| Discount | The percentage, such as *10%*, or the currency and amount, such as *USD 5.00*. |
| Applies to | **One-time amount** or **First cycle**. |
| Used | How many times it has been used, for example *3 of 100 used*, or *3 used* when there is no limit. |
| Valid until | The end date, or **No end date**, which is highlighted. |
| Description | Shown when the coupon has one. |

When nothing matches the search or filter, the list shows *Nothing here! There are no coupons that match the current filter.*

:::caution[Codes with no limit]
When at least one enabled coupon has neither a usage limit nor an end date, a warning sits above the list: *A code that leaks will keep discounting until somebody notices.* Give public codes an end date or a usage limit.
:::

## Create a coupon

1. Open **Commerce > Coupons** and click **Add coupon**.
2. Enter a **Code** and, optionally, a **Description**.
3. Pick the **Discount type** and fill in **Percentage**, or **Amount** and **Currency**.
4. Choose what the coupon **Applies to**, and set any limits.
5. Leave **Enabled** ticked and click **Create**. The message *The coupon was created.* confirms it.

| Field | What it does |
| --- | --- |
| **Code** | What the buyer types. Capital letters do not matter: *spring10* and *SPRING10* are the same code. Required, and two coupons cannot share a code. |
| **Description** | Shown on the buyer's invoice, for example *Spring sale*. |
| **Discount type** | **Percentage** takes a share off. **Fixed amount** takes a set amount off. |
| **Percentage** | For **Percentage**: a number from 1 to 100. |
| **Amount** | For **Fixed amount**: how much to take off. Never more than the amount it applies to. |
| **Currency** | For **Fixed amount**: the three-letter currency code, such as *USD*. Required for a fixed amount. The coupon takes nothing off a purchase in another currency. |
| **Applies to** | **The one-time amount** discounts the part of the purchase that is paid once. **The first billing cycle** discounts the first payment of a subscription only; later cycles are charged in full. |
| **Minimum amount** | Optional. Below this amount the coupon does nothing. |
| **Usage limit** | Optional. How many purchases can use the coupon in total. Leave blank for unlimited. |
| **Valid from (UTC)** / **Valid until (UTC)** | Optional dates and times, in UTC, between which the coupon works. The end must come after the start. |
| **Enabled** | Clear it to stop the coupon working without deleting it. |

A use is counted when a purchase that used the coupon completes, not when the buyer applies the code.

## Change a coupon

1. In the list, click the coupon's code or **Edit**.
2. Change what you need. The page shows how many times the coupon has been redeemed, for example *Redeemed 12 times.*
3. Click **Save**. The message *The coupon was updated.* confirms it.

To pause a coupon, clear **Enabled** and save. To end a campaign on a date, set **Valid until (UTC)**.

## Delete a coupon

1. In the list, click **Delete** next to the coupon.
2. Confirm with **Delete** in the dialog *Are you sure you want to delete this coupon?*

Buyers can no longer use the code. If you may want it again, clear **Enabled** instead.

## How a buyer applies a coupon

On the **Payment** step of the checkout, the buyer types the code in **Promotion code** and clicks **Apply**. The order summary updates before they pay, and the box confirms the discount, for example *USD 10.00 off applied.* See [Apply a promotion code](checkout.md#apply-a-promotion-code) for the messages a buyer can see.

## If you see an error when saving

| Message | What to do |
| --- | --- |
| *A code is required.* | Enter a **Code**. |
| *That code is already in use.* | Another coupon has that code. Pick another one. |
| *Enter a percentage between 1 and 100.* | Fix the **Percentage**. |
| *Enter an amount greater than zero.* | Fix the **Amount**. |
| *A fixed amount needs a currency.* | Enter the **Currency**. |
| *The end date must be after the start date.* | Fix **Valid from (UTC)** or **Valid until (UTC)**. |
| *Leave the limit blank for unlimited, or enter a number greater than zero.* | Fix the **Usage limit**. |
