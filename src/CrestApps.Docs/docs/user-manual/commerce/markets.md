---
sidebar_label: Markets
title: Markets
description: "Sell to several countries on their own terms: a currency and exchange rate, price rounding, a language and prices with or without tax for each market, a market picker on the storefront, and products left out of the markets they aren't sold in."
technical_manual:
  - modules/markets
---

A **market** is a group of countries you sell to on the same terms. Its shoppers see and pay prices in the market's currency, read the storefront in its language, and pay prices with or without tax, as you choose. Use markets when you sell abroad: a *Europe* market in euros with VAT included, a *United Kingdom* market in pounds, and your own prices for everywhere else.

| | |
| --- | --- |
| **Menu** | Commerce > Markets |
| **Permission** | Manage markets |
| **Feature** | Markets (it needs Products) |

<AskYourAdmin />

## How a market works

Your store has one currency of its own: the one set under **Settings > Commerce > Checkout**. In this page it's called **your store's currency**. A market adds another currency, and an **exchange rate**: how much of the market's currency 1 unit of your store's currency is worth.

When a shopper is in a market:

- A price you entered for the product **in the market's currency** is the price they pay, as it is.
- Otherwise the price in your store's currency is **converted** at the market's rate, then **rounded up** by the market's rounding rule.
- Product cards, the product page (with its variants and add-ons), the cart and the checkout all show prices in the market's currency, and the order is charged in it.
- Shipping is charged in the market's currency too. See [Shipping in a market](#shipping-in-a-market).

Shoppers in countries that aren't in a market, and everyone in the admin, see your store's own prices.

## Add a market

1. Open **Commerce > Markets** and click **Add market**.
2. Fill in the fields (below), and click **Save**.

*The market was added. Shoppers in its countries now see prices in EUR.* confirms it. Each market in the list shows whether it's **On** or **Off**, its currency and rate, **Tax included** when its prices include tax, and its language.

