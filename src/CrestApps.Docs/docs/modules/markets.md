---
sidebar_label: Markets
title: Markets
description: "Markets: groups of countries with their own currency, exchange rate, price rounding, culture and tax treatment; the IShopperCurrency seam that prices products, carts and shipping in the shopper's currency; IProductSaleRestriction for products not sold in a market; the storefront market picker; and the market query value and X-Market header for API clients."
user_manual: user-manual/commerce/markets
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Markets` (Markets) |
| **Category** | Commerce |
| **Depends on** | Commerce, Products (`CrestApps.OrchardCore.Products`) |
| **Seams it fills** | `IShopperCurrency` and `IProductSaleRestriction` (in `CrestApps.OrchardCore.Products.Core`), `ICheckoutTaxProfileHandler` (Checkout) |

The **Markets** feature sells to several countries on their own terms. A `Market` is a set of countries with a currency, an exchange rate from the store's currency, a price rounding rule, a culture and whether its prices include tax. The shopper's market decides the currency that product cards, product pages, the cart, the checkout, shipping and the catalog API price in. Admin screens are described in the [User Manual](../user-manual/commerce/markets.md).

The **store's currency** throughout is `CheckoutSettings.Currency` (**Settings > Commerce > Checkout**). Rates are relative to it.

## Model

Markets live in the `Markets` YesSql collection, indexed by `MarketIndex` (`Name`, `Currency`, `IsEnabled`), through `IMarketStore` (an `ICatalog<Market>` with `PageAsync`).

| `Market` | |
| --- | --- |
| `Name` | Shown in the picker. |
| `Countries` | Two-letter codes, upper case. The admin refuses a code another market already has, so a country resolves to one market. `Covers(country)` tests one. |
| `Currency` | Three-letter code, upper case. |
| `ExchangeRate` | How much of `Currency` one unit of the store's currency is worth. Must be above zero. |
| `Rounding` | `MarketPriceRounding`: `None`, `WholeUnits`, `EndIn99`, `EndIn95`. |
| `Culture` | A culture from `ILocalizationService.GetSupportedCulturesAsync()`, or `null`. |
| `PricesIncludeTax` | Makes the checkout's taxes inclusive (see [Tax](#tax)). |
| `IsEnabled` | Off markets are never resolved or offered. |

`ShopperMarketService.GetMarketsAsync()` returns the markets that are on, have a currency and a rate above zero, by name. Markets have no recipe or deployment step.

`MarketPricing.Convert(amount, fromRate, toRate)` is `amount / fromRate * toRate`. `MarketPricing.Round(amount, currency, rounding)` rounds **up**: `WholeUnits` to the next whole unit, `EndIn99` and `EndIn95` to the next price ending in .99 or .95 (to the next whole unit for a currency with no minor units, such as JPY), `None` to the currency's scale (`CurrencyScale`).

## The shopper's currency seam

Products.Core defines the seam every pricing caller uses:

```csharp
public interface IShopperCurrency
{
    // The currency the current shopper buys in, or null when each product sells in its own.
    ValueTask<string> GetCurrencyAsync(CancellationToken cancellationToken = default);

