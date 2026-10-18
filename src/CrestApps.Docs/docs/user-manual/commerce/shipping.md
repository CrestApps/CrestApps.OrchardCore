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
| **Permission** | **Manage shipping zones, methods and settings**; **Ship orders and update their shipments** to ship orders |
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
| **Carriers** | One per line: the carrier's name, then a vertical bar and its tracking link with `{0}` where the number goes, such as `UPS \| https://www.ups.com/track?tracknum={0}`. Carriers are suggested when shipping, and fill in the tracking link. | None |
| **Email the buyer when a shipment leaves** | Sends the shipped email. Needs an email service. | On |

## What buyers see at checkout

An order with goods in it gets a **Shipping** step after **Contact**:

1. **Ship to:** a signed-in customer picks an address from their address book, their default shipping address first, or enters a new one and can save it. Anyone else starts with the billing address they just gave, and can change it. The address needs a recipient's name; a phone helps the carrier.
2. **Shipping method:** the methods of the address's zone, with what each costs for this order, such as *Standard — 3 to 5 business days — $7.00*. With one method there is nothing to choose. After changing the address, the buyer continues once to see the methods for the new one.

The method chosen becomes a line of the order, such as *Shipping: Standard*, and is taxed like the rest of the order, where it goes: tax rules for the shipping address's jurisdiction apply, and a rule applies to shipping only when **Applies to shipping** is ticked on it. An order that doesn't ship is taxed where it's billed.

A buyer whose address no zone covers is told the store doesn't ship there. Orders of downloads and services have no Shipping step.

The order page, the order link a guest gets and the confirmation email show where the order ships and the shipping method with what it cost.

## Ship an order

A paid order with goods gets a **Shipments** section on its page (**Commerce > Orders**, then the order). It lists what has been shipped, and what is left.

1. Under **New shipment**, set how many of each item go in the parcel. All that's left is filled in; lower a number to ship in parts.
2. Enter the **Carrier** and the **Tracking number**. A carrier listed under **Settings > Commerce > Shipping** fills in the tracking link itself; otherwise paste the **Tracking link**.
3. Leave **It has left: mark it shipped now** ticked, and click **Create shipment**. Untick it to set the items aside while they're packed: the shipment shows as *Being packed*, and **Mark shipped** sends it later.

The buyer gets an email that the shipment is on its way, with what's in it and the tracking link, and sees the shipment and its link on their order page: under **My Orders** for a customer, or on the order link a guest has.

When the carrier says it arrived, click **Delivered**. If it comes back, click **Returned**: its items count as not shipped, so they can be sent again. A shipment still being packed can be **Removed**, which puts its items back.

The order shows how far it has gone: *Unfulfilled*, *Partially fulfilled* (some items have left) or *Fulfilled*, on its page and in the orders list.

With **Workflows** on, the **Shipment Created**, **Shipment Shipped**, **Shipment Delivered** and **Shipment Returned** events start workflows, for example to tell a warehouse or a carrier.

## Try your rates

**Try rates**, on **Commerce > Shipping**, shows which zone an address falls in and what each of its methods charges for an order you describe: country, region, currency, subtotal, weight, number of items and shipping class. No order is placed. Use it to check your zones and tables before buyers see them.

## Try a sample

The **Product listing: online store sample** recipe (**Configuration > Recipes**) adds shipping zones for the United States (Standard, Express by weight and free shipping from $100), Canada and the rest of the world, weights for every product with boots in a *Bulky* class, and UPS, FedEx and USPS as carriers. Buy something from **/shop**, then ship it from the order's page.

## Related pages

- [Products and currencies](products-and-currencies.md)
- [Inventory](inventory.md)
- [Taxes](taxes.md)
