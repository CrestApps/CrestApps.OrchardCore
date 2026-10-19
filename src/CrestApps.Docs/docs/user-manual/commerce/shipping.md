---
sidebar_label: Shipping
title: Shipping
description: Set where you ship, the shipping methods buyers in each place can choose, what each charges, and product weights; offer store pickup and local delivery; quote carriers' rates and buy their labels.
technical_manual:
  - modules/shipping
---

**Shipping** sets what buyers pay to have their order sent: where you ship (zones), how (methods such as *Standard* or *Express*), and what each charges, from a flat amount, a table by weight or order size, free above an order amount, your own local delivery, or the carriers' own rates. Buyers can also pick their order up from one of your locations.

| | |
| --- | --- |
| **Menu** | Commerce > Shipping; Settings > Commerce > Shipping |
| **Permission** | **Manage shipping zones, methods and settings**; **Ship orders and update their shipments** to ship orders |
| **Feature** | Shipping; **EasyPost** for carriers' rates, labels and tracking |

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
| **Local delivery** | Your own delivery to the postal codes you list, for a fee. See [Deliver locally](#deliver-locally). |
| **Carrier rates** | The carriers' own rates for each order, through your shipping account, with an optional markup. See [Carrier rates and labels](#carrier-rates-and-labels). |

Each method has:

- a **Name** buyers choose, such as *Standard*, and a **Description** shown under it, such as *3 to 5 business days*;
- **On**, to stop offering it without losing its settings;
- a **Currency**, when the store sells in more than one: the method is offered only for orders in it;
- a **Tax category code**, for how the shipping charge is taxed;
- a **Surcharge per unit** for each shipping class, such as 15 more for each bulky item;
- a **Position** in the zone's list.

## Deliver locally

To deliver orders yourself near your shop, add a **Local delivery** method to the zone your area is in, and set:

| Setting | What it does |
| --- | --- |
| **Postal codes delivered to** | One per line, or separated by commas. End one with `*` to cover every code starting with it, such as `981*`. Case, spaces and dashes are ignored. |
| **Delivery fee** | What delivery costs. |
| **Free from an order subtotal of** | Delivery is free from this subtotal. Leave it empty to always charge the fee. |
| **Smallest order delivered** | Orders below this subtotal aren't offered local delivery. Leave it at 0 to deliver any order. |

Buyers whose postal code isn't on the list don't see the method, and choose another.

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
| **Store pickup** | The places buyers can pick their order up from. See [Store pickup](#store-pickup). | None |
| **Sending parcels** | Where parcels leave from and the box most orders go in, for carrier rates and labels. See [Carrier rates and labels](#carrier-rates-and-labels). | None |

## What buyers see at checkout

An order with goods in it gets a **Shipping** step after **Contact**:

1. **Ship to:** a signed-in customer picks an address from their address book, their default shipping address first, or enters a new one and can save it. Anyone else starts with the billing address they just gave, and can change it. The address needs a recipient's name; a phone helps the carrier.
2. **Shipping method:** the methods of the address's zone, with what each costs for this order, such as *Standard — 3 to 5 business days — $7.00*. With one method there is nothing to choose. After changing the address, the buyer continues once to see the methods for the new one.

The method chosen becomes a line of the order, such as *Shipping: Standard*, and is taxed like the rest of the order, where it goes: tax rules for the shipping address's jurisdiction apply, and a rule applies to shipping only when **Applies to shipping** is ticked on it. An order that doesn't ship is taxed where it's billed.

A buyer whose address no zone covers is told the store doesn't ship there. Orders of downloads and services have no Shipping step.

The order page, the order link a guest gets and the confirmation email show where the order ships and the shipping method with what it cost.

## Store pickup

Let buyers collect their order from your shop instead of having it shipped. Under **Settings > Commerce > Shipping**, **Store pickup**, fill in the blank **Add a pickup location**:

| Field | What it's for |
| --- | --- |
| **Name** | What buyers see, such as *Downtown store*. |
| **Fee** | What pickup costs; 0 shows *Free*. |
| **Street address**, **City**, **State or region**, **Postal code**, **Country** | Where buyers come. The street, city and country are needed. |
| **What buyers are told** | Such as *Open 9-5, Monday to Saturday. Ready within 2 hours.* Shown when they choose it, and on their order. |
| **Stock comes from** | With stock kept in more than one [location](inventory.md#stock-in-several-locations): the location orders picked up here take their units from, when it has them. **Any location** lets inventory choose. |

Click **Save**; a new blank location is there for the next one, up to 20. Tick **Remove this location** to stop offering one.

**At checkout**, the **Shipping** step asks *How do you want to get it?*: **Ship it to me**, or **Pick up at** each location, with its address, what buyers are told and its fee. Choosing pickup hides the address and the shipping methods. The order is taxed where the location is. Its pages and its invoice show **Pick up at** with the location, and its packing slip says who it's **Picked up by** and where.

**Get the order ready.** On the order's page, the section reads **Pickup at** the location's name, and works like [shipping an order](#ship-an-order) without a carrier or tracking number:

1. Under **Set aside for pickup**, set how many of each item are ready.
2. Leave **It's ready: tell the buyer to come and get it** ticked and click **Set aside**. Untick it while the items are still being got ready (*Being got ready*), and click **Ready for pickup** later.
3. The buyer is emailed *Your order ... is ready for pickup*, with where to collect it and what you told them.
4. When they collect it, click **Picked up**.

## Ship an order

A paid order with goods gets a **Shipments** section on its page (**Commerce > Orders**, then the order). It lists what has been shipped, and what is left.

1. Under **New shipment**, set how many of each item go in the parcel. All that's left is filled in; lower a number to ship in parts.
2. Enter the **Carrier** and the **Tracking number**. A carrier listed under **Settings > Commerce > Shipping** fills in the tracking link itself; otherwise paste the **Tracking link**.
3. Leave **It has left: mark it shipped now** ticked, and click **Create shipment**. Untick it to set the items aside while they're packed: the shipment shows as *Being packed*, and **Mark shipped** sends it later.

The buyer gets an email that the shipment is on its way, with what's in it and the tracking link, and sees the shipment and its link on their order page: under **My Orders** for a customer, on the order link a guest has, or through **Track your order** with their order number and email (see [Guests and their orders](carts-and-orders.md#guests-and-their-orders)).

When the carrier says it arrived, click **Delivered**. If it comes back, click **Returned**: its items count as not shipped, so they can be sent again. A shipment still being packed can be **Removed**, which puts its items back.

Each shipment has a packing slip button, which prints the shipment's items and number to put in the box. See [Print an invoice or a packing slip](carts-and-orders.md#print-an-invoice-or-a-packing-slip). With [EasyPost](#carrier-rates-and-labels) set up, a shipment being packed also has **Buy label**.

The order shows how far it has gone: *Unfulfilled*, *Partially fulfilled* (some items have left) or *Fulfilled*, on its page and in the orders list.

With **Workflows** on, the **Shipment Created**, **Shipment Shipped**, **Shipment Delivered** and **Shipment Returned** events start workflows, for example to tell a warehouse or a carrier.

## Carrier rates and labels

With a carrier provider, buyers see the carriers' own rates at checkout, such as UPS, USPS, FedEx or DHL, and you buy and print the carrier's label from the order. A carrier provider is a shipping account, such as EasyPost, turned on as its own feature.

**Settings > Commerce > Shipping** has a tab for this, before the store's own shipping settings:

- **Providers** chooses the **Default carrier provider** among the enabled ones. Carrier rates methods and **Buy label** use it.
- Each carrier provider, such as **EasyPost**, has its own tab, where it's enabled and set up.
- **General**, **Store pickup** and **Sending parcels** are the store's shipping settings, whichever provider it uses.

### Set it up

1. Turn on the **EasyPost** feature under **Tools > Features**. An EasyPost account is free, and comes with a test API key: test labels aren't charged.
2. On **Settings > Commerce > Shipping**, open the **EasyPost** tab, tick **Enable EasyPost provider**, and paste your **API key**, from your EasyPost dashboard under API Keys. It's kept encrypted and never shown again.
3. On the **Sending parcels** tab, enter where parcels leave from: the **Sender's name**, **Company**, **Street address**, **City**, **State or region**, **Postal code**, **Country** and a **Phone** (some carriers need one for labels).
4. Enter the box most orders go in: its length, width and height (optional, as carriers price by weight without it) and its weight, which is added to the items' weight. Lengths are in inches for a store weighing in pounds, and centimeters for one in kilograms.
5. Click **Save**.

Enabling the only provider makes it the default one, on the **Providers** tab. Disabling the default provider clears it, and carrier rates and labels are off until you choose another.

Carriers price by weight, so give your products a **Weight** (see [Product weights and shipping classes](#product-weights-and-shipping-classes)).

### Offer the carriers' rates at checkout

On a zone's page, add a **Carrier rates** method, and set:

| Setting | What it does | Default |
| --- | --- | --- |
| **Carriers** | Separated by commas, as your account names them, such as `USPS, UPS`. Leave it empty to offer every carrier quoted. | Every carrier |
| **Services offered** | The cheapest this many services, such as 3. 0 offers them all. | 3 |
| **Markup (%)** and **Plus an amount** | Added to each carrier's rate, such as for packing. A negative percentage gives a discount. | None |
| **When the carriers can't be reached, charge** | A fallback amount. Leave it empty to offer nothing then, so the buyer chooses another method. | Empty |

Buyers choose among the services quoted for their address and order, such as *USPS Ground Advantage*, with how many business days it takes when the carrier says. Without a default carrier provider, the method offers only its fallback amount, and its editor says so.

### Buy a label

1. Create the shipment with **It has left: mark it shipped now** unticked, so it's *Being packed*.
2. Click **Buy label** on it. The page shows the parcel's items, its **Weight** (the items' weight plus the box) and the box's size from your settings. Weigh the packed parcel to be sure, and correct them.
3. Click **Compare rates**, choose a rate, and click **Buy label and ship**, then confirm.

Your EasyPost account is charged, the shipment is marked shipped with the carrier and tracking number, and the buyer is emailed the tracking number. The shipment's print button opens the label to print. Labels use your **Sending parcels** address and default box; the buyer's address is the order's.

### Follow the parcels

Tracking updates set themselves up. When your site's **Base URL**, under **Settings > General**, is its public https address, saving the **EasyPost** tab registers a webhook with EasyPost for you, signed with a secret made for it; the tab then says *Registered with EasyPost at ...*. EasyPost can't reach a site on your own computer, so the tab says so until the Base URL is public. Rates and labels work either way.

Once registered, the carrier's latest word, such as *In transit*, shows on the shipment, and the buyer's order page lists where the parcel has been. A parcel the carrier delivers is marked **Delivered** for you.

## Try your rates

**Try rates**, on **Commerce > Shipping**, shows which zone an address falls in and what each of its methods charges for an order you describe: country, region, currency, subtotal, weight, number of items and shipping class. No order is placed. Use it to check your zones and tables before buyers see them.

## Try a sample

The **Product listing: online store sample** recipe (**Configuration > Recipes**) adds shipping zones for the United States (Standard, Express by weight and free shipping from $100), Canada and the rest of the world, weights for every product with boots in a *Bulky* class, and UPS, FedEx and USPS as carriers. Buy something from **/shop**, then ship it from the order's page.

## Related pages

- [Products and currencies](products-and-currencies.md)
- [Inventory](inventory.md)
- [Taxes](taxes.md)
- [Returns and exchanges](returns.md)
