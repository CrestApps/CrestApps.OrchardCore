using CrestApps.OrchardCore.Addresses.Core;
using CrestApps.OrchardCore.Addresses.Drivers;
using CrestApps.OrchardCore.Addresses.Handlers;
using CrestApps.OrchardCore.Addresses.Indexes;
using CrestApps.OrchardCore.Addresses.Migrations;
using CrestApps.OrchardCore.Addresses.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Display.ContentDisplay;
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

        // Sites from earlier 3.0 previews keep their managed countries.
        services.AddDataMigration<AddressCountriesUpgradeMigrations>();
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

        services.AddScoped<CountryCodeValidator>();
        services.AddScoped<IContentHandler, CountryContentHandler>();
        services.AddScoped<IContentDisplayDriver, CountryCodeDisplayDriver>();

        services.AddScoped<ICountryService, ContentCountryService>();

#pragma warning disable CS0618 // Kept for sites that used AddressPart in earlier 3.0 previews.
        services.AddScoped<IAddressResolver, DefaultAddressResolver>();
#pragma warning restore CS0618
    }
}
