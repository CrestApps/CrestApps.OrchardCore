---
sidebar_label: Products and Currencies
title: Products and Currencies
description: Choose the currencies you sell in, then turn a content type into something you can sell by adding the Product and Product prices parts.
technical_manual:
  - modules/products
  - modules/commerce
---

Anything you sell on your site is a content item with a price. You set this up in two places: the **currencies** you sell in, managed under **Commerce**, and the **Product** parts you add to a content type so each item gets a price.

## Manage currencies

The currencies you add here are the ones offered in every currency drop-down of the product editors. Add only the currencies you actually sell in.

| | |
| --- | --- |
| **Menu** | Commerce > Currencies |
| **Permission** | Manage currencies |
| **Feature** | Products |

<AskYourAdmin />

### Add a currency

1. Open **Commerce > Currencies** and click **Add Currency**.
2. Enter the **Currency code** and the **Display name**.
3. Click **Save**. The message *A new currency has been created successfully.* confirms it.

| Field | What it does |
| --- | --- |
| **Currency code** | The three-letter ISO 4217 code, for example *USD* or *EUR*. It is fixed after the currency is created, and each code can only be added once. |
| **Display name** | The friendly name shown in currency drop-downs, for example *US Dollar*. Drop-downs show it with the code, such as *US Dollar (USD)*. |

### Change or delete a currency

- To rename a currency, click **Edit**, change the **Display name** and click **Save**. The code cannot be changed; to use a different code, add a new currency.
- To remove a currency, click **Delete** and confirm *Are you sure you want to delete this currency?* Items that already use the code keep it, but they cannot be saved again until you pick a currency that still exists.

When the list is empty it shows *Nothing here! There are no managed currencies at the moment.* Use the **Search** box to find a currency by code or name.

## Make a content type sellable

You add the **Product** part to the content type that describes what you sell, for example *Course* or *Plan*. Every item of that type then gets a price. Content types are edited in the browser like any other content setting.

| | |
| --- | --- |
| **Menu** | Content > Content Definition > Content Types |
| **Permission** | Edit content types |
| **Feature** | Products |

<AskYourAdmin />

1. Open **Content > Content Definition > Content Types** and edit the content type, or create one.
2. Click **Add Parts**, tick **Product**, and save. Tick **Product prices** as well if items should offer more than one way to buy them, such as monthly and yearly.
3. Back on the content type, click **Edit** next to the **Product** part to set its options (below), and save.

### Product part options

| Field | What it does |
| --- | --- |
| **Product Type** | What kind of thing this is: **Undefined**, **Good**, **Service** or **Digital**. |
| **Default currency** | The currency used by items of this type that do not pick their own. Choose from your [managed currencies](#manage-currencies). Without a default and without a currency on the item, the item cannot be sold. |

### Price an item

When you create or edit an item of the type, the **Product** part adds:

| Field | What it does |
| --- | --- |
| **Price** | The selling price. Required, and it cannot be negative. Next to it, pick the currency, or leave **Use the default currency** to use the content type's default. |
| **SKU** | Optional. A stock-keeping unit that uniquely identifies the product in your own records. |

### Offer several prices

The **Product prices** part adds a **Prices** section to the item: the ways the item can be bought. A buyer picks one at checkout. A price that recurs makes the item something people can subscribe to; see [Subscriptions](subscriptions.md).

1. Fill in the first price. A new item starts with one empty price, marked **Default**.
2. Click **Add a price** for another one.
3. To take a price away, tick **Remove this price** and save the item.

| Field | What it does |
| --- | --- |
| **Name** | What the buyer sees, for example *Monthly* or *Yearly*. |
| **Amount** | The price, with its currency next to it. **Default** uses the content type's default currency. Cannot be negative. |
| **Charged** | **Charged once** or **Recurring**. Choosing **Recurring** shows the billing fields below. |
| **Every** / **Interval** | Recurring only: how often the buyer is billed, for example every *1* **Month**. The intervals are **Day**, **Week**, **Month** and **Year**. Both are required for a recurring price. |
| **Cycles** | Recurring only: how many times to bill before stopping. Leave empty to bill until the subscription is canceled. |
| **Trial days** | Recurring only: free days before the first charge. |
| **Start after** | Recurring only: how many days to wait before billing starts. |
| **Setup fee** / **Setup fee shown as** | An extra one-time charge, and the wording the buyer sees for it, for example *Onboarding*. |
| **Default** | The price selected first. Only one price can be the default. |
| **Offered** | Clear it to stop offering the price without deleting it. |
| **Buyer names the amount** | Lets the buyer enter their own amount, for example for a donation. **Minimum** and **Maximum** set the allowed range. |
| **Buyer picks a quantity** | Lets the buyer buy more than one. **Max quantity** sets the most they can pick. |
| **Offered from** / **Offered until** | Optional dates and times between which the price is offered. Outside them, buyers do not see it. |

If the item cannot be saved, a message under the price says why, for example *A recurring price needs a billing interval.* or *Only one price can be the default.*

:::tip[Taxes on products]
To charge tax on a product, add the **Taxation** part to the same content type. See [Taxes](taxes.md).
:::
