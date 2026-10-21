using CrestApps.OrchardCore.Addresses.Core;
using CrestApps.OrchardCore.Addresses.Drivers;
using CrestApps.OrchardCore.Addresses.Endpoints;
using CrestApps.OrchardCore.Addresses.Handlers;
using CrestApps.OrchardCore.Addresses.Indexes;
using CrestApps.OrchardCore.Addresses.Migrations;
using CrestApps.OrchardCore.Addresses.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Addresses;

/// <summary>
/// Registers each country's address format, the checks, printing and address editor built on it, and the endpoint the
/// editor reads layouts from. Countries come from the ISO list
/// until the Address Countries feature replaces them with managed content.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAddressesCore();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // The address editor asks for a country's layout when the country changes. A layout is public: it is the same
        // for everyone, in the visitor's language.
        routes.MapGet("/addresses/formats/{countryCode}", AddressFormatsEndpoint.HandleAsync);

        // Address suggestions, when a provider is set up; none answers 404 and the editor offers none.
        routes.MapGet("/" + AddressAutocompleteEndpoints.SuggestPath, AddressAutocompleteEndpoints.SuggestAsync);
        routes.MapGet("/" + AddressAutocompleteEndpoints.ResolvePath, AddressAutocompleteEndpoints.ResolveAsync);
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
        services.AddDataMigration<CountryIndexMigrations>();
        services.AddDataMigration<CountrySeedMigrations>();

        services.AddIndexProvider<CountryIndexProvider>();

        services.AddScoped<CountryCodeValidator>();
        services.AddScoped<IContentHandler, CountryContentHandler>();
        services.AddScoped<IContentDisplayDriver, CountryCodeDisplayDriver>();

        services.AddScoped<ICountryService, ContentCountryService>();
    }
}
