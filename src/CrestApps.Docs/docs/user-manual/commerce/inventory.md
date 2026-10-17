---
sidebar_label: Inventory
title: Inventory
description: Count how many of each product and variant you have, receive stock, record counts and corrections, and see every change.
technical_manual:
  - modules/inventory
---

**Inventory** counts how many units of each product, and each variant, you have on hand. Every change is recorded: stock that arrives, corrections, counts, and later what customers buy. You can always see why a number is what it is.

| | |
| --- | --- |
| **Menu** | Commerce > Inventory; Settings > Commerce > Inventory |
| **Permission** | **View inventory** to look, **Manage inventory** to change stock and the settings |
| **Feature** | Inventory |

<AskYourAdmin />

## Start counting a product

Stock is counted for content types that have the **Inventory** part:

1. Under **Content > Content Definition > Content Types**, edit your product type and add the **Inventory** part.
2. Go to **Commerce > Inventory** and click **Find products to count**. Every product of that type, and each of its variants, gets a line with nothing on hand.

From then on, saving a product keeps its lines up to date: a new variant gets its own line, and a removed variant's line is archived with its history.

On the product's editor, the **Inventory** section sets how its stock is kept:

| Setting | What it does |
| --- | --- |
| **Count stock** | Untick it to stop counting the product. Its lines are archived. |
| **Allow backorders** | Keep selling when none are left. |
| **Low-stock threshold** | Stock counts as low at this many or fewer. Leave it empty to use the store's default. |

Below the settings, **On hand** lists each variant with how many are available, and a **Change** button that opens it under **Commerce > Inventory**.

## Find stock

**Commerce > Inventory** lists every product and variant counted, with the fewest available first. Search by SKU or product name, and show **Low stock**, **Out of stock** or **Backordered** lines.

Each line shows:

- **On hand:** the units you hold;
- **Reserved:** units held for orders being paid for;
- **Available:** what can still be sold, which is on hand minus reserved.

## Change stock

Open a line to change it. Each change needs only a number and, if you like, a reason:

- **Received** records stock that arrived, such as a delivery. Enter how many came in.
- **Adjusted** records a correction up or down, such as damaged or found units. Enter a negative number to take stock away.
- **Count** records how many you counted. The count replaces what's on hand, and the history records the difference.

The history lists every change, newest first, with when it happened, what it was, the change, what was on hand after it, and who made it. **Recount from history** recomputes what's on hand from that history, for when a number looks wrong.

Two people changing the same line at once can't overwrite each other: the second waits a moment, or is asked to try again.

## When customers buy

With **Orders** enabled, stock follows each order:

1. **Checkout starts:** the units the buyer is getting are held for them, so nobody else can buy them while they pay. The held units show as **Reserved**, and the item's page lists the orders holding stock and until when.
2. **Not enough left:** if a counted product doesn't have enough available units, and doesn't allow backorders, the buyer is sent back to the cart with a message such as *Only 1 of Mug left. Lower the quantity to check out.* or *Mug is out of stock. Remove it to check out.* Nothing is held for that order.
3. **Paid:** the held units are sold. They leave **On hand**, and the history shows **Sold** with the order.
4. **Not paid:** when the checkout is canceled, declined or abandoned, or the hold runs out, the units are released and can be bought again. A payment that arrives after the hold ran out still sells the units, even if that takes the count below zero.

Two buyers can't both get the last unit: the second is told to try again.

## Settings

Under **Settings > Commerce > Inventory**:

| Setting | What it does | Default |
| --- | --- | --- |
| **Low-stock threshold** | Stock counts as low at this many available units or fewer, for products without their own. Changing it updates every product that uses the default. | 5 |
| **Hold stock for (minutes)** | How long units stay held for a buyer who started checkout and hasn't paid. | 60 |

## Related pages

- [Products and currencies](products-and-currencies.md)
- [Carts and orders](carts-and-orders.md)
