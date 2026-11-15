using CrestApps.OrchardCore;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.ExchangeRates;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "Exchange Rates",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Dated exchange rates to the currency the store reports in, entered by hand or fetched each day by a provider.",
    Category = "Commerce"
)]

[assembly: Feature(
    Id = ExchangeRatesConstants.Features.Area,
    Name = "Exchange Rates",
    Description = "Dated exchange rates to the store's reporting currency: entered by hand or fetched daily by a provider, and copied onto invoices and the ledger when they are issued.",
    Category = "Commerce",

    // Its pages are under the Commerce menu, which the Commerce feature draws with its icon.
    Dependencies =
    [
        CommerceConstants.Features.Area,
    ]
)]
