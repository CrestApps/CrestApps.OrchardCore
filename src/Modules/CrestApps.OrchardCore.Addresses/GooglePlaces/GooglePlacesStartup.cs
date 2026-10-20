using CrestApps.OrchardCore.Addresses.Core.Autocomplete;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Addresses.GooglePlaces;

[Feature(AddressConstants.Feature.GooglePlaces)]
public sealed class GooglePlacesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(GooglePlacesAddressAutocompleteProvider.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(5));
        services.AddScoped<IAddressAutocompleteProvider, GooglePlacesAddressAutocompleteProvider>();
        services.AddSiteDisplayDriver<GooglePlacesSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(GooglePlacesSettingsDisplayDriver.GroupId, AddressPermissions.ManageAddressSuggestions);
        services.AddPermissionProvider<AddressPermissionProvider>();
        services.AddNavigationProvider<GooglePlacesAdminMenu>();
    }
}

/// <summary>
/// Settings › Address suggestions.
/// </summary>
public sealed class GooglePlacesAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public GooglePlacesAdminMenu(IStringLocalizer<GooglePlacesAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Address suggestions"], S["Address suggestions"].PrefixPosition(), suggestions => suggestions
                    .AddClass("address-suggestions")
                    .Id("addressSuggestions")
                    .Action("Index", "Admin", new RouteValueDictionary { { "area", "OrchardCore.Settings" }, { "groupId", GooglePlacesSettingsDisplayDriver.GroupId } })
                    .Permission(AddressPermissions.ManageAddressSuggestions)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
