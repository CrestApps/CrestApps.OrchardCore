---
sidebar_label: Shipping
title: Shipping
description: Set where you ship, the shipping methods buyers in each place can choose, what each charges, and product weights.
technical_manual:
  - modules/shipping
---

**Shipping** sets what buyers pay to have their order sent: where you ship (zones), how (methods such as *Standard* or *Express*), and what each charges, from a flat amount, a table by weight or order size, or free above an order amount.

| | |
| --- | --- |
| **Menu** | Commerce > Shipping; Settings > Commerce > Shipping |
| **Permission** | **Manage shipping zones, methods and settings** |
| **Feature** | Shipping |

<AskYourAdmin />

## Zones: where you ship

A zone is a set of countries, such as *Domestic* or *Europe*. Buyers see the methods of the zone their address is in.

1. Go to **Commerce > Shipping** and click **Add Shipping Zone**.
2. Give it a **Name** (only you see it) and choose its **Countries**.
3. Click **Save**. The zone's page opens, where you add its methods.

To cover only part of a country, such as the mainland without islands far away, save the zone first: each chosen country with regions then gets a **Regions** list. Pick the regions to cover, or leave it empty for the whole country.

Zones are tried from the smallest **Position**, and the first that covers an address wins. Put a zone for a few regions (*Alaska and Hawaii*) before the zone for their whole country (*United States*).

Tick **Rest of the world** on one zone to cover every address no other zone covers. Without it, buyers elsewhere can't be shipped to.

## Methods: how you ship, and what it costs

On a zone's page, click **Add method** and pick how it charges:

| Method | Charges |
| --- | --- |
| **Flat rate** | One amount per order, plus an optional amount for each item. |
| **Table rate** | An amount looked up by the order's weight, subtotal or number of items. Each row applies from its value up to the next row's: rows *from 0: 5*, *from 2: 8* and *from 5: 12* charge 8 for an order weighing 3. **Up to** stops offering the method above a value, such as more than a carrier takes. |
| **Free shipping** | Nothing, for orders whose items add up to the **Free from a subtotal of** amount or more. Smaller orders don't see it. |

Each method has:

- a **Name** buyers choose, such as *Standard*, and a **Description** shown under it, such as *3 to 5 business days*;
- **On**, to stop offering it without losing its settings;
- a **Currency**, when the store sells in more than one: the method is offered only for orders in it;
- a **Tax category code**, for how the shipping charge is taxed;
- a **Surcharge per unit** for each shipping class, such as 15 more for each bulky item;
- a **Position** in the zone's list.

## Product weights and shipping classes

Add the **Shipping** part to your product types (under **Content Definition**), then on each product:

- **Weight** is the weight of one unit, packed, for weight tables. A product without a weight counts as weighing nothing.
- **Variant weights** set a different weight for some variants, such as a larger size. A variant left empty weighs what the product does.
- **Shipping class** puts the product in a class a method can charge extra for.

Under **Settings > Commerce > Shipping**:

| Setting | What it does | Default |
| --- | --- | --- |
| **Weight unit** | Kilograms or pounds, for product weights and weight tables. Changing it doesn't convert weights already entered. | Kilograms |
| **Shipping classes** | The classes products can be in, one per line, such as *Bulky* or *Fragile*. | None |

## Try your rates

**Try rates**, on **Commerce > Shipping**, shows which zone an address falls in and what each of its methods charges for an order you describe: country, region, currency, subtotal, weight, number of items and shipping class. No order is placed. Use it to check your zones and tables before buyers see them.

## Related pages

- [Products and currencies](products-and-currencies.md)
- [Inventory](inventory.md)
- [Taxes](taxes.md)