| Field | What it does |
| --- | --- |
| **Name** | What shoppers see in the market picker, such as *Germany* or *Europe*. Required. |
| **Countries** | The two-letter codes of the market's countries, separated by commas, such as *DE, AT*. A country can be in one market only: a code already in another market is refused with the name of that market. |
| **Currency** | The three-letter code of the currency the market's shoppers pay in, such as *EUR*. |
| **Exchange rate** | How much of the market's currency 1 unit of your store's currency is worth. Prices in your store's currency are multiplied by it. It must be more than zero. |
| **Price rounding** | How a converted price is rounded up. See [Price rounding](#price-rounding). A price you entered in the market's currency is never rounded. |
| **Language** | The language the storefront switches to when a shopper picks the market, or **Keep the shopper's language**. The list shows your site's languages; add one under **Configuration > Settings > Localization**. |
| **Prices include tax** | Shoppers pay the price they see, and the tax, such as VAT, is taken out of it. Off, tax is added at checkout. See [Prices that include tax](#prices-that-include-tax). |
| **On** | A market that's off isn't offered, and its shoppers see your store's own prices. |

To change a market, click **Edit**. To remove one, click **Delete**: its shoppers see your store's own prices again.

Exchange rates don't update by themselves. Check them from time to time, and edit the market when a rate moves.

## Price rounding

A converted price is rounded **up**, so it's never below what the rate gives. With your store in US dollars, a market in euros at a rate of *0.92*, and a product at $26.00 (23.92 in euros):

| Price rounding | The shopper sees |
| --- | --- |
| **To the cent (none)** | 23.92 |
| **Up to a whole amount, such as 24.00** | 24.00 |
| **Up to .99, such as 23.99** | 23.99 |
| **Up to .95, such as 23.95** | 23.95 |

For a currency without cents, such as the Japanese yen, the .99 and .95 rules round up to a whole amount.

What an add-on adds to a product, such as gift wrapping, is converted to the cent and not rounded by the rule, and so are shipping charges.

## Set your own price in a market

Rounding gives tidy prices, but you may want an exact price in a market, such as 25.00 euros. Add the product's price in the market's currency:

1. Edit the product. It needs the **Product prices** part; see [Offer several prices](products-and-currencies.md#offer-several-prices).
2. Click **Add a price**, enter the **Amount**, and pick the market's currency next to it, such as *EUR*.
3. Give it the same **Name** and **Charged** as the price it stands in for, and save.

Shoppers in a market with that currency pay that price. Everyone else keeps seeing the product's other prices.

**Subscriptions** and prices the buyer names (**Buyer names the amount**) are never converted. They're sold in a market's currency only when the product has a price of that kind in it.

## The market picker

When you have at least one market that's on, the storefront's top bar shows the shopper's market and its currency, such as *Germany · EUR*, with a globe icon. Clicking it lists your markets under **Shop in**, and **Everywhere else**, which shows your store's own prices in your store's currency.

The shopper's market is:

1. the one they picked in the market picker, which their browser remembers for a year;
2. otherwise, the market of the country their storefront language is for: a shopper reading the storefront in *German (Germany)* is in your market with Germany in it;
3. otherwise none: they see your store's own prices.

Picking a market that has a **Language** switches the storefront to it. Picking **Everywhere else** keeps the language they're reading in.

Your theme designer can restyle the picker or hide it; see the [Technical Manual](../../modules/markets.md#the-market-picker).

## The cart when a shopper changes market

A shopper who picks another market sees their cart in the new market's currency at once, priced again there. The cart doesn't say the prices changed, because the shopper changed currency, not the prices. An item that isn't sold in the new market says so in the cart, and has to be removed before checking out.

## Prices that include tax

In many countries shoppers expect the price they see to be the price they pay, with VAT in it. Turn on **Prices include tax** for those markets.

At checkout, the tax your [tax rules](taxes.md) give is then taken out of the prices instead of added on top: a shopper who saw 23.99 pays 23.99, and the order summary shows the VAT in it, such as *VAT 19% (included)*. With it off, the summary adds the tax to the prices.

The checkout uses the market of the order's currency that has the country the order goes to. When none has it, it uses the shopper's own market, if the order is in that market's currency.

## Shipping in a market

Each shipping method has a **Currency** (see [Shipping](shipping.md#methods-how-you-ship-and-what-it-costs)). For a shopper in a market:

- When the zone has methods in the market's currency, those are offered, as they are.
- Otherwise the zone's methods in your store's currency (methods with no currency are in your store's) are offered, converted at the market's rate.

:::note[Free shipping and table rates in a market]
**Free from a subtotal of** and the **Table rate** rows that go by subtotal compare the amount you entered with the order's subtotal in the order's currency, without converting it. For a market, add methods in its currency, with amounts in that currency.
:::

## Leave a product out of a market

Some products can't be sold everywhere, such as a product not allowed in one country. On the product's editor, under **Not sold in**, tick the markets it isn't sold in, and save. The list shows each market with its countries; it appears once you have a market.

Shoppers in a ticked market still find the product in your listings, but its page and card say *Not sold in Germany.* (with the market's name) instead of an **Add to cart** button. The cart refuses it, and apps using the [headless API](headless-api.md) see it as not for sale.

## Languages

With a **Language** set on a market, the storefront switches to it when a shopper picks the market. Whatever language a buyer shopped in, their order keeps it, and these emails are written in it: the order confirmation, the shipped email, the emails about a return, the gift card email and the email that reminds a buyer of a checkout they left.

To show your products in several languages, turn on Orchard Core's **Content Localization** feature and translate each product. Each translation is listed in its own language, and is a product of its own, with its own prices and stock.

## What markets don't change

- **Listing sorts and price filters** work in your store's currency. A shopper in a market who sorts by price, or filters by a price range, sorts and filters by your store's prices.
- **Reports** keep each order in the currency it was paid in, and never add currencies together.
- **The admin** always shows your store's own prices.

## Example: sell in Europe with VAT included

1. Your store's currency is *USD*.
2. Under **Commerce > Markets**, add *Europe* with the countries *DE, FR, IT, ES, NL, AT*, currency *EUR*, exchange rate *0.92*, rounding **Up to .99, such as 23.99**, language *German (Germany)* and **Prices include tax** on.
3. Run the **EU VAT standard rates** recipe to add VAT for each EU country; see [Taxes](taxes.md#eu-vat-standard-rates).
4. Add a shipping method in *EUR* to your Europe shipping zone, such as *Standard* at 6.90.

A shopper in France who picks *Europe* sees a $26.00 product at 23.99 euros, and pays 23.99 plus 6.90 shipping, with France's VAT taken out of both.

## Troubleshooting

| Problem | What to check |
| --- | --- |
| The market picker doesn't show. | You need a market that's **On**, with a currency and an exchange rate above zero. |
| A shopper sees your store's own prices. | Their country isn't in a market, or they picked **Everywhere else**, or the market is **Off**. |
| The storefront doesn't switch language. | The market's **Language** must be one of your site's languages under **Configuration > Settings > Localization**. |
| The cart says an item *is priced in GBP, and your cart is in EUR.* | The product is priced in a currency that isn't your store's or a market's, so it can't be converted. Give it a price in your store's currency or in the market's. |
| No shipping method is offered in a market. | Check that the zone has methods in the market's currency or in your store's currency, and that they're **On**. |

## Related pages

- [Products and currencies](products-and-currencies.md)
- [Taxes](taxes.md)
- [Shipping](shipping.md)
- [Headless commerce API](headless-api.md)
