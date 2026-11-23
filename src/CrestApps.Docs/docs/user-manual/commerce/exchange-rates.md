---
sidebar_label: Exchange rates
title: Exchange rates
description: Keep the rate of each currency you bill in to the one your store reports in.
technical_manual:
  - modules/exchange-rates
---

If you bill customers in more than one currency, keep exchange rates so every invoice records what it was worth in the currency your store reports in.

## Set the reporting currency

1. Go to **Settings** → **Commerce** → **Exchange Rates**.
2. Enter your **Reporting currency**, such as USD.
3. Click **Save**.

## Add a rate

1. Go to **Commerce** → **Exchange Rates** and click **Add exchange rate**.
2. Enter the **Currency** (such as EUR), what it **Converts to** (your reporting currency), the **Rate** (what one unit is worth, such as 1.08) and the day it holds **From**.
3. Click **Save**.

A rate holds until you add a later one for the same currencies; there is one rate per currency and day. You only need rates one way: a rate from EUR to USD also gives USD to EUR.

## What uses the rate

When an invoice in another currency is issued, it records the rate of its issue day, and so does what it adds to the customer's balance. Changing or deleting a rate later never changes invoices already issued. Reports on invoices can then show totals in your reporting currency. An invoice in a currency with no rate yet is issued without one.

If your site has a rate provider installed, choose it under **Fetch rates from** in the settings and list the **Currencies to fetch**: each day's rates are then added for you, and rates you entered yourself are kept.
