using CrestApps.OrchardCore.Addresses.Core;
using CrestApps.OrchardCore.Addresses.Drivers;
using CrestApps.OrchardCore.Addresses.Endpoints;
using CrestApps.OrchardCore.Addresses.Handlers;
using CrestApps.OrchardCore.Addresses.Indexes;
using CrestApps.OrchardCore.Addresses.Migrations;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.RateLimits;

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

        // The editor token is a signed, short-lived token.
        services.AddSignedLinks();

        // The suggestion endpoints are throttled whether or not Orchard Core's Rate Limits feature is on: a minute per
        // network, and a day for the whole site that the tenant's configuration sets.
        services.AddOptions<AddressAutocompleteOptions>()
            .Configure<IServiceProvider>((options, serviceProvider) =>
                serviceProvider.GetService<IShellConfiguration>()?.GetSection(AddressAutocompleteOptions.SectionName).Bind(options));
        services.AddAttemptPolicy(AddressAutocompleteEndpoints.NetworkAttemptPolicy, AddressAutocompleteEndpoints.RequestsPerMinute, TimeSpan.FromMinutes(1));
        services.AddOptions<AttemptLimitOptions>()
            .Configure<IOptions<AddressAutocompleteOptions>>((attempts, addresses) => attempts.AddPolicy(
                AddressAutocompleteEndpoints.DailyAttemptPolicy,
                addresses.Value.DailyRequestLimit,
                TimeSpan.FromDays(1)));
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // The address editor asks for a country's layout when the country changes. A layout is public: it is the same
        // for everyone, in the visitor's language.
        routes.MapGet("/addresses/formats/{countryCode}", AddressFormatsEndpoint.HandleAsync)
            .WithName("AddressFormats");

        // Address suggestions, when a provider is set up; none answers 404 and the editor offers none. The editor puts
        // its token in the address, so only a page that shows the editor can ask.
        routes.MapGet("/" + AddressAutocompleteEndpoints.SuggestPath + "/{token}", AddressAutocompleteEndpoints.SuggestAsync)
            .WithName("AddressSuggestions")
            .WithRateLimitGroup(AddressConstants.AutocompleteRateLimitGroup);
        routes.MapGet("/" + AddressAutocompleteEndpoints.ResolvePath + "/{token}", AddressAutocompleteEndpoints.ResolveAsync)
            .WithName("AddressSuggestionResolve")
            .WithRateLimitGroup(AddressConstants.AutocompleteRateLimitGroup);
    }
}

/// <summary>
/// Holds each visitor to a number of address suggestion calls a minute, across both endpoints, when Orchard Core's
/// Rate Limits feature is on: each call is one the store pays its provider for.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RateLimits)]
public sealed class RateLimitsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RateLimitsOptions>(options => options.AddClientGroupRateLimit(
            AddressConstants.AutocompleteRateLimitGroup,
            AddressAutocompleteEndpoints.RequestsPerMinute,
            TimeSpan.FromMinutes(1),
            networkOnly: true));
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
