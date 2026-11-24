# Exchange Rates

Dated exchange rates to the currency the store reports in (`CrestApps.OrchardCore.ExchangeRates`).

- **Rates.** `ExchangeRate` (`1 {Currency} = {Rate} {BaseCurrency}` from a day), entered by hand under Commerce > Exchange Rates or fetched each day by an `IExchangeRateProvider`; one rate per pair and day.
- **Lookup.** `IExchangeRateService.GetRateAsync(currency, baseCurrency, date)`: 1 for the same currency, the latest rate of the pair on or before the day, else the inverse of the opposite pair's.
- **Settings.** Settings > Commerce > Exchange Rates: the reporting currency, the provider and the currencies it fetches (`ExchangeRateSettings`).
- **Consumers.** Invoicing copies the rate of the issue day onto an invoice or credit note and its ledger transaction.
- **Data.** The `ExchangeRates` recipe step and deployment step, and `ExchangeRateSettings` in the Settings step.

See [Exchange rates](../../CrestApps.Docs/docs/modules/exchange-rates.md).
