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
/// Registers the Addresses module services, content definitions, country seeding, and the content-backed
/// country service used to populate country selectors across the platform.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <summary>
    /// Registers the Addresses module services with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<AddressMigrations>();
        services.AddDataMigration<CountryIndexMigrations>();
        services.AddDataMigration<CountrySeedMigrations>();

        services.AddIndexProvider<CountryIndexProvider>();

        services.AddScoped<IContentHandler, CountryContentHandler>();

        services.AddScoped<ICountryService, ContentCountryService>();

        services.AddScoped<IAddressResolver, DefaultAddressResolver>();
    }
}