    // The amount in fromCurrency (the store's when null) as the shopper pays it in toCurrency.
    // isPrice: true rounds by the market's rule; false only to the currency's smallest unit.
    // Returns null when it can't be converted.
    ValueTask<decimal?> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, bool isPrice = true, CancellationToken cancellationToken = default);
}
```

- **`ProductOwnShopperCurrency`** is the default (`TryAddScoped` in the Products startup): no shopper currency, and it converts nothing (an amount converts only to its own currency).
- **`ShopperMarketService`** replaces it when Markets is on. `GetCurrencyAsync` is the current market's currency. `ConvertAsync` converts through the store's currency: the store's rate is 1, a market currency's rate is the shopper's market's when it's in that currency, else the first market in it. A currency that is neither the store's nor a market's can't be converted (`null`).
- **`ShopperCurrencyExtensions.ToShopperAsync(amount, currency, isPrice)`** returns `(Amount, Currency)` as the shopper sees it: converted when they buy in another currency, unchanged when they don't or it can't be converted. Use it for amounts that are only shown.

Register your own `IShopperCurrency` to price from another source, such as a rates service; consumers don't change.

## Resolving the shopper's market

`ShopperMarketService.GetCurrentAsync()` resolves once per request:

1. **None in the admin** (`AdminAttribute.IsApplied`), or without an `HttpContext`, or when no market is on.
2. The **`market` query value**, then the **`X-Market` header**: a market id, or a two-letter country code that a market covers (`ShopperMarketService.Find`).
3. For requests outside `/api` only: the **`orchard-market` cookie** (a market id), then the market of `CultureInfo.CurrentCulture`'s region (`FindByCulture`: `de-DE` is `DE`). Neutral cultures resolve none.

So an API client is never placed in a market by a browser cookie or its language; it names its market on each request. See [API clients](#api-clients).

## The market picker

`MarketNavbarDisplayDriver` adds the `MarketPicker` shape (`MarketPickerViewModel`: `Markets`, `Current`, `StoreCurrency`, `ReturnUrl`) to the front-end `Navbar` at `Detail` / `Content:5`, before the cart link, when a market is on. A theme restyles it by overriding `MarketPicker.cshtml` (or a Liquid template), or hides it with an empty one.

The picker posts `marketId` and `returnUrl` to `POST /market` (`MarketController.Choose`, antiforgery-validated). It sets `orchard-market` to the market id (one year, `HttpOnly`, `SameSite=Lax`, `IsEssential`, `Secure` on HTTPS, path the tenant's `PathBase`), and, when the market has a supported `Culture`, Orchard Core's culture cookie (`CookieRequestCultureProvider.DefaultCookieName`). An empty or unknown `marketId` deletes the market cookie ("Everywhere else") and leaves the culture alone. It redirects to `returnUrl` when local, else to the home page.

## Prices

`DefaultPriceResolver` takes `IShopperCurrency` (a second constructor; the old one still works without conversion). When `ProductSnapshotContext.Currency` differs from the price's currency:

- **`ProductPricePart` prices.** A price the product lists in the requested currency wins: the one with the same `Kind`, `Interval` and `BillingDuration` that's on offer, preferring the same `Name`, then the default. Otherwise a one-time price is converted with `isPrice: true`. A **recurring** price, or one with `AllowCustomAmount`, is never converted: the resolver returns `null` (logged), so it can't be charged in the wrong currency.
- **A single `ProductPart` price** is converted, then volume and role discounts come off the converted price.

Where prices are shown:

| Where | What changes |
| --- | --- |
| Product cards (`ProductListingService.BuildPriceShapeAsync`) | The indexed price and compare-at price go through `ToShopperAsync`. The index, and so listing **sorts and price-range filters**, stay in the products' own currency. |
| Product page (`AddToCartDisplayDriver`) | One-time prices listed in the shopper's currency replace the others; failing those, each is converted. Variant prices are converted as prices; add-on prices with `isPrice: false`. What's shown is only for display: the line is priced again when added. |
| Cart (`CartService`) | See [Carts](#carts). |
| Catalog API (`CatalogApiProductReader`) | `price` comes from `IPriceResolver` with the shopper's currency, falling back to the converted indexed price; `compareAtPrice`, variant prices and add-on prices are converted. `prices` lists each `ProductPricePart` price as the product lists it, in its own currency. |

## Carts

`CartService` takes `IShopperCurrency` and the `IProductSaleRestriction`s through a longer constructor.

- The cart's currency is the shopper's currency, else the currency its lines were added in.
- When the shopper's currency differs from `Cart.Currency` (they changed market), `PriceAsync` prices every line in the new currency and sets `Cart.Currency`. `CartLinePricing.PriceChanged` is `false` for that pricing, and adding or updating a line first rebases each line's `AddedUnitPrice` to its price in the new currency, so a currency change never reads as a price change.
- Add-on surcharges are converted from the product's currency with `isPrice: false`.
- A line that can't be priced in the cart's currency, but can in its own, gets the problem *Mug is priced in GBP, and your cart is in EUR.*, which stops the checkout.

## Not sold in a market

Products.Core defines:

```csharp
public interface IProductSaleRestriction
{
    // Why the product can't be sold to the current shopper, or null when it can.
    ValueTask<string> GetRestrictionAsync(ContentItem product, CancellationToken cancellationToken = default);
}
```

`ProductSaleRestrictionExtensions.GetRestrictionAsync(restrictions, product)` returns the first reason any registered restriction gives. It is asked by:

- `AddToCartDisplayDriver`, which renders the `AddToCartUnavailable` shape (`AddToCartUnavailableViewModel.Reason`) instead of the add-to-cart form;
- `CartService`, which refuses to add the product and gives a line the problem *Mug: Not sold in Germany.*, so the checkout can't go on; the Cart API answers `409` with the code `not-available`;
- the catalog API, which returns `isPurchasable: false`.

Markets registers `MarketSaleRestriction`. The product editor's **Not sold in** list (`ProductMarketsDisplayDriver`, on `ProductPart` at `Content:22`) keeps the ids of the markets ticked in `ProductMarketsPart.NotSoldIn`, stored on the content item beside `ProductPart` (it isn't added to the content type). A shopper whose current market is listed gets *Not sold in Germany.*, with the market's name.

The product is still listed and found by search: leaving it out of listings would need a filterable field in every search backend's index.

Add your own `IProductSaleRestriction` for other rules, such as an age check or a licence; the three places above honour it.

## Shipping

`DefaultShippingRateService` takes `IShopperCurrency` (a second constructor). When the shopper buys in a market's currency and `ShippingRateRequest.Currency` is that currency:

1. The zone's enabled methods whose `Currency` is the market's are quoted as they are.
2. When those give no rate, every other method (a method with no currency is in the store's) is quoted and its amount, after class surcharges, converted with `isPrice: false`. A rate that can't be converted is dropped. `ShippingRate.WithAmount(amount, currency)` carries the new currency.

Outside a market, methods are matched as before: a method with no currency is offered for any order currency.

Rate providers still compare amounts with the request as it is: the `Free` provider's `MinimumSubtotal` and a `Table` rate by subtotal are compared with the subtotal in the order's currency, unconverted. A market that needs those thresholds gets methods in its own currency.

## Tax

`MarketCheckoutTaxProfileHandler` (`ICheckoutTaxProfileHandler`) sets `CheckoutTaxProfile.PriceType` to `TaxPriceType.Inclusive` when the checkout is in a market whose `PricesIncludeTax` is on: the enabled market in the session's currency that covers `profile.Destination.Country`, or else the shopper's current market when the session is in its currency. A profile already inclusive is left alone. The tax rules then extract tax from the prices; see [Taxation](taxation.md#tax-inclusive-vs-tax-exclusive-pricing).

[TaxJar](taxation.md#taxjar) handles only prices that exclude tax, so a checkout in a market with prices that include tax is always taxed by the store's own rules.

## Cultures and orders

The market's `Culture` switches the storefront when the market is picked. Orders keep the culture the buyer shopped in (`Order.Culture`), and their emails are written in it; see [Carts and Orders](carts-and-orders.md#taxes-and-culture-on-the-order). Translated products are Orchard Core content localization: each translation is its own content item, listed in its culture, with its own price and stock. Selling one product across its translations with shared stock and price is not built.

## API clients

An app names its market on **every** request, the catalog, the cart and the checkout calls alike, with either:

- the `market` query value, such as `GET products?market=DE`, or
- the `X-Market` header, such as `X-Market: DE`.

The value is a market id or a two-letter country code a market covers. Without it, a request under `/api` is priced in the products' own currencies. See [Commerce API](commerce-api.md#markets).

## Admin and permissions

| Screen | Permission | Default roles |
| --- | --- | --- |
| **Commerce > Markets** (`MarketsAdminController`, admin route `markets`): the list on Orchard Core's `AdminList`, and the editor | `ManageMarkets` (**Manage markets**) | Administrator |
| **Not sold in** on a product's editor | Editing the product | |

## Related modules

- [Products](products.md#resolving-a-price): `IPriceResolver` and the listing.
- [Carts and Orders](carts-and-orders.md): carts, orders and their culture.
- [Shipping](shipping.md#rates): rates.
- [Taxation](taxation.md): inclusive prices, VAT rules and the one-stop-shop report.
