---
sidebar_label: Exchange Rates
sidebar_position: 18
title: Exchange Rates
description: Dated exchange rates to the currency the store reports in, entered by hand or fetched each day by a provider.
user_manual:
  - user-manual/commerce/exchange-rates
---

| Feature | Feature ID | What it adds |
| --- | --- | --- |
| **Exchange Rates** | `CrestApps.OrchardCore.ExchangeRates` | Dated exchange rates, their admin pages and settings, the rate lookup, the daily update from a provider, and their recipe and deployment steps. |

Exchange Rates is the building block for what a currency was worth in another on a day. A rate (`ExchangeRate`, in the `ExchangeRates` collection with `ExchangeRateIndex`) reads `1 {Currency} = {Rate} {BaseCurrency}` and holds from its `Date` until a later rate of the same pair; there is one rate per pair and day, and codes are kept upper case.

## Looking a rate up

`IExchangeRateService` (`.Abstractions`, `DefaultExchangeRateService` in `.Core`):

- `GetBaseCurrencyAsync()`: the currency the store reports in (`ExchangeRateSettings.BaseCurrency`), or nothing.
- `GetRateAsync(currency, baseCurrency, date)`: 1 for the same currency; else the latest rate of the pair on or before the day; else the inverse of the opposite pair's latest (to 10 decimal places); the newer of the two wins when both exist. It returns an `ExchangeRateQuote` (rate, the day it took effect, its source) or nothing.

A module that needs a rate asks this service; it never keeps its own rate table.

## Entering and fetching rates

**Commerce** → **Exchange Rates** lists, adds, edits and deletes rates (**Manage exchange rates**), newest day first, in both admin list layouts. A rate changed by hand is marked `manual`.

**Settings** → **Commerce** → **Exchange Rates** sets the reporting currency, the provider that fetches rates and the currencies it fetches. An `IExchangeRateProvider` (`Name`, `DisplayName`, `GetRatesAsync(baseCurrency, currencies, date)`) is the seam for a feed such as a central bank's; none is built in. The **Exchange Rate Update** background task runs hourly and asks the chosen provider for the site's day (`ExchangeRateUpdater`); a rate of a pair the day already has, entered by hand or fetched before, is never replaced.

## Who uses the rates

- **Invoicing** copies the rate of the issue day to the reporting currency onto an invoice (and a debit note) when it is issued, and onto its ledger transaction, or onto the debt an invoice takes over; a credit note carries its invoice's rate. A document in the reporting currency records 1; a currency without a rate records none. The invoice and credit note data sets have **Reporting currency**, **Exchange rate**, **Total in reporting currency** and **Still due in reporting currency**.

## Recipes and deployment

The **Exchange rates** deployment step and the `ExchangeRates` recipe step move the rates (matched by id); `ExchangeRateSettings` moves with the Settings step. With CrestApps Recipes, each has its schema.
