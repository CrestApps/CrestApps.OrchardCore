using CrestApps.OrchardCore.Addresses.Core;
using CrestApps.OrchardCore.Addresses.Handlers;
using CrestApps.OrchardCore.Addresses.Indexes;
using CrestApps.OrchardCore.Addresses.Migrations;
using CrestApps.OrchardCore.Addresses.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Addresses;

/// <summary>
/// Registers each country's address format and the checks and printing built on it. Countries come from the ISO list
/// until the Address Countries feature replaces them with managed content.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAddressesCore();
    }
}

/// <summary>
/// Keeps the countries the site serves as Country content items, seeded from the ISO list.
/// </summary>
[Feature(AddressConstants.Feature.Countries)]
public sealed class CountriesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<AddressMigrations>();
        services.AddDataMigration<GeographicAreaIndexMigrations>();
        services.AddDataMigration<CountrySeedMigrations>();

        services.AddIndexProvider<GeographicAreaIndexProvider>();

        services.AddScoped<IContentHandler, CountryContentHandler>();

        services.AddScoped<ICountryService, ContentCountryService>();
    }
}
