---
sidebar_label: Welcome Coupon
title: Give New Subscribers a Welcome Coupon
description: "Give each new address on a mailing list a one-time discount code from one of your coupons, at sign-up or once the address is confirmed, sent by e-mail or by your e-mail marketing service."
technical_manual:
  - modules/mailing-lists
---

A mailing list can thank each new subscriber with a discount code, such as *15% off your first order*. Each address gets its own code, once per list, ever.

| | |
| --- | --- |
| **Menu** | **Mailing Lists** → **Lists**, then **Edit** on a list |
| **Permission** | Manage mailing lists |
| **Feature** | Mailing List Welcome Coupon (it needs Checkout) |

<AskYourAdmin />

## Before you start: the coupon

The codes are made from one of your [coupons](../commerce/coupons.md), and work like it: the same discount, dates, products and limits.

1. Create a coupon with the discount, such as 15% off. It must be a coupon with a code the buyer types, not an automatic discount.
2. Tick **Only the generated codes work**, so the coupon's own code can't be shared.

## Turn on the welcome coupon

1. On **Mailing Lists** → **Lists**, click **Edit** on the list.
2. In the **Welcome coupon** card, tick **Give each new address a welcome coupon**.
3. Fill in the fields below.
4. Click **Save**.

| Field | What it does |
| --- | --- |
| **Coupon** | The coupon the codes are made from. Only coupons with a code (not automatic discounts) are listed. |
| **Code prefix** | The text each code starts with, such as *WELCOME-*: up to 12 letters, digits and dashes. |
| **Valid for (days)** | How long a code works after it is given, such as 30. Leave it empty to follow the coupon's end. A code never works after the coupon's end. |
| **Issue the code** | **Once the address is verified** (the safer choice), or **At sign-up**. |
| **Delivered by** | **The site e-mails the code**, or **The list's provider sends it from the member's profile** (for a list at an outside service such as Mailchimp, whose own welcome e-mail sends it). A service that can't do this falls back to the site's e-mail. |

## What a person gets

It depends on the list's **Confirm sign-ups** setting and on **Issue the code**:

| Confirm sign-ups | Issue the code | What the person gets |
| --- | --- | --- |
| Single opt-in | At sign-up | The welcome e-mail with the code, at once. |
| Single opt-in | Once the address is verified | The **Claim the welcome coupon** e-mail. Its button opens a page with **Confirm**. Once they click it, the code shows on the page and comes in the welcome e-mail. |
| Double opt-in | At sign-up | The code is already in the confirmation e-mail. Once they click **Confirm**, it also shows on the page and comes in the welcome e-mail. |
| Double opt-in | Once the address is verified | Once they click **Confirm**, the code shows on the page and comes in the welcome e-mail. |
| The provider confirms | Either | The code is made when the outside service says the person confirmed. The site e-mails it, unless you chose **The list's provider sends it from the member's profile**: then the code is written to the person's profile at the service right away. |

More things to know:

- A code shows on a web page only after the person confirmed their address.
- A signed-in user who signs up with the confirmed e-mail of their account gets the code by e-mail at once. The sign-up form doesn't show it.
- The welcome coupon is always e-mailed, even when the list's **Send a welcome e-mail** is off.
- People you add yourself or import, and people who joined through the outside service's own form (or whom its admins added), never get a code, even when the service confirms them later.

## One code per address, ever

Each address gets one code per list, ever. Unsubscribing and signing up again gives no second code. Neither does another spelling of the same mailbox, such as *john.smith+news@contoso.com* for *john.smith@contoso.com*, or a Gmail address written with or without its dots.

People you add yourself with **Add member**, or that a recipe brings in, never get a code. People who join through a workflow, or at the outside service (a Mailchimp form, or added by a Mailchimp admin), do.

## See the codes

On the coupon's page, **Download them (CSV)** gives every code made from it. A welcome code shows the e-mail address it is for, when it ends, and the person it was given to.

A person's page under the list's [members](members.md) shows **Welcome coupon: Issued** when they got one.
